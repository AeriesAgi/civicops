# WhatsApp Connector Readiness

WhatsApp Cloud API is connector-ready for sandbox/live-test and future production pilots. CivicOps does not depend on WhatsApp; residents can report, track and receive alerts through the mobile/PWA app and web portal immediately.

Production WhatsApp use requires:
- WhatsApp Business setup
- Verified Meta app and phone number
- Approved templates where required
- Resident opt-in/consent
- Billing and compliance review

CivicOps keeps:
- `GET /webhooks/whatsapp` verify endpoint
- `POST /webhooks/whatsapp` parser
- `/Demo/WhatsAppSimulator` sandbox simulator
- Outbound sending gated by environment configuration
- Masked phone numbers in UI/log-style outputs

Do not commit WhatsApp tokens, phone numbers or credentials.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.

## Final connector wording

Use this wording in judging: “Optional connector-ready WhatsApp Cloud API integration for future pilots/live-test messaging.” Do not describe WhatsApp as the main demo path, a production-approved channel, or a dependency for reporting.
