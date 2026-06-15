# Band of Agents Hackathon Readiness Checklist

This checklist is the final judge-facing pass for positioning CivicOps Command as a strong contender for the Band of Agents Hackathon.

## Winning thesis

CivicOps Command should be presented as a **high-stakes, human-governed emergency dispatch workflow where Band is the shared operational room and audit trail**. The core differentiator is not that the app has agents; it is that five specialised agents and humans coordinate through one Band room per incident.

## What judges should see in the first 90 seconds

1. Open `/Band` and point to the five named agent identities.
2. Launch **Structural fire with people trapped** with auto-confirm disabled.
3. Show that `IncidentIntakeAgent` posts the structured incident into the room.
4. Pause when `DispatchCoordinatorAgent` proposes a unit and waits for a human decision.
5. Point out that `ResourceLogisticsAgent` stages backup in parallel in the same room.
6. Confirm the dispatch as the human dispatcher.
7. Let the incident proceed to public info, SLA escalation, backup commitment and resolution.

## Judge criteria mapping

| Criteria | Proof in the repo/demo |
|---|---|
| Application of Band technology | `IBandTransport`, per-incident rooms, agent identities, history, subscriptions, human decisions and optional live mirror. |
| Presentation | `/Band` console and `/Band/Room/{id}` make the transcript, agent hand-offs and human decision visible. |
| Business value | Emergency dispatch is a regulated workflow with real ROI: faster triage, accountable hand-offs, SLA monitoring and public updates. |
| Originality | Dispatch, logistics, monitoring and public-information agents collaborate in parallel through the same incident room. |

## Demo hardening checklist

- Run `dotnet restore` and `dotnet build CivicOps.csproj` on a machine with .NET 10 installed.
- Run `./scripts/smoke-test.sh http://localhost:5000` after starting the app.
- Run `./scripts/api-check.sh http://localhost:5000` after starting the app.
- Run `./scripts/band-demo.sh structure-fire` and confirm it reports five agents, a summary and a closed room.
- Keep Band in Simulation mode for the judged hosted demo unless live Band credentials are already tested.
- If using Live mode, start `band-bridge/` with `THENVOI_API_KEY` and distinct `THENVOI_AGENT_KEYS` before the app.

## Presentation cautions

- Do not imply CivicOps replaces emergency services or has an official municipal deployment.
- Say the default demo uses synthetic eThekwini-style data and a deterministic fallback classifier for reliability.
- Emphasize that irreversible dispatch/public-broadcast actions remain human-confirmed.
- If the hosted demo URL is not finalized, say the repo is one-click deployable via `render.yaml` and `/healthz`.

## Suggested closing line

> “The room is the incident: every agent action, human decision, SLA warning and citizen update is coordinated through Band and replayable as an audit trail.”
