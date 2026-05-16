# WhatsApp Cloud API Setup

CivicOps supports both a safe WhatsApp demo simulator and real WhatsApp Cloud API webhook/send readiness. Sandbox mode works without Meta credentials. Live send is only attempted when explicitly enabled and all send credentials are present.

## Environment variables

Configure these on the server:

```bash
WHATSAPP_ENABLED=true
WHATSAPP_DEMO_MODE=false
WHATSAPP_VERIFY_TOKEN=choose_a_private_verify_token
WHATSAPP_ACCESS_TOKEN = meta_access_token
WHATSAPP_PHONE_NUMBER_ID=meta_phone_number_id
WHATSAPP_GRAPH_VERSION=v22.0
WHATSAPP_PUBLIC_BASE_URL=https://your-public-host.example
```

Safe demo defaults in `appsettings.json`:

- `WHATSAPP_ENABLED=false`
- `WHATSAPP_DEMO_MODE=true`
- `WHATSAPP_GRAPH_VERSION=v22.0`

## Webhook verification

Meta should call:

```text
GET /webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=...&hub.challenge=...
```

CivicOps returns the `hub.challenge` only when `hub.mode=subscribe` and the supplied verify token exactly matches `WHATSAPP_VERIFY_TOKEN`. There is no hardcoded fallback verify token.

## Inbound messages

Meta posts WhatsApp Cloud API payloads to:

```text
POST /webhooks/whatsapp
```

CivicOps parses inbound text messages from the standard `entry[].changes[].value.messages[]` structure and sends each report through the same CivicOps intake pipeline as web reports, demo WhatsApp, voice-note transcripts, and mobile/API reports.

Each processed message creates:

- reference number
- validation result
- department/authority routing
- priority
- citizen response
- audit entry/internal note
- alert recommendation metadata

## Outbound replies

Outbound WhatsApp send happens only when all are true:

- `WHATSAPP_ENABLED=true`
- `WHATSAPP_DEMO_MODE=false`
- `WHATSAPP_ACCESS_TOKEN` is configured
- `WHATSAPP_PHONE_NUMBER_ID` is configured

If those conditions are not met, CivicOps logs a masked phone number and skips the live send safely.

## Privacy and safety

- Do not commit access tokens, verify tokens, phone-number IDs, private phone numbers, or `.env` files.
- CivicOps masks WhatsApp sender numbers in connector metadata and logs.
- CivicOps is a civic intake and routing demo; it is not an emergency service replacement.

## Demo simulator

The judge/demo path remains available without Meta credentials:

```text
/Demo/WhatsAppSimulator
```

## Submission note

IBM Bob built and accelerated the main hackathon implementation. This final Codex cleanup completed WhatsApp Cloud API readiness and stability. Do not claim live WhatsApp operation unless the deployment has the required Meta app configuration and environment variables.

## Final UI behavior

The WhatsApp connector is shown as:

- **Live Ready / Send Ready** when sending is enabled, sandbox mode is off, and access token plus phone number id are present.
- **Webhook Ready** when verify token and public base URL are present.
- **Sandbox Active** when the local intake console is available but Meta credentials are not configured.

`/Demo/WhatsAppSimulator` is a WhatsApp Intake Sandbox that uses the same intake pipeline as the live webhook. It does not attempt live send from the sandbox console. `/webhooks/whatsapp` remains the Meta callback path for verification and inbound messages.
