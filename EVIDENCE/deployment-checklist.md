# Deployment Checklist

- Set `PORT` for the hosting platform.
- Keep `DEMO_MODE=true` for deterministic judging unless live Band credentials are ready.
- For live Band mirror, run `band-bridge/`, set `BAND_API_KEY`, `BAND_API_BASE_URL`, `Band__Mode=Live`, `Band__BridgeUrl` and `DEMO_MODE=false`.
- Do not expose `.env`, API keys, phone tokens or appsettings secret overrides.
- Confirm `/healthz` returns `status: ok`.
- Confirm `/demo/band` opens and can launch the seeded scenario.
- Confirm `/api/integrations/status` does not return secret values.
