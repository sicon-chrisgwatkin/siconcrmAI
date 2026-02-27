import type { FormEvent } from 'react'

interface MissingFieldsPromptProps {
  missingFields: string[]
  fields: Record<string, unknown>
  disabled?: boolean
  onSubmit: (values: Record<string, string>) => void
}

function MissingFieldsPrompt({
  missingFields,
  fields,
  disabled = false,
  onSubmit,
}: MissingFieldsPromptProps) {
  const stringifyFieldValue = (value: unknown): string => {
    if (value == null) {
      return ''
    }

    if (typeof value === 'string') {
      return value
    }

    if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
      return String(value)
    }

    if (typeof value === 'object') {
      try {
        return JSON.stringify(value)
      } catch {
        return ''
      }
    }

    return ''
  }

  const handleFormSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const formData = new FormData(event.currentTarget)
    const values: Record<string, string> = {}

    for (const fieldName of missingFields) {
      const rawValue = formData.get(fieldName)
      values[fieldName] = typeof rawValue === 'string' ? rawValue.trim() : ''
    }

    onSubmit(values)
  }

  return (
    <section className="card">
      <h3 className="card-title">Missing details</h3>
      <p className="muted">
        Add the required fields so the assistant can continue.
      </p>
      <form onSubmit={handleFormSubmit} className="stack-gap-sm">
        {missingFields.map((fieldName) => (
          <label key={fieldName} className="field">
            <span className="field-label">{fieldName}</span>
            <input
              name={fieldName}
              className="text-input"
              defaultValue={stringifyFieldValue(fields[fieldName])}
              placeholder={`Enter ${fieldName}`}
              required
            />
          </label>
        ))}
        <div className="row-end">
          <button type="submit" className="button" disabled={disabled}>
            Apply details
          </button>
        </div>
      </form>
    </section>
  )
}

export default MissingFieldsPrompt
