# CivicOps Judge Route / Demo Script

Use the term **sandbox scenario** or **synthetic civic data** rather than implying a live municipal deployment.

1. Open `/` and explain: “AI-powered civic reporting, routing and public alerts.” Residents report through mobile/PWA/web; Gemini/fallback structures reports; humans remain in control.
2. Open `/Home/Report`, submit a civic issue, and show the reference confirmation.
3. Open `/Home/Mobile` or `/app` to show the PWA citizen hub as the main public channel.
4. Open `/Home/Agent` and run: latest report, WhatsApp sandbox report, voice-note transcript, citizen response, department brief, alert recommendation and judge summary.
5. Open `/Home/Dashboard` to show queues and control-room cards.
6. Open `/Home/Lookup` and track a reference.
7. Open `/Home/Alerts` and `/Home/Weather` for public notices and area context.
8. Open `/Demo/WhatsAppSimulator` as optional connector readiness only.
9. Open `/Home/Connectors` and explain Gemini diagnostics, quota-safe cooldowns and WhatsApp environment gates.
10. Open `/Home/BobEvidence` to show preserved IBM Bob evidence docs.

Safety line: CivicOps does not replace emergency services and does not claim an official municipal partnership.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.
