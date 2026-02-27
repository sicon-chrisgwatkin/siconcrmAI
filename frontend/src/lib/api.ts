import axios from 'axios'
import { z } from 'zod'

import { mockChat, mockExecute, mockRecord, type MockChatPayload } from './mocks'
import {
  chatMessageSchema,
  entitySchema,
  modelJsonSchema,
  type ChatMessage,
  type Entity,
  type ModelJson,
} from '../types/model'

const rawBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').trim()
export const apiBaseUrl = rawBaseUrl === '' ? '/' : rawBaseUrl

const envMockMode = (import.meta.env.MOCK_MODE ?? '').toLowerCase() === 'true'
export const mockModeEnabled = rawBaseUrl === '' || envMockMode

const api = axios.create({
  baseURL: apiBaseUrl,
  timeout: 15_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

const chatResponseSchema = z
  .object({
    model_json: modelJsonSchema,
    ui_messages: z.array(chatMessageSchema).optional(),
  })
  .strict()

const executeResponseSchema = z
  .object({
    status: z.literal('created'),
    entity: entitySchema,
    id: z.string().min(1),
  })
  .strict()

const recordResponseSchema = z.record(z.string(), z.unknown())

export interface ChatRequest {
  message: string
  messages?: ChatMessage[]
}

export interface ChatResponse {
  model_json: ModelJson
  ui_messages?: ChatMessage[] | undefined
}

export interface ExecuteActionResponse {
  status: 'created'
  entity: Entity
  id: string
}

export class ApiValidationError extends Error {
  constructor(endpoint: string, details: string) {
    super(`Invalid response from ${endpoint}: ${details}`)
    this.name = 'ApiValidationError'
  }
}

function formatZodIssues(error: z.ZodError): string {
  return error.issues
    .slice(0, 4)
    .map((issue) => {
      const path = issue.path.length > 0 ? issue.path.join('.') : 'root'
      return `${path}: ${issue.message}`
    })
    .join('; ')
}

function parseOrThrow<T>(
  endpoint: string,
  schema: z.ZodType<T>,
  payload: unknown,
): T {
  const result = schema.safeParse(payload)

  if (!result.success) {
    throw new ApiValidationError(endpoint, formatZodIssues(result.error))
  }

  return result.data
}

function normalizeError(error: unknown): never {
  if (error instanceof ApiValidationError) {
    throw error
  }

  if (axios.isAxiosError(error)) {
    const details = error.response?.data
      ? JSON.stringify(error.response.data)
      : error.message
    throw new Error(`API request failed: ${details}`)
  }

  if (error instanceof Error) {
    throw error
  }

  throw new Error('Unexpected API error')
}

export async function postChat(payload: ChatRequest): Promise<ChatResponse> {
  try {
    if (mockModeEnabled) {
      const data = await mockChat(payload as MockChatPayload)
      return parseOrThrow('/api/chat', chatResponseSchema, data)
    }

    const response = await api.post('/api/chat', payload)
    return parseOrThrow('/api/chat', chatResponseSchema, response.data)
  } catch (error) {
    return normalizeError(error)
  }
}

export async function executeAction(
  modelJson: ModelJson,
): Promise<ExecuteActionResponse> {
  try {
    if (mockModeEnabled) {
      const data = await mockExecute(modelJson)
      return parseOrThrow('/api/actions/execute', executeResponseSchema, data)
    }

    const response = await api.post('/api/actions/execute', modelJson)
    return parseOrThrow('/api/actions/execute', executeResponseSchema, response.data)
  } catch (error) {
    return normalizeError(error)
  }
}

export async function getRecord(
  entity: string,
  id: string,
): Promise<Record<string, unknown>> {
  try {
    if (mockModeEnabled) {
      const data = await mockRecord(entity, id)
      return parseOrThrow('/api/records/:entity/:id', recordResponseSchema, data)
    }

    const response = await api.get(
      `/api/records/${encodeURIComponent(entity)}/${encodeURIComponent(id)}`,
    )
    return parseOrThrow('/api/records/:entity/:id', recordResponseSchema, response.data)
  } catch (error) {
    return normalizeError(error)
  }
}

export function toReadableError(error: unknown): string {
  if (error instanceof Error) {
    return error.message
  }

  return 'Something went wrong. Please try again.'
}
