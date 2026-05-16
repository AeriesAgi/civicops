# Gemini Setup

CivicOps supports Gemini as an event-triggered AI agent layer with deterministic fallback.

Environment/config keys:

```text
GEMINI_ENABLED=true|false
GEMINI_API_KEY: set outside git
GEMINI_MODE=Hybrid
GEMINI_MODEL=gemini-2.5-flash
GEMINI_ROUTINE_MODEL=gemini-3.1-flash-lite
GEMINI_FALLBACK_MODELS=gemini-3.1-flash-lite,gemini-2.5-flash-lite,gemini-2.0-flash-lite,gemini-2.0-flash
GEMINI_AUTO_RUN_AGENT_PAGE=false
GEMINI_MANUAL_TEST_COOLDOWN_SECONDS=60
GEMINI_QUOTA_COOLDOWN_MINUTES=30
```

Gemini must not run on app startup, page load, dashboard load, connector page load, weather/alerts page load, mobile app opening, background timers or smoke tests. It may run only when a report is submitted, a transcript/WhatsApp inbound report is processed, or staff/judges explicitly click an AI Agent action.

If a model is unsupported, quota-limited or fails once, CivicOps skips safely and falls back without retry loops. Deterministic fallback remains available for judging and offline use.
