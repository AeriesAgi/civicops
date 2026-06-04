# CivicOps Command — Band Multi-Agent Architecture

> Band is the **coordination layer**, not a wrapper. Three specialised AI agents
> run an emergency dispatch from raw report to resolution by communicating
> **through Band** — a shared, per-incident interaction room with a full audit
> trail and a human-in-the-loop confirmation step.

## Why Band

Emergency dispatch is a regulated, high-stakes workflow (Hackathon **Track 3**).
It is exactly the kind of work where multiple specialists must share context,
hand off cleanly, and leave an auditable record — while a human keeps authority
over the irreversible decision (committing a unit). Band gives us all of that as
a first-class substrate: identities, rooms, messages, subscriptions and history.

## The three agents

| Agent | Identity | Reads from Band | Writes to Band |
|---|---|---|---|
| **IncidentIntakeAgent** | `agent.intake` | `RawReport` | `Classified`, `Handoff → dispatch` |
| **DispatchCoordinatorAgent** | `agent.dispatch` | `Classified`, `HumanDecision` | `UnitsQueried`, `AssignmentProposed` (awaits human), `Dispatched`, `Handoff → monitor` |
| **ResponseMonitorAgent** | `agent.monitor` | `Dispatched` | `StatusUpdate`, `SlaWarning`, `Escalation → supervisor`, `CitizenUpdate`, `Resolved`, `Summary` |

Each agent connects to Band **under its own identity** and only ever acts by
posting messages and reacting to the shared stream. No agent calls another agent
directly — the workflow advances purely because messages flow through Band.

## The room IS the incident

One Band room per incident (`incident id = room id`). Every signal about that
incident — the raw citizen report, the AI classification, the unit scoring, the
human's confirmation, the GPS heartbeats, the SLA escalation, the resolution —
lives in that single shared room. That room transcript is the audit trail judges
can replay end to end.

## End-to-end message flow

```
Citizen channel ──RawReport──▶ ┌──────────────── BAND ROOM (incident id) ────────────────┐
                                │                                                          │
IncidentIntakeAgent  ◀─────────┤ reads RawReport                                          │
   classifies (Gemini/fallback)│──Classified──▶ ──Handoff→dispatch──▶                     │
                                │                                                          │
DispatchCoordinatorAgent ◀──────┤ reads Classified                                         │
   scores fleet (ETA+skill+load)│──UnitsQueried──▶ ──AssignmentProposed (awaits human)──▶  │
                                │                                                          │
Human Dispatcher  ─────────────▶│──HumanDecision (confirm / override / reject)──▶          │
                                │                                                          │
DispatchCoordinatorAgent ◀──────┤ reads HumanDecision                                      │
                                │──Dispatched──▶ ──Handoff→monitor──▶                       │
                                │                                                          │
ResponseMonitorAgent ◀──────────┤ reads Dispatched                                         │
   tracks GPS + SLA             │──StatusUpdate──▶ ──SlaWarning──▶ ──Escalation→supervisor─▶│
                                │──CitizenUpdate──▶ ──Resolved──▶ ──Summary──▶ (room closed)│
                                └──────────────────────────────────────────────────────────┘
```

## Code map (running app)

```
Band/
├── IBandTransport.cs          # the Band interaction-layer contract (seam)
├── LocalBandBroker.cs         # in-process Band: identities, rooms, history, pub/sub
├── BandHttpGateway.cs         # optional live mirror to a hosted band.ai workspace
├── BandAgentClient.cs         # C# Band client wrapper bound to one identity (SDK-like)
├── BandAgentService.cs        # facade: start incident, human decision, room reads
├── BandIdentities.cs          # the agent + human identities
├── BandIncidentRoom.cs        # per-incident room helpers
├── Fleet.cs                   # Command response fleet + ETA/skill/workload scoring
├── DispatchMapping.cs         # civic classification → unit type + SLA
├── BandSimulationService.cs   # scripted end-to-end scenarios for the demo
├── BandRealtimeBroadcaster.cs # bridges Band → SignalR for the live viewer
└── Agents/
    ├── BandAgent.cs               # base: connect, subscribe, react off-thread
    ├── IncidentIntakeAgent.cs     # AGENT 1
    ├── DispatchCoordinatorAgent.cs# AGENT 2
    └── ResponseMonitorAgent.cs    # AGENT 3
Hubs/BandHub.cs                # SignalR hub for the Band Room Viewer
Controllers/BandController.cs  # console, room viewer, REST + simulate
Views/Band/Console.cshtml      # launch + fleet + active rooms
Views/Band/Room.cshtml         # live Band Room Viewer + human confirm panel
```

## Simulation vs Live

Band runs in two modes, chosen in `appsettings.json` → `Band:Mode`:

- **Simulation (default):** the `LocalBandBroker` is a faithful in-process model
  of Band. Zero external dependencies — the multi-agent demo always runs, even
  offline, which is exactly what you want for a reliable video and a hosted demo.
- **Live:** set `Band:Mode=Live` and `Band:ApiKey`. The same agents run, and
  `BandHttpGateway` additionally relays every message to a hosted band.ai
  workspace room. The local broker stays the source of truth, so going live is
  purely additive and never breaks the flow.

Because every agent is written against `IBandTransport`, swapping Simulation for
Live changes **no agent code**.

## Human-in-the-loop

`DispatchCoordinatorAgent` never commits a unit on its own. It posts an
`AssignmentProposed` message and sets the room to *awaiting confirmation*. A human
dispatcher sees the full Band context in the Room Viewer and clicks **Confirm**,
**Override** (pick a different unit) or **Reject** — which posts a `HumanDecision`
message back into the room that the agent then acts on. The authority for the
irreversible step stays with the human; Band is where that hand-off happens.
