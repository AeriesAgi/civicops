# CivicOps Citizen App/PWA

The primary public channel is the **CivicOps Citizen App** at `/citizen-app` and `/app`.

Capabilities:

- Resident login/profile and demo credentials.
- Submit civic reports with text, location notes, photo placeholder and voice-note readiness.
- Track own reports and public references.
- View area alerts and weather/area risk context.
- Follow suburbs, wards and areas.
- Open Copilot/AI Assistant actions that call the backend explicitly.
- Lightweight community incident/area threads for local confirmations and related activity.

The PWA and Android shell use the same backend routes. Gemini runs server-side only when triggered by a report submission or explicit Copilot/staff/judge action. WhatsApp is optional connector-ready support, not the main citizen channel.
