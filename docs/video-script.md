# CivicOps Video Script

CivicOps is a mobile-first civic AI platform for pilot-ready reporting, routing and public alerts. A resident reports a burst pipe through the PWA/web portal. CivicOps creates a reference number, validates the report, and uses Gemini when configured—or a local deterministic fallback when not—to classify, prioritize and route the ticket.

The AI Agent Command Centre shows event-triggered actions: analyze resident reports, WhatsApp sandbox messages and voice-note transcripts; generate citizen responses and department briefs; recommend area alerts; and produce a judge summary. Gemini is never called automatically on page load or refresh, protecting quota and keeping fallback active.

The dashboard gives civic teams a control-room view of open incidents, high-priority queues and department workloads. Residents can track status through the public lookup page and view area alerts and weather-risk context.

WhatsApp is connector-ready for sandbox/live-test and future pilots, but CivicOps does not depend on WhatsApp. The main public channel is the installable Citizen App / Installable PWA.

IBM Bob helped build and accelerate the hackathon implementation; final engineering polish is documented separately. CivicOps uses synthetic civic data, makes no official municipal partnership claim, and does not replace emergency services.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.

## Final 90-second video flow

1. Resident opens the Citizen App / Installable PWA.
2. Resident submits a messy report with a misspelled area, for example “Chatworth” and a blocked drain description.
3. Gemini/fallback normalizes the area to Chatsworth, estimates the ward or flags uncertainty, classifies the issue and routes it to Roads & Stormwater.
4. A department user signs in and sees only their queue.
5. The public tracks the reference on the status page.
6. Area Alerts and Weather/Area Risk provide public context.
7. Admin dashboard shows eThekwini-wide synthetic workload.
8. Bob Evidence page shows IBM Bob assistance and continuity notes.
9. WhatsApp is shown only as optional connector-ready.


## Final CivicOps submission QA notes

- Citizen App / Installable PWA (`/app`) is the main public channel for reporting, tracking references, My Reports, Area Alerts, Weather/Area Risk, followed suburbs/wards, Gemini Copilot actions and lightweight Community Threads.
- Gemini is the civic AI agent layer and runs server-side only from explicit app/staff/judge actions: report submission, Copilot/AI Agent button click, voice-note transcript analysis, optional WhatsApp sandbox processing, generated citizen response, department brief or alert recommendation.
- Gemini/fallback enrichment cleans messy report text, corrects common area spellings such as Chatworth to Chatsworth and Pheonix to Phoenix, assigns category/department/priority, creates citizen responses and department briefs, and records audit notes.
- Ward values are synthetic estimates for the eThekwini scenario. If a ward cannot be inferred, CivicOps must show a ward estimate or Needs ward confirmation rather than pretending certainty.
- Department responders see only their own queues; Admin and Dispatcher can see broader operational views.
- Community Threads are lightweight local confirmation/update areas, not a full social network.
- The data set is synthetic eThekwini scenario data for hackathon judging. CivicOps does not claim live municipal data, an official municipal partnership, emergency-service replacement, or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging; the Citizen App/PWA is the primary demo path.
- Production requires real identity, municipal integrations, privacy/security hardening, approved communications channels, and authoritative GIS/ward data.
- CivicOps was built with IBM Bob assistance and finalized into a working hackathon submission with verification, packaging and polish.
