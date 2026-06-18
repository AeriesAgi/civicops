# CivicOps Command — Band Bridge (Node sidecar)

This is the **real Band client** for CivicOps Command. It uses the official Band
SDK (`@band-ai/sdk` + `@thenvoi/rest-client`) to publish the five-agent dispatch
transcript into a hosted Band room.

## How it fits

```
┌──────────────────────────── CivicOps Command (ASP.NET Core) ────────────────────────────┐
│  IntakeAgent  DispatchAgent  LogisticsAgent  MonitorAgent  PublicInfoAgent               │
│        │            │              │              │              │                       │
│        └────────────┴───► LocalBandBroker (in-process Band, source of truth) ◄───────────┤
│                               │  (Band:Mode=Live)                                         │
│                       BandHttpGateway ──HTTP──►  ┌─────────────────────────────┐          │
└──────────────────────────────────────────────── │  band-bridge (this service) │ ──────►  Band
                                                   │  @band-ai/sdk               │  (real rooms)
                                                   └─────────────────────────────┘
```

The C# agents always coordinate through the in-process broker, so the demo runs
with zero dependencies. When `Band:Mode=Live`, every message is *also* relayed to
this sidecar, which republishes it to the real Band platform via the SDK. The
local broker stays the source of truth — going live is purely additive.

## Identity model

Two ways to attribute the transcript in Band:

- **Single identity:** set `THENVOI_API_KEY`. That one Band identity creates each
  room and posts every message (each line prefixed with the agent's name).
- **True multi-identity:** set `THENVOI_AGENT_KEYS` — a JSON map of each CivicOps
  agent id to its own Band API key — so all five agents appear as five distinct
  members collaborating in one shared Band room. Any agent without its own key
  falls back to `THENVOI_API_KEY`.

```jsonc
THENVOI_AGENT_KEYS={
  "agent.intake":     "<band-agent-key>",
  "agent.dispatch":   "<band-agent-key>",
  "agent.logistics":  "<band-agent-key>",
  "agent.monitor":    "<band-agent-key>",
  "agent.publicinfo": "<band-agent-key>"
}
```

## Run it

```bash
cd band-bridge
npm install                 # installs @band-ai/sdk + @thenvoi/rest-client + express
cp .env.example .env        # set THENVOI_API_KEY (and/or THENVOI_AGENT_KEYS)
THENVOI_API_KEY=<band-api-key> npm start
# → [band-bridge] Band SDK online → https://app.thenvoi.com (workspace 'civicops-command', ...)
# → [band-bridge] listening on http://localhost:8787 (mode=live, workspace=civicops-command)
```

Then run CivicOps Command with:

```jsonc
// appsettings.json
"Band": { "Mode": "Live", "BridgeUrl": "http://localhost:8787", "Workspace": "civicops-command" }
```

Launch a scenario at `/Band` and the same five-agent transcript appears in your
Band room.

## Stub mode

If no key is set (or the SDK can't load), the bridge runs in **stub mode**: it
logs each message it would publish instead of calling Band. This lets you
exercise the full Live relay path locally without credentials.

## HTTP API

| Method | Path | Body | Purpose |
|---|---|---|---|
| `GET`  | `/health` | — | `{ ok, mode, workspace, restUrl, perAgentIdentities }` |
| `POST` | `/agents` | `{ id, name, role }` | Probe whether an agent posts under its own Band identity |
| `POST` | `/rooms/:roomId/messages` | `{ agentId, agentName, role, type, text, handoffTo, data }` | Publish a message into the room's Band chat |

Each relayed message is posted with `createChatMessage`; the CivicOps message
**kind** and structured payload travel in `metadata` (the platform validates its
own `messageType` enum, so domain kinds live in metadata rather than there).

## Resilience

Every Band call is best-effort and wrapped so a live-relay hiccup (auth, network,
rate limit) is logged and returned as `delivered: "error-soft"` — it never throws
back into the C# workflow. The in-process broker remains the source of truth, so
the demo keeps running no matter what the live platform does.
