Build a complete application from scratch called CivicOps.

You are working inside a fresh empty GitHub repository / Codespaces environment.

Important naming and branding:
- The public product name is CivicOps.
- The user-facing app must be branded CivicOps / Culltron CivicOps only.
- Do not brand the app as an IBM Bob app.
- IBM Bob is the development tool used to build the project, and that must be captured only in documentation/report files for the IBM Bob hackathon.
- The app may also be reused for an AI Agent hackathon, so keep the product branding clean, professional, and reusable.

Mission:
CivicOps is an integration-ready civic operations platform for municipal incident intake, routing, public tracking, responder workflows, mobile access, WhatsApp intake, voice-note readiness, and area-based alerts.

It helps residents report civic issues through:
1. Web/PWA
2. Android mobile app
3. WhatsApp text/voice-note-ready intake

The system structures those reports into incidents, uses Gemini where configured to assist with extraction/classification/routing/triage, falls back to deterministic routing when Gemini is unavailable, routes incidents to the correct department, gives residents a reference number, supports public status lookup, gives departments queues, and supports suburb/ward/area-based public alerts.

Build goal:
Create a serious hackathon-ready CivicOps application from scratch in this repository. It must be demo-ready, mobile-friendly, connector-ready, documented, and buildable.

Preferred stack:
- ASP.NET Core 8 MVC or Razor Pages for the backend/web app.
- Simple JSON-file persistence by default, unless SQLite is very easy and does not complicate the build.
- No paid external services.
- No hardcoded secrets, API keys, tokens, phone numbers, or credentials.
- Use environment variables for all real integrations.
- Simulated/demo mode must work locally without external accounts.
- Use a clean dark professional UI: navy/dark background, teal/cyan accents, dashboard cards, mobile-friendly layout, polished landing page.
- Add a .gitignore that prevents committing secrets, local env files, build folders, and temporary files.

Core rule:
Do not build only a local dashboard. Build CivicOps as an integration-ready civic operations system.

For every external service that cannot be live in the hackathon environment, create a clear adapter/interface, demo implementation, environment-variable configuration, connector status display, and documentation showing how the real connector would be enabled later.

Safety and honesty:
- CivicOps must not claim to replace police, fire, EMS, disaster management, or official emergency services.
- Citizen-facing pages must clearly say: if someone is in immediate danger, they must contact official emergency services directly.
- Do not claim live WhatsApp, live SMS, live voice transcription, live GIS, live municipal ERP, live emergency-service integration, or live Gemini usage unless actually configured.
- Demo/simulated connectors are acceptable, but they must be honestly labelled.

Execution instruction:
First inspect the empty repo, choose a simple buildable structure, then build the app.
Do not stop after planning.
Continue implementing as much as possible in this session.
If something is too large to complete, implement a working placeholder/adapter and document the limitation clearly.
Run only necessary commands.
Avoid long loops.
Keep the app buildable.

FULL CIVICOPS FLOW:
A resident sees a civic issue such as a burst pipe, electricity fault, blocked drain, road damage, illegal dumping, fire/flood risk, public safety concern, environmental hazard, sewage leak, waste collection issue, informal settlement hazard, damaged public space, or municipal service disruption.

The resident can report it through:
- public web/mobile form,
- Android app,
- WhatsApp text message,
- WhatsApp-style voice-note/audio intake,
- optional photo/media placeholder,
- optional location notes,
- suburb/area,
- ward.

The report enters the CivicOps intake pipeline.

The CivicOps agent layer should:
- extract structured fields from messy reports,
- summarise citizen reports,
- classify category,
- assign department,
- suggest priority,
- detect safety/disaster/public-risk keywords,
- produce clear public update wording,
- produce internal triage notes,
- route the report into the correct municipal workflow.

Gemini should assist this process only when configured.
The app must always work without Gemini using deterministic fallback classification/routing.

REQUIRED FEATURES:

1. Public landing page

Create a polished landing page explaining:
- public civic reporting,
- WhatsApp-ready intake,
- voice-note-ready intake,
- Android/mobile app access,
- AI/Gemini-assisted and deterministic fallback classification,
- municipal department routing,
- public reference/status tracking,
- department queues,
- area alerts,
- mobile/PWA readiness,
- integration readiness,
- safety disclaimer.

