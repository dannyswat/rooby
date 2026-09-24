import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { CreateProfileRequest, ProfileResponse } from '../../api/types'
import { ProblemAlert } from '../../components/ProblemAlert'

export function ProfilesTab() {
  const { project } = useParams<{ project: string }>()
  const [profiles, setProfiles] = useState<ProfileResponse[] | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [timeZone, setTimeZone] = useState('UTC')
  const [publishUri, setPublishUri] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function load() {
    try {
      setProfiles(await api.get<ProfileResponse[]>(`/projects/${project}/profiles`))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project])

  async function handleCreate(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const request: CreateProfileRequest = { code, name, timeZone, publishUri: publishUri || undefined }
      await api.post(`/projects/${project}/profiles`, request)
      setCode('')
      setName('')
      setPublishUri('')
      setShowCreate(false)
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
        <h2>Profiles</h2>
        <button type="button" onClick={() => setShowCreate((v) => !v)}>
          {showCreate ? 'Cancel' : 'New profile'}
        </button>
      </div>
      <ProblemAlert error={error} />
      {showCreate && (
        <form className="card" onSubmit={handleCreate}>
          <div className="field">
            <label>Code</label>
            <input value={code} onChange={(e) => setCode(e.target.value)} required />
          </div>
          <div className="field">
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="field">
            <label>Time zone (IANA id)</label>
            <input value={timeZone} onChange={(e) => setTimeZone(e.target.value)} />
          </div>
          <div className="field">
            <label>Publish URI (optional)</label>
            <input value={publishUri} onChange={(e) => setPublishUri(e.target.value)} />
          </div>
          <button type="submit" disabled={submitting}>
            Create
          </button>
        </form>
      )}
      {profiles === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <table className="list-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Time zone</th>
              <th>Test gate</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {profiles.map((p) => (
              <tr key={p.id}>
                <td>
                  <Link to={`/projects/${project}/profiles/${p.code}`}>{p.code}</Link>
                </td>
                <td>{p.name}</td>
                <td>{p.timeZone}</td>
                <td>{p.testGate}</td>
                <td>{p.isDisabled ? 'Disabled' : 'Active'}</td>
              </tr>
            ))}
            {profiles.length === 0 && (
              <tr>
                <td colSpan={5}>No profiles yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  )
}
