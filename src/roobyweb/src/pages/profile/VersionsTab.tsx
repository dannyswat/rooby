import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { VersionResponse, VersionSnapshotResponse } from '../../api/types'
import { ProblemAlert } from '../../components/ProblemAlert'

export function VersionsTab() {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [versions, setVersions] = useState<VersionResponse[] | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [snapshot, setSnapshot] = useState<VersionSnapshotResponse | null>(null)

  const basePath = `/projects/${project}/profiles/${profile}`

  useEffect(() => {
    void (async () => {
      try {
        setVersions(await api.get<VersionResponse[]>(`${basePath}/versions`))
      } catch (err) {
        setError(err)
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project, profile])

  async function viewVersion(n: number) {
    setSelected(n)
    setSnapshot(null)
    try {
      setSnapshot(await api.get<VersionSnapshotResponse>(`${basePath}/versions/${n}`))
    } catch (err) {
      setError(err)
    }
  }

  return (
    <div className="split-view">
      <div className="split-list">
        <h2>Published versions</h2>
        <ProblemAlert error={error} />
        {versions === null ? (
          <div className="page-loading">Loading…</div>
        ) : (
          <table className="list-table">
            <thead>
              <tr>
                <th>Version</th>
                <th>From</th>
                <th>Description</th>
                <th>Published at</th>
              </tr>
            </thead>
            <tbody>
              {versions.map((v) => (
                <tr
                  key={v.versionId}
                  className={v.versionId === selected ? 'clickable selected' : 'clickable'}
                  onClick={() => viewVersion(v.versionId)}
                >
                  <td>{v.versionId}</td>
                  <td>{v.fromVersionId}</td>
                  <td>{v.description}</td>
                  <td>{v.publishedAt ?? ''}</td>
                </tr>
              ))}
              {versions.length === 0 && (
                <tr>
                  <td colSpan={4}>No published versions yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>
      <div className="split-detail">
        {selected === null && <div className="empty-hint">Select a version to view its snapshot.</div>}
        {selected !== null && snapshot === null && <div className="page-loading">Loading…</div>}
        {snapshot && (
          <div>
            <h3>Version {selected}</h3>
            <h4>Items ({snapshot.items.length})</h4>
            <table className="list-table">
              <thead>
                <tr>
                  <th>Key</th>
                  <th>Type</th>
                  <th>Data type</th>
                </tr>
              </thead>
              <tbody>
                {snapshot.items.map((i) => (
                  <tr key={i.id}>
                    <td>{i.key}</td>
                    <td>{i.itemType}</td>
                    <td>{i.dataType}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <h4>Test cases ({snapshot.testCases.length})</h4>
          </div>
        )}
      </div>
    </div>
  )
}