The landing page must make CivicOps look like a serious public operations system, not a toy demo.

2. Citizen report submission

Create a public mobile-friendly report form with:
- description,
- category,
- suburb/area,
- ward,
- optional contact name,
- optional phone/email,
- optional location notes,
- optional photo/media placeholder,
- optional voice-note/audio placeholder,
- emergency/safety disclaimer.

On submit:
- generate a public reference number like CIV-2026-0001,
- classify and route the incident,
- save it,
- show confirmation with reference number,
- show assigned department,
- show current status,
- show safety disclaimer.

3. Classification and routing engine

Implement deterministic fallback classification/routing using keywords and category mapping.

Departments must include:
- Water & Sanitation
- Electricity
- Roads & Stormwater
- Waste Management
- Parks & Public Spaces
- Housing/Informal Settlements
- Environmental Health
- Disaster Management
- Fire & Rescue
- Metro Police/Public Safety
- SAPS liaison/police referral
- EMS/medical referral
- Ward Councillor/Ward Committee

Each incident must have:
- unique internal ID,
- public reference number,
- source channel: Web, Android, WhatsApp, Voice Note, Demo,
- description,
- AI/deterministic summary,
- category,
- assigned department,
- suburb/area,
- ward,
- status,
- priority,
- created time,
- last updated time,
- public updates,
- internal notes/history,
- optional contact details,
- optional location notes,
- optional media/audio metadata,
- connector metadata where relevant.

Statuses:
- New
- Triaged
- Assigned
- In Progress
- Escalated
- Resolved
- Closed

Priorities:
- Low
- Medium
- High
- Urgent

4. Gemini CivicOps agent layer

Add an optional Gemini-powered CivicOps agent service using environment variables only.

Environment variables:
- GEMINI_API_KEY
- GEMINI_MODEL, default gemini-2.5-flash
- GEMINI_ENABLED=false by default unless configured
- GEMINI_MODE=Hybrid

The Gemini/CivicOps agent should help with:
- extracting structured fields from messy reports,
- summarising citizen reports,
- classifying category,
- assigning department,
- suggesting priority,
- detecting safety/disaster/public-risk keywords,
- producing clear public update wording,
- producing internal triage notes.

Requirements:
- Do not hardcode keys.
- Do not commit .env.
- Do not claim Gemini is live unless GEMINI_API_KEY is configured.
- App must work fully with deterministic fallback if Gemini is disabled.
- Show Gemini readiness/status on the admin/connector readiness page.
- Document Gemini setup in docs/gemini-setup.md.

5. Backend API requirements

Create APIs that the Android app, Web/PWA, WhatsApp webhook, and future connectors can call.

Required API endpoints:
- POST /api/reports
- GET /api/reports/{reference}
- GET /api/alerts
- GET /api/alerts?area=&ward=
- POST /api/voice-reports
- POST /api/media-reports or a media placeholder endpoint
- GET /api/departments
- GET /api/departments/{department}/queue
- GET /api/incidents/{id}
- POST /api/incidents/{id}/status
- POST /api/incidents/{id}/note
- POST /api/incidents/{id}/escalate
- GET /api/connectors/status
- GET /webhooks/whatsapp
- POST /webhooks/whatsapp
- POST /demo/whatsapp/inbound

API responses should be simple JSON and suitable for the Android app.

6. Public status lookup

Create a public page where a resident can enter their reference number and see:
- current status,
- assigned department,
- suburb/area,
- ward,
- created time,
- last update,
- latest public note,
- safety disclaimer.

7. Admin dashboard

Create an admin/dashboard page showing:
- total incidents,
- new incidents,
- in progress incidents,
- escalated incidents,
- resolved/closed incidents,
- incidents by department,
- incidents by source channel,
- recent reports,
- high-priority incidents,
- area alerts summary,
- connector readiness status,
- Gemini status,
- WhatsApp status,
- Android/API readiness status.

8. Department queues

Create department queue pages:
- list incidents filtered by department,
- incident detail page,
- update status,
- add internal note,
- add public update,
- escalate incident,
- resolve/close incident,
- show incident history/timeline,
- show source channel,
- show classification/routing explanation.

9. WhatsApp readiness

