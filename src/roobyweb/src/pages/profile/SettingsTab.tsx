import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { ProfileResponse, RotateApiKeyResponse, TestGate, UpdateProfileRequest } from '../../api/types'
import { TEST_GATES } from '../../api/types'
import { ProblemAlert } from '../../components/ProblemAlert'

export function SettingsTab() {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [entity, setEntity] = useState<ProfileResponse | null>(null)
  const [name, setName] = useState('')
  const [timeZone, setTimeZone] = useState('')
  const [publishUri, setPublishUri] = useState('')
  const [publishSecret, setPublishSecret] = useState('')
  const [testGate, setTestGate] = useState<TestGate>('None')
  const [isDisabled, setIsDisabled] = useState(false)
  const [remark, setRemark] = useState('')
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)
  const [rotatedKey, setRotatedKey] = useState<string | null>(null)

  const basePath = `/projects/${project}/profiles/${profile}`

  async function load() {
    setError(null)
    try {
      const loaded = await api.get<ProfileResponse>(basePath)
      setEntity(loaded)
      setName(loaded.name)
      setTimeZone(loaded.timeZone)
      setPublishUri(loaded.publishUri ?? '')
      setTestGate(loaded.testGate)
      setIsDisabled(loaded.isDisabled)
      setRemark(loaded.remark)
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project, profile])

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    if (!entity) {
      return
    }
    setError(null)
    setSubmitting(true)
    try {
      const request: UpdateProfileRequest = {
        name,
        timeZone,
        publishUri: publishUri || undefined,
        publishSecret: publishSecret || undefined,
        testGate,
        isDisabled,
        remark,
      }
      await api.patch(basePath, request, entity.eTag)
      setPublishSecret('')
      await load()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleRotateKey() {
    if (!entity) {
      return
    }
    setSubmitting(true)
    try {
      const result = await api.post<RotateApiKeyResponse>(`${basePath}/apikey`, undefined, entity.eTag)
      setRotatedKey(result.apiKey)
      await load()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  if (!entity) {
    return <ProblemAlert error={error} />
  }

  return (
    <div>
      <form className="card" onSubmit={handleSave}>
        <h2>Profile settings</h2>
        <ProblemAlert error={error} />
        <div className="field">
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} required />
        </div>
        <div className="field">
          <label>Time zone (IANA id)</label>
          <input value={timeZone} onChange={(e) => setTimeZone(e.target.value)} required />
        </div>
        <div className="field">
          <label>Publish URI</label>
          <input value={publishUri} onChange={(e) => setPublishUri(e.target.value)} />
        </div>
        <div className="field">
          <label>Publish secret {entity.hasPublishSecret && '(set — leave blank to keep)'}</label>
          <input
            type="password"
            value={publishSecret}
            onChange={(e) => setPublishSecret(e.target.value)}
            placeholder={entity.hasPublishSecret ? '••••••••' : ''}
          />
        </div>
        <div className="field">
          <label>Test gate</label>
          <select value={testGate} onChange={(e) => setTestGate(e.target.value as TestGate)}>
            {TEST_GATES.map((g) => (
              <option key={g} value={g}>
                {g}
              </option>
            ))}
          </select>
        </div>
        <div className="field checkbox-field">
          <label>
            <input type="checkbox" checked={isDisabled} onChange={(e) => setIsDisabled(e.target.checked)} />
            Disabled
          </label>
        </div>
        <div className="field">
          <label>Remark</label>
          <input value={remark} onChange={(e) => setRemark(e.target.value)} />
        </div>
        <div className="form-actions">
          <button type="submit" disabled={submitting}>
            Save
          </button>
        </div>
      </form>
      <div className="card">
        <h3>Delivery API key</h3>
        <p>Rotating generates a new key and immediately invalidates the previous one.</p>
        <button type="button" onClick={handleRotateKey} disabled={submitting}>
          Rotate API key
        </button>
        {rotatedKey && (
          <div className="alert alert-info">
            <strong>New API key (shown once):</strong>
            <pre>{rotatedKey}</pre>
          </div>
        )}
      </div>
    </div>
  )
}
