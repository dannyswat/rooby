import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { TestCaseRequest, TestCaseResponse } from '../../api/types'
import { JsonEditor, isValidJson, toJsonText } from '../../components/JsonEditor'
import { ProblemAlert } from '../../components/ProblemAlert'

export function TestsTab() {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [tests, setTests] = useState<TestCaseResponse[] | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [selected, setSelected] = useState<TestCaseResponse | 'new' | null>(null)

  const basePath = `/projects/${project}/profiles/${profile}/tests`

  async function load() {
    try {
      setTests(await api.get<TestCaseResponse[]>(basePath))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project, profile])

  return (
    <div>
      <div className="page-header">
        <h2>Test cases</h2>
        <button type="button" onClick={() => setSelected(selected === 'new' ? null : 'new')}>
          {selected === 'new' ? 'Cancel' : 'New test case'}
        </button>
      </div>
      <ProblemAlert error={error} />
      {selected && (
        <TestCaseForm
          basePath={basePath}
          testCase={selected === 'new' ? null : selected}
          onSaved={() => {
            setSelected(null)
            void load()
          }}
          onCancel={() => setSelected(null)}
        />
      )}
      {tests === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <table className="list-table">
          <thead>
            <tr>
              <th>Item key</th>
              <th>Remarks</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {tests.map((t) => (
              <tr key={t.id} className="clickable" onClick={() => setSelected(t)}>
                <td>{t.itemKey}</td>
                <td>{t.remarks}</td>
                <td>{t.isDeleted ? 'Deleted' : ''}</td>
              </tr>
            ))}
            {tests.length === 0 && (
              <tr>
                <td colSpan={3}>No test cases yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  )
}

function TestCaseForm({
  basePath,
  testCase,
  onSaved,
  onCancel,
}: {
  basePath: string
  testCase: TestCaseResponse | null
  onSaved: () => void
  onCancel: () => void
}) {
  const [itemKey, setItemKey] = useState(testCase?.itemKey ?? '')
  const [remarks, setRemarks] = useState(testCase?.remarks ?? '')
  const [inputData, setInputData] = useState(toJsonText(testCase?.inputData ?? {}))
  const [outputValue, setOutputValue] = useState(toJsonText(testCase?.outputValue ?? null))
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!isValidJson(inputData) || !isValidJson(outputValue)) {
      return
    }
    setError(null)
    setSubmitting(true)
    try {
      const request: TestCaseRequest = {
        itemKey,
        remarks,
        inputData: JSON.parse(inputData),
        outputValue: JSON.parse(outputValue),
      }
      if (testCase) {
        await api.put(`${basePath}/${testCase.id}`, request, testCase.eTag)
      } else {
        await api.post(basePath, request)
      }
      onSaved()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDelete() {
    if (!testCase) {
      return
    }
    setSubmitting(true)
    try {
      await api.delete(`${basePath}/${testCase.id}`, testCase.eTag)
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
        <label>Item key</label>
        <input value={itemKey} onChange={(e) => setItemKey(e.target.value)} required />
      </div>
      <div className="field">
        <label>Remarks</label>
        <input value={remarks} onChange={(e) => setRemarks(e.target.value)} />
      </div>
      <JsonEditor label="Input data" value={inputData} onChange={setInputData} rows={5} />
      <JsonEditor label="Expected output" value={outputValue} onChange={setOutputValue} rows={5} />
      <div className="form-actions">
        <button type="submit" disabled={submitting}>
          {testCase ? 'Save' : 'Create'}
        </button>
        {testCase && (
          <button type="button" className="danger" onClick={handleDelete} disabled={submitting}>
            Delete
          </button>
        )}
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}
