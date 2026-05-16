# CivicOps Video Script

CivicOps is a mobile-first civic AI platform for pilot-ready reporting, routing and public alerts. A resident reports a burst pipe through the PWA/web portal. CivicOps creates a reference number, validates the report, and uses Gemini when configured—or a local deterministic fallback when not—to classify, prioritize and route the ticket.

The AI Agent Command Centre shows event-triggered actions: analyze resident reports, WhatsApp sandbox messages and voice-note transcripts; generate citizen responses and department briefs; recommend area alerts; and produce a judge summary. Gemini is never called automatically on page load or refresh, protecting quota and keeping fallback active.

The dashboard gives civic teams a control-room view of open incidents, high-priority queues and department workloads. Residents can track status through the public lookup page and view area alerts and weather-risk context.

WhatsApp is connector-ready for sandbox/live-test and future pilots, but CivicOps does not depend on WhatsApp. The main public channel is the installable mobile/PWA app.

IBM Bob helped build and accelerate the hackathon implementation; final engineering polish is documented separately. CivicOps uses synthetic civic data, makes no official municipal partnership claim, and does not replace emergency services.
