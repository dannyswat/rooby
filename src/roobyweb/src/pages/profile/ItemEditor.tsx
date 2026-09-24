import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type {
  CreateItemRequest,
  DataType,
  ItemResponse,
  ItemType,
  LineRequest,
  LineResponse,
  UpdateItemRequest,
} from '../../api/types'
import { DATA_TYPES, ITEM_TYPES } from '../../api/types'
import { JsonEditor, isValidJson, toJsonText } from '../../components/JsonEditor'
import { ProblemAlert } from '../../components/ProblemAlert'

interface ItemEditorProps {
  mode: 'create' | 'edit'
  itemKey: string | null
  onDone: () => void
}

export function ItemEditor({ mode, itemKey, onDone }: ItemEditorProps) {
  return mode === 'create' ? <CreateItemForm onDone={onDone} /> : <EditItemForm itemKey={itemKey!} onDone={onDone} />
}

function CreateItemForm({ onDone }: { onDone: () => void }) {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [key, setKey] = useState('')
  const [itemType, setItemType] = useState<ItemType>('SingleValue')
  const [dataType, setDataType] = useState<DataType>('String')
  const [description, setDescription] = useState('')
  const [content, setContent] = useState(toJsonText({ $v: 1 }))
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!isValidJson(content)) {
      return
    }
    setError(null)
    setSubmitting(true)
    try {
      const request: CreateItemRequest = { key, itemType, dataType, description, content: JSON.parse(content) }
      await api.post(`/projects/${project}/profiles/${profile}/items`, request)
      onDone()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="card" onSubmit={handleSubmit}>
      <h2>New item</h2>
      <ProblemAlert error={error} />
      <div className="field">
        <label>Key</label>
        <input value={key} onChange={(e) => setKey(e.target.value)} required />
      </div>
      <div className="field-row">
        <div className="field">
          <label>Item type</label>
          <select value={itemType} onChange={(e) => setItemType(e.target.value as ItemType)}>
            {ITEM_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Data type</label>
          <select value={dataType} onChange={(e) => setDataType(e.target.value as DataType)}>
            {DATA_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="field">
        <label>Description</label>
        <input value={description} onChange={(e) => setDescription(e.target.value)} />
      </div>
      <JsonEditor label="Content" value={content} onChange={setContent} />
      <div className="form-actions">
        <button type="submit" disabled={submitting}>
          Create
        </button>
      </div>
    </form>
  )
}

function EditItemForm({ itemKey, onDone }: { itemKey: string; onDone: () => void }) {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [item, setItem] = useState<ItemResponse | null>(null)
  const [key, setKey] = useState('')
  const [description, setDescription] = useState('')
  const [content, setContent] = useState('')
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false);

  const basePath = `/projects/${project}/profiles/${profile}/items/${itemKey}`

  async function load() {
    setError(null)
    try {
      const loaded = await api.get<ItemResponse>(basePath)
      setItem(loaded)
      setKey(loaded.key)
      setDescription(loaded.description)
      setContent(toJsonText(loaded.content))
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [itemKey])

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    if (!item || !isValidJson(content)) {
      return
    }
    setError(null)
    setSubmitting(true)
    try {
      const request: UpdateItemRequest = { key, description, content: JSON.parse(content) }
      await api.put(basePath, request, item.eTag)
      await load()
      onDone()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDelete() {
    if (!item) {
      return
    }
    setSubmitting(true)
    try {
      await api.delete(basePath, item.eTag)
      await load()
      onDone()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleRestore() {
    if (!item) {
      return
    }
    setSubmitting(true)
    try {
      await api.post(`${basePath}/restore`, undefined, item.eTag)
      await load()
      onDone()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  if (!item) {
    return <ProblemAlert error={error} />
  }

  return (
    <div>
      <form className="card" onSubmit={handleSave}>
        <div className="page-header">
          <h2>{item.key}</h2>
          <span className="badge">
            {item.itemType} / {item.dataType}
          </span>
        </div>
        <ProblemAlert error={error} />
        <div className="field">
          <label>Key</label>
          <input value={key} onChange={(e) => setKey(e.target.value)} required />
        </div>
        <div className="field">
          <label>Description</label>
          <input value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <JsonEditor label="Content" value={content} onChange={setContent} />
        <div className="form-actions">
          <button type="submit" disabled={submitting}>
            Save
          </button>
          {item.isDeleted ? (
            <button type="button" onClick={handleRestore} disabled={submitting}>
              Restore
            </button>
          ) : (
            <button type="button" className="danger" onClick={handleDelete} disabled={submitting}>
              Delete
            </button>
          )}
        </div>
      </form>
      <ItemLinesEditor itemKey={itemKey} basePath={basePath} />
    </div>
  )
}

function ItemLinesEditor({ basePath }: { itemKey: string; basePath: string }) {
  const [lines, setLines] = useState<LineResponse[] | null>(null)
  const [drafts, setDrafts] = useState<LineRequest[]>([])
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  async function load() {
    try {
      const loaded = await api.get<LineResponse[]>(`${basePath}/lines`)
      setLines(loaded)
      setDrafts(
        loaded.map((l) => ({
          schemaId: l.schemaId,
          inheritSchema: l.inheritSchema,
          validFrom: l.validFrom,
          validTo: l.validTo,
          remarks: l.remarks,
          content: l.content,
        })),
      )
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [basePath])

  function updateDraft(index: number, patch: Partial<LineRequest> & { contentText?: string }) {
    setDrafts((prev) =>
      prev.map((d, i) => {
        if (i !== index) {
          return d
        }
        const { contentText, ...rest } = patch
        return { ...d, ...rest, content: contentText !== undefined ? tryParse(contentText, d.content) : d.content }
      }),
    )
  }

  function contentTextFor(index: number): string {
    return toJsonText(drafts[index]?.content)
  }

  function addLine() {
    setDrafts((prev) => [...prev, { inheritSchema: true, remarks: '', content: {} }])
  }

  function removeLine(index: number) {
    setDrafts((prev) => prev.filter((_, i) => i !== index))
  }

  async function handleSaveLines() {
    setError(null)
    setSubmitting(true)
    try {
      await api.put(`${basePath}/lines`, { lines: drafts })
      await load()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  if (lines === null) {
    return null
  }

  return (
    <div className="card">
      <div className="page-header">
        <h3>Lines</h3>
        <div>
          <button type="button" onClick={addLine}>
            Add line
          </button>
          <button type="button" onClick={handleSaveLines} disabled={submitting}>
            Save lines
          </button>
        </div>
      </div>
      <ProblemAlert error={error} />
      {drafts.map((draft, index) => (
        <div className="card nested" key={index}>
          <div className="field-row">
            <div className="field">
              <label>Valid from</label>
              <input
                type="date"
                value={draft.validFrom ?? ''}
                onChange={(e) => updateDraft(index, { validFrom: e.target.value || null })}
              />
            </div>
            <div className="field">
              <label>Valid to</label>
              <input
                type="date"
                value={draft.validTo ?? ''}
                onChange={(e) => updateDraft(index, { validTo: e.target.value || null })}
              />
            </div>
            <div className="field">
              <label>Remarks</label>
              <input value={draft.remarks} onChange={(e) => updateDraft(index, { remarks: e.target.value })} />
            </div>
          </div>
          <JsonEditor
            label="Content"
            value={contentTextFor(index)}
            onChange={(text) => updateDraft(index, { contentText: text })}
            rows={4}
          />
          <button type="button" className="danger" onClick={() => removeLine(index)}>
            Remove
          </button>
        </div>
      ))}
      {drafts.length === 0 && <div className="empty-hint">No lines.</div>}
    </div>
  )
}

function tryParse(text: string, fallback: unknown): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return fallback
  }
}
