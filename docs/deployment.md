# Deploying CivicOps Command

The app is a single ASP.NET Core (.NET 10) container. It binds to `$PORT`, exposes
`/healthz`, and runs Band in **Simulation** mode by default — so the hosted demo
(the full five-agent dispatch + live Band Room Viewer) works with **zero secrets**.
Going **Live** (mirroring the transcript into a real Band workspace) is purely
additive and described at the end.

## What judges see on a fresh deploy

- `/` — the CivicOps Command product
- `/Band` — the five-agent dispatch console; launch a scenario and watch
  IncidentIntake → (DispatchCoordinator ‖ ResourceLogistics) → human confirm →
  PublicInfo + ResponseMonitor → escalation → resolve, all in one Band room
- `/healthz` — `{ "status": "ok", "bandMode": "Simulation", "agents": 5 }`

## Option A — Render (one click)

The repo ships a [`render.yaml`](../render.yaml) blueprint.

1. Push this repo to GitHub.
2. In Render: **New → Blueprint**, pick the repo. Render reads `render.yaml`,
   builds the Dockerfile, and deploys a free web service with a health check on
   `/healthz`.
3. Open the assigned `https://<name>.onrender.com` URL.

## Option B — Docker (any host: Railway, Fly.io, Azure, Cloud Run, a VM)

```bash
docker build -t civicops-command .
docker run -p 8080:8080 -e PORT=8080 civicops-command
# → http://localhost:8080
```

- **Railway:** New Project → Deploy from repo → it detects the Dockerfile. Railway
  injects `PORT` automatically.
- **Fly.io:** `fly launch` (uses the Dockerfile); set the internal port to `8080`.
- **Azure / Cloud Run:** push the image to a registry and deploy; both inject
  `PORT`. The app already honours forwarded headers for the TLS proxy.

## Option C — Run from the SDK (no container)

```bash
dotnet publish CivicOps.csproj -c Release -o ./publish
PORT=8080 ASPNETCORE_ENVIRONMENT=Production dotnet ./publish/CivicOps.dll
```

## Configuration

| Setting | Env var | Default | Notes |
|---|---|---|---|
| Port | `PORT` | 8080 (container) | Bound automatically on PaaS |
| Environment | `ASPNETCORE_ENVIRONMENT` | `Production` | |
| Band mode | `Band__Mode` | `Simulation` | `Live` mirrors to a real Band workspace |
| Bridge URL | `Band__BridgeUrl` | `http://localhost:8787` | The band-bridge sidecar (Live only) |
| Gemini | `Gemini__Enabled` / `GEMINI_API_KEY` | off | Optional; deterministic fallback otherwise |

> Note the **double underscore** (`Band__Mode`) for nested config in env vars.
> Never commit real keys — set them in the platform's secret store.

## Going Live with real Band (uses your Band budget)

Live mode mirrors every one of the five agents' messages into a real Band room
via the official SDK (`@band-ai/sdk`). The in-process broker stays the source of
truth, so Live is additive and never breaks the demo.

**Locally, full stack in one command** ([`docker-compose.yml`](../docker-compose.yml)):

```bash
# one shared Band identity (messages prefixed with each agent's name):
THENVOI_API_KEY=sk_... docker compose up --build

# OR five distinct Band identities — one per agent:
THENVOI_AGENT_KEYS='{"agent.intake":"k1","agent.dispatch":"k2","agent.logistics":"k3","agent.monitor":"k4","agent.publicinfo":"k5"}' \
  docker compose up --build
# → app on http://localhost:8080 (Band__Mode=Live), bridge on :8787
```

**In production:** deploy `band-bridge/` as a second service (its own
[`Dockerfile`](../band-bridge/Dockerfile)), set its `THENVOI_API_KEY` /
`THENVOI_AGENT_KEYS` secrets, then set the app's `Band__Mode=Live` and
`Band__BridgeUrl` to the bridge's address. See [`band-bridge/README.md`](../band-bridge/README.md).

## CI

[`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) builds the app,
boots it, runs the route/API smoke scripts, and asserts a full Band scenario
resolves with **five agents** collaborating — so every push proves the multi-agent
flow still works end to end.
