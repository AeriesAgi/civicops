# CivicOps Command — Backend

**Enterprise Operational Intelligence Platform** for security, fleet, dispatch, and emergency response operations.

This repository contains the **production-grade ASP.NET Core 8 backend** — REST API, real-time SignalR engine, AI dispatch engine, multi-tenant architecture, and full operational modules.

---

## Architecture

Clean Architecture across four projects:

```
src/
├── CivicOps.Domain/          # Entities, enums, domain events, repository contracts
├── CivicOps.Application/      # CQRS commands/queries, handlers, DTOs, validators, AI logic
├── CivicOps.Infrastructure/   # EF Core, PostgreSQL, Redis, Gemini, Firebase, Twilio, jobs
└── CivicOps.Api/              # Controllers, SignalR hubs, middleware, DI wiring
tests/
├── CivicOps.UnitTests/        # Domain + handler unit tests (xUnit + FluentAssertions)
└── CivicOps.IntegrationTests/ # API integration tests
```

**Tech stack:** ASP.NET Core 8 · C# 12 · EF Core 8 · PostgreSQL 16 + TimescaleDB · Redis 7 · SignalR · MediatR (CQRS) · FluentValidation · Hangfire · Gemini API · Serilog · Docker

---

## Core Modules

| Module | Endpoints | Description |
|---|---|---|
| **Auth** | `/api/v1/auth/*` | JWT + refresh tokens, MFA (TOTP), account lockout |
| **Fleet** | `/api/v1/fleet/*` | Vehicle CRUD, live GPS, history, trips, batch ingestion |
| **Incidents** | `/api/v1/incidents/*` | Full lifecycle, media uploads, escalation, SLA, timeline |
| **Dispatch** | `/api/v1/dispatch/*` | AI nearest-unit recommendation, queue, assignment lifecycle |
| **Analytics** | `/api/v1/analytics/*` | Dashboard KPIs, response times, heatmaps, SLA compliance |
| **Maintenance** | `/api/v1/maintenance/*` | Schedules, records, alerts, AI predictive analysis |
| **Users** | `/api/v1/users/*` | RBAC user management (8 roles) |
| **Panic** | `/api/v1/panic` | Emergency panic events with SMS + real-time escalation |
| **Admin** | `/api/v1/admin/*` | Multi-tenant management (Super Admin) |

Real-time SignalR hubs: `/hubs/operations`, `/hubs/fleet`, `/hubs/dispatch`

---

## Quick Start (Docker)

```bash
# 1. Configure environment
cp .env.example .env
# Edit .env — set JWT_SECRET (32+ chars), DB_PASSWORD, GEMINI_API_KEY (optional)

# 2. Start the full stack
docker compose up -d

# 3. Watch logs
docker compose logs -f api

# 4. Access
#   API + Swagger docs:  http://localhost:5000/docs
#   Health check:        http://localhost:5000/health
#   Hangfire dashboard:  http://localhost:5000/hangfire
#   RabbitMQ UI:         http://localhost:15672
```

Migrations run automatically and demo data is seeded in Development mode.

### Demo Credentials

| Role | Email | Password |
|---|---|---|
| Super Admin | `admin@demo.civicops.io` | `Admin@123!` |
| Ops Manager | `ops@demo.civicops.io` | `Ops@123!` |
| Dispatcher | `dispatcher@demo.civicops.io` | `Ops@123!` |
| Officer | `officer1@demo.civicops.io` | `Ops@123!` |
| Client Viewer | `client@demo.civicops.io` | `Client@123!` |

The demo tenant (**Metro Security Solutions**, Durban) seeds 14 vehicles, 35 users, 90 days of GPS history, ~1,080 historical incidents, and 7 live incidents.

---

## Local Development (without Docker)

```bash
# Prerequisites: .NET 8 SDK, PostgreSQL 16 (+ TimescaleDB), Redis 7

# 1. Restore + build
dotnet restore
dotnet build

# 2. Apply migrations
dotnet ef database update -p src/CivicOps.Infrastructure -s src/CivicOps.Api

# 3. Run
dotnet run --project src/CivicOps.Api

# 4. Run tests
dotnet test
```

---

## Multi-Tenancy

Tenants are resolved (in order) from:
1. JWT `tenant_id` claim (authenticated requests)
2. Subdomain (`acme.civicops.io` → slug `acme`)
3. `X-Tenant-Slug` header (API integrations)

EF Core global query filters automatically scope every query to the current tenant — no manual `WHERE tenant_id = ...` needed.

## RBAC Roles

`SuperAdmin` · `OperationsManager` · `Dispatcher` · `Supervisor` · `PatrolOfficer` · `FleetManager` · `Driver` · `ClientViewer`

Policies enforced via `[Authorize(Policy = "...")]` attributes on controllers.

## AI Dispatch Engine

The `ILLMProvider` abstraction lets you swap AI backends. Default: Gemini. To use Claude or OpenAI, implement `ILLMProvider` and register it in `Program.cs`. The dispatch engine combines Haversine spatial ranking with LLM reasoning — recommendations only; humans confirm dispatch.

## Real-Time Events (SignalR)

Server → client events: `GpsUpdate`, `IncidentCreated`, `DispatchUpdate`, `PanicTriggered`, `GeofenceAlert`, `SlaBreachWarning`, `SlaBreached`, `MaintenanceAlert`, `SystemNotification`.

Connect with JWT in the query string: `wss://api/hubs/operations?access_token=<jwt>`

## Background Jobs (Hangfire)

| Job | Schedule | Purpose |
|---|---|---|
| SLA Monitor | every minute | Warns at 80%, marks breach at 100% |
| Geofence Check | every 30s | Enter/exit detection |
| Maintenance Alerts | daily 06:00 | Due/overdue notifications |
| Analytics Refresh | every 5 min | Cache + materialized view refresh |

## Health & Observability

- `/health` — full health (Postgres + Redis)
- `/health/ready` — readiness probe
- `/metrics` — Prometheus metrics
- Structured logging via Serilog (console + rolling file)
- Optional Grafana/Prometheus: `docker compose --profile observability up -d`

## Security

JWT (15-min access + 7-day rotating refresh) · BCrypt password hashing (work factor 12) · TOTP MFA · account lockout after 5 failed attempts · rate limiting (auth/api/gps tiers) · audit logging of all mutations · tenant isolation · HTTPS enforcement · security headers via NGINX.

---

## License

Commercial — © CivicOps. All rights reserved.
