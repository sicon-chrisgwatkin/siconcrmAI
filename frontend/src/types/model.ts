import { z } from 'zod'

export const actionSchema = z.enum([
  'create_task',
  'create_opportunity',
  'create_company',
  'create_person',
  'create_sales_order',
  'create_purchase_order',
  'add_order_lines',
  'confirm_order',
  'cancel',
])

export const entitySchema = z.enum([
  'task',
  'opportunity',
  'company',
  'person',
  'sales_order',
  'purchase_order',
  'order_lines',
  'none',
])

export const lineItemTypeSchema = z.enum([
  'stock',
  'free_text',
  'additional_charge',
  'comment',
])

const lineItemBaseSchema = z
  .object({
    type: lineItemTypeSchema,
    stock_code: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
    qty: z.number().optional(),
    unit_price: z.number().optional(),
    uom: z.string().min(1).optional(),
    warehouse: z.string().min(1).optional(),
    additional_charge_code: z.string().min(1).optional(),
    tax_code: z.string().min(1).optional(),
    discount_percent: z.number().min(0).max(100).optional(),
  })
  .strict()

export const lineItemSchema = lineItemBaseSchema.superRefine((item, ctx) => {
  if (item.type === 'stock' && !item.stock_code) {
    ctx.addIssue({
      code: 'custom',
      message: 'stock items require stock_code',
      path: ['stock_code'],
    })
  }

  if (item.type === 'free_text' && !item.description) {
    ctx.addIssue({
      code: 'custom',
      message: 'free_text items require description',
      path: ['description'],
    })
  }

  if (item.type === 'additional_charge' && !item.additional_charge_code) {
    ctx.addIssue({
      code: 'custom',
      message: 'additional_charge items require additional_charge_code',
      path: ['additional_charge_code'],
    })
  }

  if (item.type === 'comment' && !item.description) {
    ctx.addIssue({
      code: 'custom',
      message: 'comment items require description',
      path: ['description'],
    })
  }
})

export const modelJsonSchema = z
  .object({
    action: actionSchema,
    entity: entitySchema,
    fields: z.record(z.string(), z.unknown()),
    items: z.array(lineItemSchema).optional().default([]),
    missing_fields: z.array(z.string()).optional().default([]),
    meta: z
      .object({
        user_confirmation_required: z.boolean(),
        notes: z.string().optional(),
      })
      .optional()
      .default({ user_confirmation_required: false }),
  })
  .strict()

export const messageRoleSchema = z.enum(['user', 'assistant', 'system'])

export const chatMessageSchema = z
  .object({
    role: messageRoleSchema,
    content: z.string(),
  })
  .strict()

export type Action = z.infer<typeof actionSchema>
export type Entity = z.infer<typeof entitySchema>
export type LineItem = z.infer<typeof lineItemSchema>
export type ModelJson = z.infer<typeof modelJsonSchema>
export type ChatMessage = z.infer<typeof chatMessageSchema>
