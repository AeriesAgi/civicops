# CivicOps Mobile/PWA

CivicOps is PWA-first. The primary citizen channel is the **CivicOps Mobile Citizen App** at `/Home/Mobile` and `/app`.

Working mobile flows:
- Report issue: `/Home/Report`
- Track reference: `/Home/Lookup`
- My reports/profile: `/Resident/MyReports`, `/Resident/Profile`
- Area alerts: `/Home/Alerts`
- Weather / area conditions: `/Home/Weather`
- Optional WhatsApp connector info: `/Demo/WhatsAppSimulator`

PWA assets:
- `wwwroot/manifest.json`
- `wwwroot/service-worker.js`
- `wwwroot/icons/civicops-icon.svg`
- Service worker registration in `wwwroot/js/site.js`

Opening the mobile app does **not** call Gemini. AI runs only on report submission or explicit staff/judge actions.
