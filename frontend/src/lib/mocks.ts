import type { Action, ChatMessage, Entity, ModelJson } from '../types/model'

const MOCK_DELAY_MS = 180

export interface MockChatPayload {
  message: string
  messages?: ChatMessage[]
}

export interface MockChatResponse {
  model_json: ModelJson
  ui_messages?: ChatMessage[]
}

export interface MockExecuteResponse {
  status: 'created'
  entity: Entity
  id: string
}

function wait(durationMs: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, durationMs)
  })
}

function hash(input: string): number {
  let result = 0
  for (let i = 0; i < input.length; i += 1) {
    result = (result * 31 + input.charCodeAt(i)) >>> 0
  }
  return result
}

function classifyIntent(message: string): { action: Action; entity: Entity } {
  const lower = message.toLowerCase()

  if (lower.includes('purchase')) {
    return { action: 'create_purchase_order', entity: 'purchase_order' }
  }

  if (lower.includes('opportunit')) {
    return { action: 'create_opportunity', entity: 'opportunity' }
  }

  if (lower.includes('company')) {
    return { action: 'create_company', entity: 'company' }
  }

  if (lower.includes('person') || lower.includes('contact')) {
    return { action: 'create_person', entity: 'person' }
  }

  if (lower.includes('task') || lower.includes('follow up')) {
    return { action: 'create_task', entity: 'task' }
  }

  if (lower.includes('line') || lower.includes('add item')) {
    return { action: 'add_order_lines', entity: 'order_lines' }
  }

  return { action: 'create_sales_order', entity: 'sales_order' }
}

function baseFieldsByEntity(entity: Entity): Record<string, unknown> {
  switch (entity) {
    case 'sales_order':
      return {
        customer_code: 'CUST-001',
        customer_name: 'Acme Engineering Ltd',
        order_date: '2026-02-27',
        warehouse: 'MAIN',
        currency: 'GBP',
      }
    case 'purchase_order':
      return {
        supplier_code: 'SUP-204',
        supplier_name: 'Northwind Components',
        required_date: '2026-03-05',
        warehouse: 'MAIN',
      }
    case 'opportunity':
      return {
        title: 'Warehouse expansion project',
        company_name: 'Acme Engineering Ltd',
        expected_value: 35000,
        close_date: '2026-03-15',
      }
    case 'company':
      return {
        name: 'Acme Engineering Ltd',
        phone: '+44 161 555 1000',
        email: 'hello@acme.example',
      }
    case 'person':
      return {
        first_name: 'Maya',
        last_name: 'Patel',
        email: 'maya.patel@acme.example',
        company_name: 'Acme Engineering Ltd',
      }
    case 'task':
      return {
        subject: 'Follow up pricing request',
        due_date: '2026-03-01',
        priority: 'normal',
      }
    case 'order_lines':
      return {
        order_id: 'SO-000123',
      }
    case 'none':
      return {}
    default:
      return {}
  }
}

function lineItemsByEntity(entity: Entity): ModelJson['items'] {
  if (entity === 'sales_order' || entity === 'purchase_order' || entity === 'order_lines') {
    return [
      {
        type: 'stock',
        stock_code: 'A100',
        description: 'Hydraulic pump',
        qty: 2,
        unit_price: 125.5,
        uom: 'EA',
        warehouse: 'MAIN',
        tax_code: 'T1',
      },
      {
        type: 'free_text',
        description: 'Installation labour',
        qty: 3,
        unit_price: 65,
        tax_code: 'T1',
      },
      {
        type: 'additional_charge',
        additional_charge_code: 'FREIGHT',
        description: 'Freight charge',
        unit_price: 18,
        tax_code: 'T1',
      },
      {
        type: 'comment',
        description: 'Deliver before 12:00 if possible',
      },
    ]
  }

  return []
}

function missingByEntity(entity: Entity): string[] {
  switch (entity) {
    case 'sales_order':
      return ['customer_code', 'order_date']
    case 'purchase_order':
      return ['supplier_code']
    case 'opportunity':
      return ['close_date']
    case 'company':
      return ['name']
    case 'person':
      return ['email']
    case 'task':
      return ['due_date']
    case 'order_lines':
      return ['order_id']
    default:
      return []
  }
}

function shouldRequireMoreFields(message: string): boolean {
  const lower = message.toLowerCase()
  if (lower.includes('complete') || lower.includes('all fields')) {
    return false
  }

  return hash(message) % 2 === 0
}

export async function mockChat(payload: MockChatPayload): Promise<MockChatResponse> {
  await wait(MOCK_DELAY_MS)

  const { action, entity } = classifyIntent(payload.message)
  const baseFields = baseFieldsByEntity(entity)
  const needsMoreFields = shouldRequireMoreFields(payload.message)
  const missing_fields = needsMoreFields ? missingByEntity(entity) : []
  const fields = Object.fromEntries(
    Object.entries(baseFields).filter(([fieldName]) => !missing_fields.includes(fieldName)),
  )

  const model_json: ModelJson = {
    action,
    entity,
    fields,
    items: lineItemsByEntity(entity),
    missing_fields,
    meta: {
      user_confirmation_required: true,
      notes: needsMoreFields
        ? 'Mock mode: request remaining fields before confirmation.'
        : 'Mock mode: ready to confirm and execute.',
    },
  }

  const ui_messages: ChatMessage[] = [
    {
      role: 'assistant',
      content: needsMoreFields
        ? `I prepared a ${entity.replaceAll('_', ' ')} action and still need: ${missing_fields.join(
            ', ',
          )}.`
        : `I prepared a ${entity.replaceAll('_', ' ')} action. Review and confirm to continue.`,
    },
  ]

  return { model_json, ui_messages }
}

export async function mockExecute(modelJson: ModelJson): Promise<MockExecuteResponse> {
  await wait(MOCK_DELAY_MS)

  return {
    status: 'created',
    entity: modelJson.entity,
    id: 'SO-000123',
  }
}

export async function mockRecord(entity: string, id: string): Promise<Record<string, unknown>> {
  await wait(MOCK_DELAY_MS)

  return {
    id,
    entity,
    status: 'created',
    source: 'mock',
    summary: {
      created_at: '2026-02-27T09:30:00Z',
      created_by: 'ai-assistant',
      notes: 'Replace this DTO with your real Sage/Sicon record mapper.',
    },
    fields: {
      customer_code: 'CUST-001',
      customer_name: 'Acme Engineering Ltd',
      warehouse: 'MAIN',
    },
    items: [
      {
        line_no: 1,
        type: 'stock',
        stock_code: 'A100',
        description: 'Hydraulic pump',
        qty: 2,
        unit_price: 125.5,
      },
      {
        line_no: 2,
        type: 'additional_charge',
        additional_charge_code: 'FREIGHT',
        description: 'Freight charge',
        unit_price: 18,
      },
    ],
  }
}
