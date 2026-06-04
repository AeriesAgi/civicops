# CivicOps Command — Band Bridge (Node sidecar)

This is the **real Band client** for CivicOps Command. It uses the official
`@band-sdk/core` npm SDK to publish the multi-agent dispatch transcript to a
hosted Band workspace.

## How it fits

```
┌─────────────────────────── CivicOps Command (ASP.NET Core) ───────────────────────────┐
│  IncidentIntakeAgent   DispatchCoordinatorAgent   ResponseMonitorAgent                 │
│        │                       │                          │                            │
│        └──────────► LocalBandBroker (in-process Band, source of truth) ◄───────────────┤
│                               │  (Band:Mode=Live)                                       │
│                       BandHttpGateway ──HTTP──►  ┌─────────────────────────────┐        │
└──────────────────────────────────────────────── │  band-bridge (this service) │ ──────►  band.ai
                                                   │  @band-sdk/core             │   (real Band rooms)
                                                   └─────────────────────────────┘
```

The C# agents always coordinate through the in-process broker, so the demo runs
with zero dependencies. When `Band:Mode=Live`, every message is *also* relayed to
this sidecar, which republishes it to the real Band platform via the SDK. The
local broker stays the source of truth — going live is purely additive.

## Run it

```bash
cd band-bridge
npm install                 # installs @band-sdk/core + express
cp .env.example .env        # set BAND_API_KEY
BAND_API_KEY=sk_... npm start
# → [band-bridge] listening on http://localhost:8787 (mode=live, workspace=civicops-command)
```

Then run CivicOps Command with:

```jsonc
// appsettings.json
"Band": { "Mode": "Live", "BridgeUrl": "http://localhost:8787", "Workspace": "civicops-command" }
```

Launch a scenario at `/Band` and the same transcript appears in your Band room.

## Stub mode

If `@band-sdk/core` isn't installed or `BAND_API_KEY` is empty, the bridge runs in
**stub mode**: it logs each message it would publish instead of calling Band. This
lets you exercise the full Live relay path locally without credentials.

## HTTP API

| Method | Path | Body | Purpose |
|---|---|---|---|
| `GET`  | `/health` | — | `{ ok, mode, workspace }` |
| `POST` | `/agents` | `{ id, name, role }` | Register/connect a Band agent identity |
| `POST` | `/rooms/:roomId/messages` | `{ agentId, agentName, role, type, text, handoffTo, data }` | Publish a message to a Band room |

> Note: `@band-sdk/core` method shapes are resolved defensively at runtime
> (`band.room()/joinRoom()`, `room.post()/send()`), so the bridge tolerates SDK
> surface changes and always degrades to stub mode rather than crashing.
