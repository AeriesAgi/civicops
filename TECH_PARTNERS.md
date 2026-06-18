# CivicOps Technology Partners

## Band

Band is the multi-agent collaboration layer for CivicOps. Each incident opens a shared Band-style room where the Intake, Triage, Dispatch/Routing, Public Status/Comms and Audit/Supervisor agents exchange structured messages. In local fallback mode, the in-process broker provides the same workflow deterministically. In live mode, the optional `band-bridge/` sidecar mirrors the transcript to Band using server-side credentials.

Required env vars for live Band readiness:

- `BAND_API_KEY`
- `BAND_API_BASE_URL`
- `Band__Mode=Live`
- `Band__BridgeUrl=http://localhost:8787`
- `DEMO_MODE=false`

No Band key is logged, rendered to the frontend or committed.

## AI/ML API

AI/ML API is supported as an optional unified model provider for reasoning, summarization, extraction and automation-heavy workflows. CivicOps reads `AIML_API_KEY`, `AIML_API_BASE_URL` and `AIML_MODEL` from the environment. If the key is absent, CivicOps continues with deterministic local processing.

## Featherless AI

Featherless AI is supported as an optional OpenAI-compatible provider for serverless open-source inference. CivicOps uses the base URL `https://api.featherless.ai/v1` and the chat endpoint `/v1/chat/completions` through the same OpenAI-compatible adapter style. It reads `FEATHERLESS_API_KEY` and `FEATHERLESS_MODEL` from the environment.

## Why These Partners Strengthen CivicOps

Band makes the multi-agent workflow visible, auditable and judgeable. AI/ML API adds a unified path for stronger model reasoning when credentials are available. Featherless AI offers an open-source inference path that can fit civic procurement and transparency needs. The deterministic fallback keeps the demo and core workflow working without external keys.
