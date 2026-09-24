import { useEffect, useState } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { ItemResponse } from '../../api/types'
import { ProblemAlert } from '../../components/ProblemAlert'
import { ItemEditor } from './ItemEditor'

export function ItemsTab() {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const [items, setItems] = useState<ItemResponse[] | null>(null)
  const [error, setError] = useState<unknown>(null)

  const selectedKey = searchParams.get('item')
  const creating = searchParams.get('new') === '1'

  async function load() {
    try {
      setItems(await api.get<ItemResponse[]>(`/projects/${project}/profiles/${profile}/items`))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project, profile])

  function select(key: string | null) {
    if (key === null) {
      setSearchParams({ tab: 'items' })
    } else {
      setSearchParams({ tab: 'items', item: key })
    }
  }

  function startCreate() {
    setSearchParams({ tab: 'items', new: '1' })
  }

  return (
    <div className="split-view">
      <div className="split-list">
        <div className="page-header">
          <h2>Items</h2>
          <button type="button" onClick={startCreate}>
            New item
          </button>
        </div>
        <ProblemAlert error={error} />
        {items === null ? (
          <div className="page-loading">Loading…</div>
        ) : (
          <table className="list-table">
            <thead>
              <tr>
                <th>Key</th>
                <th>Type</th>
                <th>Data type</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((i) => (
                <tr
                  key={i.id}
                  className={i.key === selectedKey ? 'clickable selected' : 'clickable'}
                  onClick={() => select(i.key)}
                >
                  <td>{i.key}</td>
                  <td>{i.itemType}</td>
                  <td>{i.dataType}</td>
                  <td>{i.isDeleted ? 'Deleted' : ''}</td>
                </tr>
              ))}
              {items.length === 0 && (
                <tr>
                  <td colSpan={4}>No items yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>
      <div className="split-detail">
        {creating && (
          <ItemEditor
            mode="create"
            itemKey={null}
            onDone={() => {
              select(null)
              void load()
            }}
          />
        )}
        {!creating && selectedKey && (
          <ItemEditor
            mode="edit"
            itemKey={selectedKey}
            onDone={() => {
              void load()
            }}
          />
        )}
        {!creating && !selectedKey && <div className="empty-hint">Select an item, or create a new one.</div>}
      </div>
    </div>
  )
}
