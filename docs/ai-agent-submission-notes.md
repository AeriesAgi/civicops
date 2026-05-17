# AI Agent Submission Notes

The CivicOps AI Agent Command Centre (`/Home/Agent`) exposes action-triggered backend tasks:
- Analyze latest resident report
- Analyze WhatsApp sandbox report
- Analyze voice-note transcript
- Run live Gemini health test
- Generate citizen response
- Generate department brief
- Recommend area alert
- Generate judge summary

Each button calls the backend once and displays validation, category, department, priority, routing reason, citizen response, alert recommendation, source model/fallback and audit notes.

Gemini diagnostics include enabled/key-present status, primary/routine/fallback models, calls since app start, last action, last model, last result, quota-limited state and fallback-active state. Keys and prompt secrets are not exposed.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.
