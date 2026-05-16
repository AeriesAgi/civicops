# Gemini Connector Setup

CivicOps is connector-ready for server-side Gemini classification. The browser never receives the Gemini API key, and the app keeps the deterministic fallback available whenever Gemini is disabled, missing credentials, or unavailable.

## Environment variables

Set these in the server environment, not in frontend code:

```bash
GEMINI_API_KEY=your_google_ai_studio_key
GEMINI_ENABLED=true
GEMINI_MODEL=gemini-2.5-flash
GEMINI_MODE=Hybrid
```

Safe defaults in `appsettings.json` keep demo mode working without a key:

- `GEMINI_ENABLED=false`
- `GEMINI_MODEL=gemini-2.5-flash`
- `GEMINI_MODE=Hybrid`

## Runtime behavior

- When `GEMINI_ENABLED=true` and `GEMINI_API_KEY` is present, `GeminiService` calls the server-side Gemini API.
- When Gemini is disabled, missing a key, returns an error, or cannot be parsed, CivicOps automatically uses deterministic classification.
- The AI Agent Console displays whether the current run is using Gemini or fallback.
- No API key is rendered into Razor views or JavaScript.

## Live test endpoint

Use the connector health endpoint after starting the app:

```bash
curl http://localhost:5000/api/connectors/gemini/test
```

Expected fallback response without a key:

```json
{
  "success": false,
  "status": "Disabled - Using Deterministic Fallback",
  "model": "gemini-2.5-flash",
  "mode": "Hybrid",
  "message": "Gemini is not enabled or GEMINI_API_KEY is missing; deterministic fallback is active."
}
```

With valid environment variables, the endpoint performs a small live classification test and reports whether Gemini processed it.

## Submission note

IBM Bob built and accelerated the main hackathon implementation. This final Codex cleanup completed the live Gemini readiness path and stability pass. Do not claim live Gemini operation unless the deployment has the required environment variables configured.
