import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { SchemaRequest, SchemaResponse } from '../../api/types'
import { JsonEditor, isValidJson, toJsonText } from '../../components/JsonEditor'
import { ProblemAlert } from '../../components/ProblemAlert'

const emptyDefinition = toJsonText({ type: 'object', properties: {} })

export function SchemasTab() {
  const { project } = useParams<{ project: string }>()
  const [schemas, setSchemas] = useState<SchemaResponse[] | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [selected, setSelected] = useState<SchemaResponse | 'new' | null>(null)

  async function load() {
    try {
      setSchemas(await api.get<SchemaResponse[]>(`/projects/${project}/schemas`))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project])

  return (
    <div>
      <div className="page-header">
        <h2>Schemas</h2>
        <button type="button" onClick={() => setSelected(selected === 'new' ? null : 'new')}>
          {selected === 'new' ? 'Cancel' : 'New schema'}
        </button>
      </div>
      <ProblemAlert error={error} />
      {selected && (
        <SchemaForm
          project={project!}
          schema={selected === 'new' ? null : selected}
          onSaved={() => {
            setSelected(null)
            void load()
          }}
          onCancel={() => setSelected(null)}
        />
      )}
      {schemas === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <table className="list-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Valid from</th>
              <th>Valid to</th>
            </tr>
          </thead>
          <tbody>
            {schemas.map((s) => (
              <tr key={s.id} className="clickable" onClick={() => setSelected(s)}>
                <td>{s.code}</td>
                <td>{s.name}</td>
                <td>{s.validFrom}</td>
                <td>{s.validTo ?? '(open)'}</td>
              </tr>
            ))}
            {schemas.length === 0 && (
              <tr>
                <td colSpan={4}>No schemas yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  )
}

function SchemaForm({
  project,
  schema,
  onSaved,
  onCancel,
}: {
  project: string
  schema: SchemaResponse | null
  onSaved: () => void
  onCancel: () => void
}) {
  const [code, setCode] = useState(schema?.code ?? '')
  const [name, setName] = useState(schema?.name ?? '')
  const [validFrom, setValidFrom] = useState(schema?.validFrom ?? new Date().toISOString().slice(0, 10))
  const [validTo, setValidTo] = useState(schema?.validTo ?? '')
  const [definition, setDefinition] = useState(schema ? toJsonText(schema.definition) : emptyDefinition)
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!isValidJson(definition)) {
      return
    }
    setError(null)
    setSubmitting(true)
    try {
      const request: SchemaRequest = {
        code,
        name,
        validFrom,
        validTo: validTo || null,
        definition: JSON.parse(definition),
      }
      if (schema) {
        await api.put(`/projects/${project}/schemas/${schema.id}`, request, schema.eTag)
      } else {
        await api.post(`/projects/${project}/schemas`, request)
      }
      onSaved()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="card" onSubmit={handleSubmit}>
      <ProblemAlert error={error} />
      <div className="field">
        <label>Code</label>
        <input value={code} onChange={(e) => setCode(e.target.value)} required />
      </div>
      <div className="field">
        <label>Name</label>
        <input value={name} onChange={(e) => setName(e.target.value)} required />
      </div>
      <div className="field-row">
        <div className="field">
          <label>Valid from</label>
          <input type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} required />
        </div>
        <div className="field">
          <label>Valid to (optional)</label>
          <input type="date" value={validTo} onChange={(e) => setValidTo(e.target.value)} />
        </div>
      </div>
      <JsonEditor label="Definition (SPEC §6 subset)" value={definition} onChange={setDefinition} rows={12} />
      <div className="form-actions">
        <button type="submit" disabled={submitting}>
          {schema ? 'Save' : 'Create'}
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}
