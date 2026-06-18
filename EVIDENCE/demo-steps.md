# Demo Steps

1. Run `dotnet run --urls http://localhost:5000`.
2. Open `http://localhost:5000/demo/band`.
3. Select `Burst water main threatening homes`.
4. Keep `Auto-confirm human step` checked for a 2-4 minute video.
5. Click `Launch in Band`.
6. Record the Band room stream showing raw report, classification, assignment proposal, human decision, dispatch, public update, monitoring, escalation if present, resolution and summary.
7. Open `http://localhost:5000/api/integrations/status` to show fallback/live readiness without exposing credentials.
