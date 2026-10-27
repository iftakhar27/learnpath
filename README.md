# LearnPath — exam-prep learning platform (working codename)

Backend and product docs for a NEET / JEE / Board learning app: recorded lessons, notes, practice, timed tests,
analytics, study planner and paid courses, in English and Hindi. The brand name is configurable (`Branding__ProductName`).

**Status:** planning docs, the backend API and the **admin portal** are built and tested. The student website and the
Flutter mobile app are **not built yet**. See [`docs/09-phase-report-and-roadmap.md`](docs/09-phase-report-and-roadmap.md).

## Docs

| Doc | |
|---|---|
| [01-PRD](docs/01-PRD.md) | Goals, personas, scope, product rules, open questions |
| [02-module-matrix](docs/02-module-matrix.md) | Modules A–T, priority, implementation status |
| [03-architecture](docs/03-architecture.md) | System diagram, decisions, security model |
| [04-data-model](docs/04-data-model.md) · [schema.sql](docs/schema.sql) | ERD, integrity rules, migrations |
| [05-api-spec](docs/05-api-spec.md) · [openapi.json](docs/openapi.json) | Conventions, flows, endpoint index |
| [06-screen-map](docs/06-screen-map.md) | Screen inventory, navigation, design tokens |
| [07-cost-estimate](docs/07-cost-estimate.md) | Build budget and monthly running cost (INR) |
| [08-security-and-operations](docs/08-security-and-operations.md) | Security checklist, backups, production readiness |
| [09-phase-report-and-roadmap](docs/09-phase-report-and-roadmap.md) | What's done, verified, and next |

## Repository layout

```
src/LearnPath.Api/
  Domain/            entities (Identity, Catalog, Assessment, Engagement, Commerce)
  Data/              AppDbContext, Seeder, Migrations/
  Infrastructure/    errors, language, hashing, audit, Razorpay/OTP/video adapters
  Modules/           Identity, Catalog, Learning, Practice, Exams, Analytics, Planner, Commerce,
                     Notifications, Search, Admin
tests/LearnPath.Tests/  unit + integration tests (real PostgreSQL)
web/                    Next.js admin portal (student web next) — see web/README.md
scripts/                export-openapi.sh
docs/
```

## Prerequisites
- .NET 8 SDK
- PostgreSQL 14+ (16 recommended)
- Optional: Docker + Docker Compose

## Run locally

```bash
# 1. Database (any local Postgres with user postgres/postgres works)
docker compose up -d db          # or use an installed PostgreSQL

# 2. API — Development settings use sample content, a fake payment gateway and console OTPs
dotnet run --project src/LearnPath.Api --urls http://localhost:5080
# Swagger UI: http://localhost:5080/swagger   Health: http://localhost:5080/health
# Seeded admin: admin@example.com / ChangeMe!2026  (from appsettings.Development.json; change it)
```

Admin portal (in another terminal):
```bash
cd web && npm install && npm run build && npm start     # http://localhost:3000/admin
```

Or run the API and database in containers: `cp .env.example .env && docker compose up --build`. API on http://localhost:8080.

Migrations are applied automatically on start. To add one:
```bash
dotnet tool install --global dotnet-ef --version 8.0.10
dotnet ef migrations add <Name> -p src/LearnPath.Api -o Data/Migrations
```

## Tests

```bash
dotnet test                               # backend unit + integration
cd web && npm run build && npm run test:e2e   # admin portal end-to-end (starts its own API + web servers)
```
Integration tests create and drop a temporary database `learnpath_test_*`. Point them at another server with
`TEST_PG="Host=...;Port=5432;Username=...;Password=..."`.

## Configuration (environment variables)

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__SigningKey` | ≥ 32 bytes random. **Required** |
| `Seed__AdminEmail`, `Seed__AdminPassword` | Creates the first SuperAdmin if missing |
| `Seed__SampleContent` | `true` to load demo content (never in production) |
| `Razorpay__KeyId`, `Razorpay__KeySecret`, `Razorpay__WebhookSecret` | Payment gateway |
| `Razorpay__UseFakeGateway` | Offline gateway for dev/tests (refused in Production) |
| `Otp__Provider` | `log` (dev only, refused in Production). Production needs an MSG91 sender |
| `Video__LibraryId`, `Video__TokenKey` | Bunny Stream signed playback |
| `Storage__CdnBaseUrl`, `Storage__SigningKey` | Signed PDF URLs |
| `Cors__Origins__0..n` | Allowed web origins |
| `Exams__SubmissionGraceSeconds` | Late-save grace (default 30) |
| `Analytics__MinCohortForPercentile` | Hide percentile below this cohort size (default 100) |
| `Database__MigrateOnStartup` | Set `false` in production and run reviewed scripts |

## Deployment (lean)

1. Build the image: `docker build -t learnpath-api .`
2. Provision managed PostgreSQL (Mumbai/Bangalore region) and a small VM or container service.
3. Set the environment variables above as secrets; `ASPNETCORE_ENVIRONMENT=Production`.
4. Generate and run migrations: `dotnet ef migrations script --idempotent -p src/LearnPath.Api -o migrate.sql`, then `psql -f migrate.sql`.
5. Put a TLS-terminating proxy (Caddy/nginx or the platform's load balancer) in front; point uptime monitoring at `/health`.
6. Register `https://<host>/api/v1/payments/webhooks` in the Razorpay dashboard (events: `payment.captured`, `order.paid`, `payment.failed`).

Backups and the full checklist: [`docs/08-security-and-operations.md`](docs/08-security-and-operations.md).
