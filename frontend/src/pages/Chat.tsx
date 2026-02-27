import { FormEvent, KeyboardEvent, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import MissingFieldsPrompt from '../components/MissingFieldsPrompt'
import OrderPreviewCard from '../components/OrderPreviewCard'
import { executeAction, postChat, toReadableError } from '../lib/api'
import type { ChatMessage, ModelJson } from '../types/model'

const initialMessages: ChatMessage[] = [
  {
    role: 'assistant',
    content:
      'Describe what to create in CRM or SOP/POP and I will prepare the action payload.',
  },
]

function summarizeModel(modelJson: ModelJson): ChatMessage {
  if (modelJson.missing_fields.length > 0) {
    return {
      role: 'assistant',
      content: `I can continue once these fields are provided: ${modelJson.missing_fields.join(
        ', ',
      )}.`,
    }
  }

  return {
    role: 'assistant',
    content: `Prepared ${modelJson.action} for ${modelJson.entity}. Review and confirm.`,
  }
}

function ChatPanel() {
  const navigate = useNavigate()

  const [messages, setMessages] = useState<ChatMessage[]>(initialMessages)
  const [input, setInput] = useState('')
  const [pending, setPending] = useState<ModelJson | null>(null)
  const [isSending, setIsSending] = useState(false)
  const [isExecuting, setIsExecuting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const send = async () => {
    const trimmed = input.trim()
    if (!trimmed || isSending) {
      return
    }

    const userMessage: ChatMessage = {
      role: 'user',
      content: trimmed,
    }

    const nextHistory = [...messages, userMessage]
    setMessages(nextHistory)
    setInput('')
    setError(null)
    setIsSending(true)

    try {
      const response = await postChat({
        message: trimmed,
        messages: nextHistory,
      })

      setPending(response.model_json)

      const assistantMessages =
        response.ui_messages && response.ui_messages.length > 0
          ? response.ui_messages
          : [summarizeModel(response.model_json)]
      setMessages((prev) => [...prev, ...assistantMessages])
    } catch (sendError) {
      setError(toReadableError(sendError))
    } finally {
      setIsSending(false)
    }
  }

  const handleSendSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void send()
  }

  const handleInputKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
      event.preventDefault()
      void send()
    }
  }

  const handleMissingFieldsSubmit = (values: Record<string, string>) => {
    setPending((current) => {
      if (!current) {
        return current
      }

      return {
        ...current,
        fields: {
          ...current.fields,
          ...values,
        },
        missing_fields: [],
      }
    })

    setMessages((prev) => [
      ...prev,
      {
        role: 'assistant',
        content: 'Thanks. Missing fields captured and merged into the draft action.',
      },
    ])
  }

  const handleConfirm = async () => {
    if (!pending || isExecuting) {
      return
    }

    setError(null)
    setIsExecuting(true)

    try {
      const result = await executeAction(pending)
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: `${result.entity} created: ${result.id}`,
        },
      ])
      setPending(null)
      navigate(`/record/${result.entity}/${result.id}`)
    } catch (confirmError) {
      setError(toReadableError(confirmError))
    } finally {
      setIsExecuting(false)
    }
  }

  const handleCancel = () => {
    setPending(null)
    setMessages((prev) => [
      ...prev,
      {
        role: 'assistant',
        content: 'Action cancelled. Nothing was sent to execution.',
      },
    ])
  }

  const hasMissingFields = Boolean(
    pending && pending.missing_fields && pending.missing_fields.length > 0,
  )
  const showConfirmationButtons = pending?.meta.user_confirmation_required === true

  return (
    <section className="chat-panel">
      <div className="chat-scroll">
        <h2 className="panel-title">Assistant Chat</h2>
        {messages.map((message, index) => (
          <article
            key={`${message.role}-${index}`}
            className={`message-bubble message-${message.role}`}
          >
            <div className="message-role">{message.role}</div>
            <div>{message.content}</div>
          </article>
        ))}
        {isSending && <p className="muted">Assistant is thinking…</p>}
      </div>

      {error && <div className="banner banner-error">{error}</div>}

      {pending?.meta.notes && <div className="banner">{pending.meta.notes}</div>}

      {pending && <OrderPreviewCard modelJson={pending} />}

      {pending && hasMissingFields && (
        <MissingFieldsPrompt
          missingFields={pending.missing_fields}
          fields={pending.fields}
          onSubmit={handleMissingFieldsSubmit}
          disabled={isExecuting}
        />
      )}

      {pending && showConfirmationButtons && (
        <div className="confirm-row">
          <button
            type="button"
            className="button"
            onClick={() => {
              void handleConfirm()
            }}
            disabled={isExecuting || hasMissingFields}
          >
            {isExecuting ? 'Confirming…' : 'Confirm'}
          </button>
          <button
            type="button"
            className="button button-secondary"
            onClick={handleCancel}
            disabled={isExecuting}
          >
            Cancel
          </button>
        </div>
      )}

      <form onSubmit={handleSendSubmit} className="composer">
        <label htmlFor="chat-input" className="composer-label">
          Request
        </label>
        <textarea
          id="chat-input"
          className="text-area"
          value={input}
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={handleInputKeyDown}
          placeholder="Create a sales order for Acme with 2 x A100 pumps."
          rows={4}
        />
        <div className="composer-footer">
          <span className="muted">Ctrl/Cmd + Enter to send</span>
          <button type="submit" className="button" disabled={isSending || isExecuting}>
            Send
          </button>
        </div>
      </form>
    </section>
  )
}

export default ChatPanel
