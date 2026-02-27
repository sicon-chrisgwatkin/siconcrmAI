import { FormEvent, useEffect, useState } from 'react'

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
  const [answers, setAnswers] = useState<Record<string, string>>({})

  useEffect(() => {
    const initial: Record<string, string> = {}
    for (const field of missingFields) {
      const value = fields[field]
      initial[field] = value == null ? '' : String(value)
    }
    setAnswers(initial)
  }, [fields, missingFields])

  const handleFormSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    onSubmit(answers)
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
              className="text-input"
              value={answers[fieldName] ?? ''}
              onChange={(event) => {
                setAnswers((prev) => ({
                  ...prev,
                  [fieldName]: event.target.value,
                }))
              }}
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
