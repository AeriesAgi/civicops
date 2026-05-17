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

Gemini must not run on app startup, page load, dashboard load, connector page load, weather/alerts page load, Citizen App opening, background timers or smoke tests. It may run only when a report is submitted, a transcript/WhatsApp inbound report is processed, or staff/judges explicitly click an AI Agent action.

If a model is unsupported, quota-limited or fails once, CivicOps skips safely and falls back without retry loops. Deterministic fallback remains available for judging and offline use.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.
