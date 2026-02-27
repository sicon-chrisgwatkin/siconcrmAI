# Sage 200 Local LLM Chat Panel (Add-on Scaffold)

This repository contains a C# scaffold for a Sage 200 add-on that provides:

- A right-hand, hideable chat panel (WinForms) with session-persisted state.
- Local-only LLM integrations (Ollama, LM Studio, in-process GGUF runtime adapter).
- Strict JSON envelope parsing/validation for action execution safety.
- Service-layer abstractions for Sicon CRM and Sage 200 SOP/POP actions.
- Clarification question mapping and record navigation helpers.
- Sample unit tests for parser/validator behavior.

## Project Layout

- `src/Sage200.LocalLlmChat.Core` - Domain models, contracts, parser/validator, orchestration.
- `src/Sage200.LocalLlmChat.Infrastructure` - Local LLM and API service implementations.
- `src/Sage200.LocalLlmChat.WinForms` - Right-docked chat panel and host integration.
- `tests/Sage200.LocalLlmChat.Tests` - Sample tests.

## Notes

- The implementation is privacy-first and does not include any cloud connector.
- API-specific Sage 200/Sicon calls are represented behind interfaces and concrete placeholders.
- Order posting/confirmation is guarded by explicit user confirmation flow.