Add WhatsApp Cloud API-ready webhook endpoints:
- GET verification endpoint,
- POST inbound message endpoint.

Use these environment variables:
- WHATSAPP_VERIFY_TOKEN
- WHATSAPP_ACCESS_TOKEN
- WHATSAPP_PHONE_NUMBER_ID
- WHATSAPP_DEMO_MODE=true by default.

In demo mode:
- allow simulated inbound WhatsApp text reports,
- convert text into incidents using the same classification/routing pipeline,
- support media/voice-note metadata placeholders,
- show WhatsApp source channel on incidents,
- document that live WhatsApp requires Meta app setup and env vars.

WhatsApp must not be fake marketing text only.
Implement a working demo inbound WhatsApp simulator page or endpoint:
- POST /demo/whatsapp/inbound
- allow message text, sender label, suburb/ward/location fields if practical,
- generate a CivicOps incident and reference number.

Create docs/whatsapp-setup.md explaining:
- required env vars,
- webhook verification,
- inbound message flow,
- demo mode,
- production limitations,
- no secrets in repo.

10. Voice-note/audio readiness

Add a demo-safe voice-note/audio reporting flow:
- allow users to submit or simulate a voice-note report,
- store audio/media metadata or placeholder,
- allow supplied transcript/description field,
- create incident from transcript/description using same intake pipeline,
- show source channel as Voice Note,
- document that real transcription is connector-ready future work,
- do not falsely claim real speech-to-text if not implemented.

11. Android mobile app

Create a /mobile/CivicOpsAndroid folder with a simple Android app source if practical.

The Android app must support:
- citizen report submission,
- description/category/suburb/ward/contact/location notes,
- optional photo/media placeholder,
- optional voice-note/audio placeholder or simulated voice transcript field,
- submit to backend API,
- show generated reference number,
- save/show recent submitted references locally if practical,
- public status lookup by reference number,
- public area alerts by suburb/ward,
- WhatsApp reporting shortcut/deep-link or instruction screen,
- emergency disclaimer,
- connector/demo mode notice.

Android responder/admin mode if practical:
- simple role switch/demo login,
- department queue view,
- incident detail,
- update status,
- add note,
- escalate/resolve.

If Android SDK/build tooling is not available in Codespaces:
- still create the Android app source structure,
- document how to open/build it in Android Studio,
- ensure backend APIs are ready for it,
- do not let Android build issues break the web app,
- create docs/android-app.md explaining app purpose, screens, API calls, build steps, and demo flow.

12. Web/PWA requirements

The web app must include:
- landing page,
- public report form,
- public status lookup,
- public alerts page,
- admin dashboard,
- department queues,
- incident detail,
- status update workflow,
- connector readiness page,
- mobile/PWA readiness page,
- WhatsApp demo simulator page if practical,
- voice-note demo intake page if practical.

Make public pages mobile-first.

Add if practical:
- manifest.json,
- service worker,
- PWA install/readiness support.

Also add a Mobile/PWA Readiness page explaining:
- citizen mobile reporting,
- reference tracking,
- WhatsApp reporting,
- voice-note reporting,
- area alerts,
- future Android/iOS wrapper possibility,
- responder mobile queue use.

13. Area alerts

Create area/ward/suburb-based alerts.

Alert types:
- water outage,
- electricity disruption,
- road closure,
- flood,
- fire,
- waste collection disruption,
- environmental hazard,
- public safety notice,
- disaster warning.

Features:
- admin/demo creation of alerts,
- public alerts page,
- filter by suburb/area/ward,
- responsible wording,
- timestamps,
- affected department,
- severity,
- no panic wording.

14. External connector readiness

Create visible connector readiness for:
- Gemini
- WhatsApp Cloud API
- Android/mobile API
- SMS notifications
- email notifications
- voice transcription
- GIS/maps/geocoding
- municipal ERP/ticketing systems
- media/file storage
- authentication/RBAC
- audit logging
- production database

For each connector:
- create an interface or placeholder adapter where practical,
- create a demo implementation,
- document required environment variables or future setup,
- keep demo mode working locally.

Create a connector readiness/status page in the app showing:
- connector name,
- current mode: Demo, Configured, Disabled, Future Connector,
- required environment variables,
- what it enables,
- production notes.

15. Demo data

