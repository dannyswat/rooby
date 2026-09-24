import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { CreateProjectRequest, ProjectResponse } from '../api/types'
import { hasSystemAccess, useAuth } from '../auth/AuthContext'
import { ProblemAlert } from '../components/ProblemAlert'

export function ProjectsPage() {
  const { me } = useAuth()
  const [projects, setProjects] = useState<ProjectResponse[] | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [remark, setRemark] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function load() {
    try {
      setProjects(await api.get<ProjectResponse[]>('/projects'))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  async function handleCreate(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const request: CreateProjectRequest = { code, name, remark: remark || undefined }
      await api.post('/projects', request)
      setCode('')
      setName('')
      setRemark('')
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
        <h1>Projects</h1>
        {hasSystemAccess(me, 'SystemAdmin') && (
          <button type="button" onClick={() => setShowCreate((v) => !v)}>
            {showCreate ? 'Cancel' : 'New project'}
          </button>
        )}
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
            <label>Remark</label>
            <input value={remark} onChange={(e) => setRemark(e.target.value)} />
          </div>
          <button type="submit" disabled={submitting}>
            Create
          </button>
        </form>
      )}
      {projects === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <table className="list-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Remark</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((p) => (
              <tr key={p.id}>
                <td>
                  <Link to={`/projects/${p.code}`}>{p.code}</Link>
                </td>
                <td>{p.name}</td>
                <td>{p.remark}</td>
                <td>{p.isDisabled ? 'Disabled' : 'Active'}</td>
              </tr>
            ))}
            {projects.length === 0 && (
              <tr>
                <td colSpan={4}>No projects yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  )
}
