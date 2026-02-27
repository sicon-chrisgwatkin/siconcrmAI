# Sicon CRM AI Frontend

Vite + React + TypeScript frontend for a chat-driven CRM/order assistant.

## Features

- Right-hand hideable assistant panel (persistent across routes)
- Chat flow with Ctrl/Cmd + Enter send shortcut
- Zod validation for `model_json` and all API responses
- Confirm/cancel execution flow for assistant actions
- Mock mode for local development without a backend
- Record viewer route for created entities

## Routes

- `/` - workspace + assistant chat panel
- `/record/:type/:id` - generic record viewer (JSON-first, easy to customize per entity)

## Setup

1. Install dependencies:
   ```bash
   npm install
   ```
2. Copy environment template:
   ```bash
   cp .env.local.example .env.local
   ```
3. Run development server:
   ```bash
   npm run dev
   ```

## Environment

`.env.local.example`:

```bash
VITE_API_BASE_URL=/
MOCK_MODE=true
```

- `VITE_API_BASE_URL`: backend API base URL (defaults to `/` when blank)
- `MOCK_MODE=true`: force deterministic local mocks for all API calls

## Scripts

- `npm run dev` - start Vite dev server
- `npm run lint` - run ESLint
- `npm run build` - type-check and build production bundle

## Backend integration points

- LLM orchestration endpoint: `POST /api/chat`
  - Replace mock generation with your real backend prompt-to-JSON flow
- Action execution endpoint: `POST /api/actions/execute`
  - Plug Sage 200 / Sicon service calls behind this contract
- Record retrieval endpoint: `GET /api/records/:entity/:id`
  - Map backend DTOs to entity-specific UI renderers over time
