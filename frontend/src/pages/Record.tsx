import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'

import { getRecord, toReadableError } from '../lib/api'

function humanize(value: string): string {
  return value
    .replaceAll('_', ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase())
}

function RecordViewer() {
  const { type, id } = useParams<{ type: string; id: string }>()

  const [data, setData] = useState<Record<string, unknown> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const heading = useMemo(() => {
    if (!type || !id) {
      return 'Record'
    }

    return `${humanize(type)} • ${id}`
  }, [id, type])

  useEffect(() => {
    let active = true

    async function load() {
      if (!type || !id) {
        setError('Record route is missing type or id.')
        setLoading(false)
        return
      }

      setLoading(true)
      setError(null)

      try {
        const record = await getRecord(type, id)
        if (active) {
          setData(record)
        }
      } catch (loadError) {
        if (active) {
          setError(toReadableError(loadError))
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => {
      active = false
    }
  }, [id, type])

  return (
    <section className="workspace-panel">
      <h1>{heading}</h1>
      <p className="muted">
        Generic record view. Add entity-specific renderers here later (sales orders,
        opportunities, tasks, etc.).
      </p>

      {error && <div className="banner banner-error">{error}</div>}

      {loading && <p>Loading record…</p>}

      {!loading && !error && data && (
        <div className="card">
          <h3 className="card-title">Raw JSON</h3>
          <pre className="json-view">{JSON.stringify(data, null, 2)}</pre>
        </div>
      )}
    </section>
  )
}

export default RecordViewer
