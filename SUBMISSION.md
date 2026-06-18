# CivicOps Band of Agents Submission

## Project Title

CivicOps Command

## Tagline

Multi-agent civic and emergency operations: from citizen report to routed response, public update and audit trail.

## Problem

Citizen reports arrive through messy text, WhatsApp-style messages and voice-note transcripts. Municipal and emergency teams then have to extract facts, classify urgency, route work, update the public and preserve an audit trail under pressure. That work is often fragmented across people, spreadsheets and disconnected inboxes.

## Solution

CivicOps turns raw citizen reports into structured incidents and coordinates five collaborating agents in a shared Band workflow:

- Intake Agent extracts location, category, urgency, contact and description.
- Triage Agent classifies severity, response type and missing information.
- Dispatch/Routing Agent assigns workflow, response priority, unit and escalation path.
- Public Status/Comms Agent prepares citizen-friendly WhatsApp/public updates.
- Audit/Supervisor Agent records key decisions, SLA risk and final supervisor summary.

The demo shows the full chain for a seeded water-main incident: voice-note transcript -> Band room -> triage -> dispatch proposal -> human confirmation -> public update -> monitoring -> resolution -> audit summary.

## Why Now

Cities face more climate, infrastructure and safety disruptions while residents expect real-time communication. Multi-agent systems are now strong enough to divide complex operational work into specialized lanes, but high-stakes response still needs human checkpoints and auditable records. Band gives CivicOps a collaboration layer where those agents can work together visibly.

## Target Users

- Municipal operations centers.
- Ward offices and public works teams.
- Utilities and infrastructure response teams.
- Emergency management and disaster coordination teams.
- NGOs or civic response groups that triage community reports.

## How Band Is Used

Band is the shared interaction layer for the incident. CivicOps runs a local in-process Band-style broker by default so judges can test the complete workflow without external credentials. When live credentials and the Node `band-bridge/` sidecar are configured, the same transcript can be mirrored to a hosted Band room.

Secrets are read from environment variables only. The app reports only configured/not-configured flags and never renders API keys to the frontend or logs.

## Agent Workflow

1. A citizen report enters through the seeded demo, web report, WhatsApp-style simulator or voice-note transcript simulator.
2. The Intake Agent posts the raw report and extracted fields to the incident room.
3. The Triage Agent classifies category, urgency, severity and missing information.
4. The Dispatch/Routing Agent recommends the operational workflow and unit, then waits for human confirmation.
5. The Public Status/Comms Agent writes a clear resident-facing update.
6. The Audit/Supervisor Agent records decisions, status changes, escalation and the final summary.

## Technology Partners

- **Band:** multi-agent room, handoff and evidence layer; optional live mirror through `band-bridge/`.
- **AI/ML API:** optional unified model access for extraction, reasoning and summarization when `AIML_API_KEY` is present.
- **Featherless AI:** optional OpenAI-compatible serverless open-source inference using `https://api.featherless.ai/v1`.
- **Gemini:** existing optional enrichment layer; deterministic fallback remains the default.

## Architecture

- ASP.NET Core MVC app.
- SignalR live Band room viewer.
- `Band/` local broker, agent identities, room models, simulation service and optional HTTP gateway.
- `band-bridge/` Node sidecar for optional live Band relay.
- Optional OpenAI-compatible provider adapter for AI/ML API and Featherless.
- JSON-backed demo data and deterministic classification fallback.
- Docker-ready app with `/healthz` and `/api/integrations/status`.

## Impact

CivicOps can reduce dispatcher overload, speed up routing, make resident communication clearer and create a replayable evidence trail. The pattern applies to water leaks, road hazards, power outages, fire risk, flooding and public safety reports.

## Demo Instructions

1. Run `dotnet run --urls http://localhost:5000`.
2. Open `http://localhost:5000/demo/band`.
3. Select `Burst water main threatening homes`.
4. Keep auto-confirm checked for the recording, or uncheck it to perform the human dispatcher decision manually.
5. Click `Launch in Band`.
6. Watch the Band room reach dispatch, public update, monitoring, resolution and supervisor summary.
7. Open `/api/integrations/status` to verify fallback/live readiness without exposing secrets.

## Deployment Instructions

```bash
dotnet restore
dotnet build
dotnet run --urls http://localhost:5000
```

Container:

```bash
docker build -t civicops-command .
docker run -p 8080:8080 -e PORT=8080 -e DEMO_MODE=true civicops-command
```

Live Band mirror requires the `band-bridge/` sidecar and environment variables listed below.

## Environment Variables

- `BAND_API_KEY`
- `BAND_API_BASE_URL`
- `AIML_API_KEY`
- `AIML_API_BASE_URL`
- `AIML_MODEL`
- `FEATHERLESS_API_KEY`
- `FEATHERLESS_MODEL`
- `DEMO_MODE`
- `Band__Mode`
- `Band__BridgeUrl`
- `Band__Workspace`
- `Band__TickSeconds`
- Existing optional Gemini and WhatsApp variables are documented in `.env.example`.

## What Is Working

- Full local deterministic multi-agent Band demo.
- `/demo/band` and `/Band` console.
- Live Band room viewer with real-time SignalR updates.
- Seeded water-main scenario showing intake, triage, dispatch, public update and audit summary.
- Human-in-the-loop dispatch confirmation.
- Optional provider status endpoint.
- Safe `.env.example`.
- Submission docs, pitch outline and evidence checklist.

## What Is Simulated / Fallback

- Without Band credentials, the app uses the local Band broker and does not claim live Band delivery.
- Without AI/ML API, Featherless or Gemini keys, extraction/classification uses deterministic local fallback.
- WhatsApp and voice-note flows are demo/simulator-ready unless production messaging and transcription credentials are supplied.
- No production municipal partnership, emergency authority or live deployment is claimed in this repository.

## Future Roadmap

- Live Band room validation with official hackathon credentials.
- Production identity and access control.
- Approved WhatsApp templates and real call-center ingestion.
- GIS/ward boundary integrations.
- Fleet CAD, work-order and utility outage integrations.
- Post-incident analytics and model evaluation.

## Contact

info@culltron.app
