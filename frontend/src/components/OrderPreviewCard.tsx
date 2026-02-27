import type { ModelJson } from '../types/model'

interface OrderPreviewCardProps {
  modelJson: ModelJson
}

function formatValue(value: unknown): string {
  if (value == null) {
    return '—'
  }

  if (typeof value === 'number') {
    return Number.isInteger(value) ? String(value) : value.toFixed(2)
  }

  if (typeof value === 'string') {
    return value
  }

  if (typeof value === 'boolean') {
    return value ? 'Yes' : 'No'
  }

  if (typeof value === 'object') {
    try {
      return JSON.stringify(value)
    } catch {
      return '[unserializable]'
    }
  }

  if (typeof value === 'bigint') {
    return value.toString()
  }

  return ''
}

function describeLineItem(item: ModelJson['items'][number]): string {
  switch (item.type) {
    case 'stock': {
      const qty = item.qty == null ? '0' : formatValue(item.qty)
      return `${item.stock_code ?? 'N/A'} • ${qty} @ ${formatValue(item.unit_price)}`
    }
    case 'free_text': {
      const qty = item.qty == null ? '1' : formatValue(item.qty)
      return `${qty} x ${item.description ?? 'Free text'}`
    }
    case 'additional_charge':
      return `${item.additional_charge_code ?? 'CHARGE'} @ ${formatValue(
        item.unit_price,
      )}`
    case 'comment':
      return item.description ?? 'Comment'
    default:
      return 'Line item'
  }
}

function OrderPreviewCard({ modelJson }: OrderPreviewCardProps) {
  const fieldEntries = Object.entries(modelJson.fields)

  return (
    <section className="card">
      <h3 className="card-title">Action preview</h3>
      <div className="preview-meta">
        <span>
          <strong>Action:</strong> {modelJson.action}
        </span>
        <span>
          <strong>Entity:</strong> {modelJson.entity}
        </span>
      </div>

      {fieldEntries.length > 0 ? (
        <dl className="key-value-grid">
          {fieldEntries.map(([key, value]) => (
            <div key={key} className="key-value-row">
              <dt>{key}</dt>
              <dd>{formatValue(value)}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <p className="muted">No header fields were returned for this action.</p>
      )}

      {modelJson.items.length > 0 && (
        <>
          <h4 className="section-title">Line items</h4>
          <ul className="line-items">
            {modelJson.items.map((item, index) => (
              <li key={`${item.type}-${String(index)}`} className="line-item">
                <span className="line-item-type">{item.type}</span>
                <span className="line-item-description">{describeLineItem(item)}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

export default OrderPreviewCard
