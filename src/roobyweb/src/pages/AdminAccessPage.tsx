import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../api/client'
import type { GrantAccessRequest, ProfileResponse, ProjectResponse, UserAccessResponse, UserResponse } from '../api/types'
import { ACCESS_FLAGS } from '../api/types'
import { ProblemAlert } from '../components/ProblemAlert'

export function AdminAccessPage() {
  const [users, setUsers] = useState<UserResponse[] | null>(null)
  const [access, setAccess] = useState<UserAccessResponse[] | null>(null)
  const [projects, setProjects] = useState<ProjectResponse[]>([])
  const [error, setError] = useState<unknown>(null)
  const [showGrant, setShowGrant] = useState(false)

  async function load() {
    try {
      const [u, a, p] = await Promise.all([
        api.get<UserResponse[]>('/users'),
        api.get<UserAccessResponse[]>('/access'),
        api.get<ProjectResponse[]>('/projects'),
      ])
      setUsers(u)
      setAccess(a)
      setProjects(p)
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  async function handleRevoke(id: number) {
    try {
      await api.post(`/access/${id}/revoke`)
      await load()
    } catch (err) {
      setError(err)
    }
  }

  function userName(userId: number): string {
    return users?.find((u) => u.id === userId)?.loginName ?? `#${userId}`
  }

  return (
    <div>
      <div className="page-header">
        <h1>Access management</h1>
        <button type="button" onClick={() => setShowGrant((v) => !v)}>
          {showGrant ? 'Cancel' : 'Grant access'}
        </button>
      </div>
      <ProblemAlert error={error} />
      {showGrant && (
        <GrantAccessForm
          users={users ?? []}
          projects={projects}
          onGranted={() => {
            setShowGrant(false)
            void load()
          }}
        />
      )}

      <h2>Users</h2>
      {users === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <table className="list-table">
          <thead>
            <tr>
              <th>Login</th>
              <th>Display name</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.loginName}</td>
                <td>{u.displayName}</td>
                <td>{u.isDisabled ? 'Disabled' : 'Active'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <h2>Access grants</h2>
      {access === null ? (
        <div className="page-loading">Loading…</div>
      ) : (
        <table className="list-table">
          <thead>
            <tr>
              <th>User</th>
              <th>Project</th>
              <th>Profile</th>
              <th>Access level</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {access.map((a) => (
              <tr key={a.id}>
                <td>{userName(a.userId)}</td>
                <td>{a.projectId ?? '(system)'}</td>
                <td>{a.profileId ?? ''}</td>
                <td>{a.accessLevel}</td>
                <td>{a.isRevoked ? 'Revoked' : 'Active'}</td>
                <td>
                  {!a.isRevoked && (
                    <button type="button" className="danger" onClick={() => handleRevoke(a.id)}>
                      Revoke
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {access.length === 0 && (
              <tr>
                <td colSpan={6}>No access grants yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  )
}

function GrantAccessForm({
  users,
  projects,
  onGranted,
}: {
  users: UserResponse[]
  projects: ProjectResponse[]
  onGranted: () => void
}) {
  const [userId, setUserId] = useState<number | ''>('')
  const [projectCode, setProjectCode] = useState('')
  const [profiles, setProfiles] = useState<ProfileResponse[]>([])
  const [profileId, setProfileId] = useState('')
  const [flags, setFlags] = useState<Set<string>>(new Set())
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  const project = projects.find((p) => p.code === projectCode) ?? null

  useEffect(() => {
    setProfileId('')
    if (!projectCode) {
      setProfiles([])
      return
    }
    void (async () => {
      try {
        setProfiles(await api.get<ProfileResponse[]>(`/projects/${projectCode}/profiles`))
      } catch {
        setProfiles([])
      }
    })()
  }, [projectCode])

  function toggleFlag(flag: string) {
    setFlags((prev) => {
      const next = new Set(prev)
      if (next.has(flag)) {
        next.delete(flag)
      } else {
        next.add(flag)
      }
      return next
    })
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (userId === '') {
      return
    }
    setError(null)
    setSubmitting(true)
    try {
      const request: GrantAccessRequest = {
        userId,
        projectId: project?.id ?? null,
        profileId: profileId || null,
        accessLevel: flags.size === 0 ? 'None' : Array.from(flags).join(', '),
      }
      await api.post('/access', request)
      onGranted()
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
        <label>User</label>
        <select value={userId} onChange={(e) => setUserId(e.target.value ? Number(e.target.value) : '')} required>
          <option value="">Select a user…</option>
          {users.map((u) => (
            <option key={u.id} value={u.id}>
              {u.loginName}
            </option>
          ))}
        </select>
      </div>
      <div className="field-row">
        <div className="field">
          <label>Project (optional — blank = system-wide)</label>
          <select value={projectCode} onChange={(e) => setProjectCode(e.target.value)}>
            <option value="">(system)</option>
            {projects.map((p) => (
              <option key={p.id} value={p.code}>
                {p.code}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Profile (optional)</label>
          <select value={profileId} onChange={(e) => setProfileId(e.target.value)} disabled={!projectCode}>
            <option value="">(whole project)</option>
            {profiles.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="field">
        <label>Access level</label>
        <div className="checkbox-list">
          {ACCESS_FLAGS.map((flag) => (
            <label key={flag}>
              <input type="checkbox" checked={flags.has(flag)} onChange={() => toggleFlag(flag)} />
              {flag}
            </label>
          ))}
        </div>
      </div>
      <div className="form-actions">
        <button type="submit" disabled={submitting}>
          Grant
        </button>
      </div>
    </form>
  )
}
