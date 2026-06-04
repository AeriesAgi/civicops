# Band of Agents Hackathon — Submission

**Title:** CivicOps Command — Band Multi-Agent Emergency Dispatch System

**Track:** Track 3 — Regulated & High-Stakes Workflows

**Tech tags:** Band, ASP.NET Core, AI Agents, Emergency Response, Multi-Agent

---

## Short description

Three specialised AI agents run emergency dispatch end to end — intake, dispatch
coordination, and response monitoring — coordinating **through Band**, with a
human dispatcher confirming every committal inside the shared per-incident Band
room. The room is the incident; the transcript is the audit trail.

---

## Long description

CivicOps Command is a production-grade operational intelligence platform for
emergency response and dispatch (ASP.NET Core, Clean Architecture, real-time
SignalR, AI classification). For the Band of Agents Hackathon we added **Band as
the coordination layer between three specialised agents** that together run a
dispatch from the moment a citizen report arrives to the moment the incident is
resolved.

**Band is the substrate, not a wrapper.** Each agent connects to Band under its
own identity and acts *only* by posting messages to, and reacting to, a shared
per-incident room (`incident id = room id`). No agent calls another directly —
the workflow advances purely because context flows through Band.

- **IncidentIntakeAgent** accepts raw, unstructured reports from any channel
  (citizen app, WhatsApp, call centre, walk-in), uses the CivicOps AI pipeline to
  classify type, severity, affected area and required resource, posts the
  structured incident into the Band room, and hands off to dispatch.
- **DispatchCoordinatorAgent** reads the classified incident from Band, queries
  available units by type and proximity, scores the best match on ETA + skill +
  current workload, and proposes a unit — then **stops and waits for a human
  dispatcher to confirm in the Band room.** The human sees the full Band context
  and clicks Confirm, Override or Reject; that decision is itself a Band message
  the agent then acts on.
- **ResponseMonitorAgent** reads the active assignment from Band, tracks the unit's
  GPS and the SLA timer, posts live status heartbeats, escalates to a supervisor
  through Band if the SLA is at risk, pushes status updates back to the citizen,
  and closes the incident with an audit summary.

Everything is visible in a live **Band Room Viewer**: a real-time, colour-coded
stream of every agent message and hand-off, with the human confirmation panel
inline. Judges can watch three agents and a human collaborate in one shared space,
then replay the entire incident from the room history.

A one-click **simulation mode** runs a serious incident (e.g. a structural fire
with people trapped) through the whole flow for the demo video, including the
human-in-the-loop step and a supervisor escalation.

### Why this matters
Emergency dispatch is a regulated, high-stakes workflow where minutes save lives,
specialists must share context, hand-offs must be clean, and every decision must
be auditable — while a human retains authority over irreversible actions. That is
precisely what Band provides as a coordination layer, and precisely what CivicOps
Command demonstrates.

### Judging alignment
- **Application of technology:** Band is the actual coordination layer — identities,
  rooms, messages, subscriptions, hand-offs and history — with a clean
  `IBandTransport` seam that runs in-process for the demo and mirrors to a hosted
  band.ai workspace in live mode without changing a line of agent code.
- **Presentation:** emergency dispatch is instantly legible; the Band Room Viewer
  makes multi-agent coordination something you can simply *watch*.
- **Business value:** real enterprise workflow — faster, auditable, human-governed
  dispatch on a production platform, not a toy.
- **Originality:** multi-agent emergency dispatch with human-in-the-loop
  confirmation happening *inside* the shared Band room.

---

## Deliverables

| # | Deliverable | Where |
|---|---|---|
| 1 | Working Band integration (3 agents) | `Band/` + `Band/Agents/` |
| 2 | End-to-end demo flow | `/Band` console + `scripts/band-demo.sh` |
| 3 | Clean public GitHub repo | this repository |
| 4 | Hosted demo URL | _add your deployment URL here_ |
| 5 | Submission text | this file |
| 6 | README explaining Band architecture | `README.md` + `docs/band-architecture.md` |

## Demo script (for the video)

1. Open `/Band`. Point out the three agents and the Command fleet.
2. Select **Structural fire with people trapped**, **uncheck auto-confirm**, Launch.
3. Watch **IncidentIntakeAgent** classify and hand off in the Band room.
4. Watch **DispatchCoordinatorAgent** score units and propose one — note it *waits*.
5. As the human dispatcher, click **Confirm** inside the Band room.
6. Watch **ResponseMonitorAgent** track GPS/SLA, escalate to the supervisor,
   update the citizen, resolve, and post the Band room summary.
7. Re-run with auto-confirm for the unattended end-to-end pass.
