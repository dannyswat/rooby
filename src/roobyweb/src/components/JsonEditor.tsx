import { useMemo } from 'react'

interface JsonEditorProps {
  label?: string
  value: string
  onChange: (text: string) => void
  rows?: number
  disabled?: boolean
}

export function JsonEditor({ label, value, onChange, rows = 8, disabled }: JsonEditorProps) {
  const error = useMemo(() => {
    if (value.trim() === '') {
      return 'JSON is required'
    }
    try {
      JSON.parse(value)
      return null
    } catch (err) {
      return err instanceof Error ? err.message : 'Invalid JSON'
    }
  }, [value])

  return (
    <div className="field">
      {label && <label>{label}</label>}
      <textarea
        className="json-editor"
        rows={rows}
        value={value}
        disabled={disabled}
        spellCheck={false}
        onChange={(e) => onChange(e.target.value)}
      />
      {error && <div className="field-error">{error}</div>}
    </div>
  )
}

export function isValidJson(text: string): boolean {
  try {
    JSON.parse(text)
    return true
  } catch {
    return false
  }
}

export function toJsonText(value: unknown): string {
  return JSON.stringify(value, null, 2)
}