Seed realistic demo data:
- at least 10 civic incidents across different departments,
- at least 5 area alerts,
- realistic Durban/eThekwini-style suburb/ward examples,
- do not claim official municipal partnership,
- include water, electricity, roads, waste, fire/disaster, public safety, environmental health, and stormwater examples.

Example areas may include:
- Chatsworth
- Umlazi
- Phoenix
- Durban CBD
- Pinetown
- Newlands
- Isipingo
- KwaMashu
- Hillcrest
- Bluff

Do not claim official eThekwini partnership.

16. Documentation/reporting

Create:
- README.md
- docs/bob-report.md
- docs/build-log.md
- docs/demo-script.md
- docs/integration-readiness.md
- docs/android-app.md
- docs/whatsapp-setup.md
- docs/gemini-setup.md
- docs/ai-agent-submission-notes.md

README.md must include:
- app overview,
- setup commands,
- run commands,
- demo flow,
- key routes,
- API endpoints,
- environment variables,
- hackathon notes,
- safety disclaimer,
- integration readiness,
- no official municipal partnership claim.

docs/bob-report.md must capture:
- that IBM Bob was used to build CivicOps from scratch,
- session purpose,
- architecture chosen,
- major files created,
- features implemented,
- commands run,
- build/test result,
- remaining issues,
- future integration work,
- honest limitations.

docs/build-log.md must capture:
- step-by-step build progress,
- files created/changed,
- commands run,
- build result,
- fixes made,
- remaining cleanup needed.

docs/demo-script.md must include:
- 3 to 5 minute demo script,
- route order,
- what to show judges,
- how to explain web reporting,
- how to explain Android app flow,
- how to explain WhatsApp readiness honestly,
- how to explain voice-note readiness honestly,
- how to explain Gemini/fallback honestly,
- how to explain connector readiness.

docs/integration-readiness.md must explain:
- WhatsApp Cloud API connector,
- SMS connector,
- voice transcription connector,
- GIS/maps connector,
- municipal ERP/ticketing connector,
- email notification connector,
- auth/RBAC,
- audit logging,
- production database,
- privacy/security hardening.

docs/android-app.md must explain:
- Android app purpose,
- citizen flow,
- responder flow if present,
- backend API calls,
- how to open/build in Android Studio,
- demo limitations.

docs/whatsapp-setup.md must explain:
- Meta WhatsApp Cloud API setup requirements,
- env vars,
- webhook verification,
- inbound text flow,
- voice-note/media placeholder flow,
- demo mode.

docs/gemini-setup.md must explain:
- env vars,
- Gemini model,
- hybrid mode,
- deterministic fallback,
- no hardcoded keys.

docs/ai-agent-submission-notes.md must frame CivicOps as:
- an agentic civic operations platform,
- intake to structured incident,
- Gemini-assisted extraction/classification where configured,
- deterministic fallback,
- department workflow,
- public tracking,
- area alerts,
- connector-ready external channels.

17. Build and run

After creating the app:
- run restore/build,
- fix reasonable build errors,
- avoid long loops,
- keep the app buildable,
- record the build result in docs/bob-report.md and docs/build-log.md.

If Android build tools are unavailable:
- do not fail the whole project because of Android,
- document Android build steps,
- ensure backend APIs and Android source are present.

Quality bar:
- Working app is more important than perfect architecture.
- Connector-ready structure is more important than fake live integrations.
- Keep UI professional and mobile-friendly.
- Keep the app honest.
- Do not leave random broken files.
- Do not create unused complex systems that break the build.
- Do not remove documentation/report files.
- Do not commit secrets.

End result:
A complete from-scratch CivicOps hackathon prototype that can be demoed, pushed to GitHub, submitted for the IBM Bob hackathon with a public Bob report, and reused for an AI Agent hackathon with agentic workflow framing.

Final expected repo outcome:
- Buildable ASP.NET Core CivicOps web app.
- Public web/PWA reporting flow.
- Android app source or documented Android companion source under /mobile/CivicOpsAndroid.
- WhatsApp webhook/demo simulator.
- Gemini-ready hybrid agent layer with deterministic fallback.
- Department routing/queues.
- Public status lookup.
- Area alerts.
- Connector readiness page.
- Professional README and docs.
- Public IBM Bob report in docs/bob-report.md.
