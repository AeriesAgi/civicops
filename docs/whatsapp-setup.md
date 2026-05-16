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
