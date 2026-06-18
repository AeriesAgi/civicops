# Test Output Notes

Final verification notes:

- `dotnet restore`: passed, all projects up to date.
- `dotnet build --no-restore`: passed with 0 warnings and 0 errors.
- Local app run: passed after binding with escalated local-network permission on `http://127.0.0.1:5000`.
- `GET /healthz`: passed; returned Simulation mode, `demoMode: true`, `agents: 5`, no Band key configured.
- `GET /api/integrations/status`: passed; returned configured/not-configured flags only, no secret values.
- `GET /demo/band`: passed; HTML returned successfully.
- `POST /api/band/simulate` with `water-main-leak`: passed; returned a Band room URL.
- Local Android Gradle build: blocked by missing Android SDK in this container. The Android APK workflow was updated for GitHub Actions, which has Android SDK setup.

Expected baseline: all core flows pass in deterministic fallback mode without Band, AI/ML API or Featherless keys.
