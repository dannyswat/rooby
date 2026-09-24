import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { DraftDiff, ProfileResponse } from '../../api/types'
import { ProblemAlert } from '../../components/ProblemAlert'

export function DiffTab() {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [diff, setDiff] = useState<DraftDiff | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [showPublish, setShowPublish] = useState(false)
  const [description, setDescription] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const basePath = `/projects/${project}/profiles/${profile}`

  async function load() {
    try {
      setDiff(await api.get<DraftDiff>(`${basePath}/diff`))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project, profile])

  async function handlePublish(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const current = await api.get<ProfileResponse>(basePath)
      await api.post(`${basePath}/publish`, { description }, current.eTag)
      setShowPublish(false)
      setDescription('')
      await load()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDiscard() {
    if (!window.confirm('Discard all draft changes since the last publish? This cannot be undone.')) {
      return
    }
    setSubmitting(true)
    try {
      await api.post(`${basePath}/draft/discard`)
      await load()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div>
      <div className="page-header">
        <h2>Pending changes</h2>
        <div>
          <button type="button" onClick={() => setShowPublish((v) => !v)}>
            Publish…
          </button>
          <button type="button" className="danger" onClick={handleDiscard} disabled={submitting}>
            Discard draft
          </button>
        </div>
      </div>
      <ProblemAlert error={error} />
      {showPublish && (
        <form className="card" onSubmit={handlePublish}>
          <div className="field">
            <label>Publish description</label>
            <input value={description} onChange={(e) => setDescription(e.target.value)} required />
          </div>
          <div className="form-actions">
            <button type="submit" disabled={submitting}>
              Publish
            </button>
          </div>
        </form>
      )}
      {diff === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <>
          {diff.schemaUpdates.length > 0 && (
            <div className="card">
              <h3>Schema updates</h3>
              <ul>
                {diff.schemaUpdates.map((s) => (
                  <li key={s.schemaId}>{s.code}</li>
                ))}
              </ul>
            </div>
          )}
          {diff.items.map((item) => (
            <div className="card" key={item.itemId}>
              <div className="page-header">
                <h3>
                  {item.key} <span className="badge">{item.change}</span>
                </h3>
              </div>
              <div className="field-row">
                <pre className="json-block">{item.before ? JSON.stringify(item.before, null, 2) : '(none)'}</pre>
                <pre className="json-block">{item.after ? JSON.stringify(item.after, null, 2) : '(none)'}</pre>
              </div>
              {item.lines.length > 0 && (
                <table className="list-table">
                  <thead>
                    <tr>
                      <th>Line</th>
                      <th>Change</th>
                    </tr>
                  </thead>
                  <tbody>
                    {item.lines.map((l) => (
                      <tr key={l.lineId}>
                        <td>{l.lineId}</td>
                        <td>{l.change}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          ))}
          {diff.items.length === 0 && diff.schemaUpdates.length === 0 && (
            <div className="empty-hint">No pending changes.</div>
          )}
        </>
      )}
    </div>
  )
}
