# Security Checklist, Backup & Production Readiness

Legend: ✅ implemented and covered by tests · 🟡 implemented, not yet verified in a deployed environment · ⬜ to do

## Security checklist

| Control | Status | Where |
|---|---|---|
| BCrypt password hashing (work factor 11) | ✅ | `AuthController` |
| Short-lived JWT (15 min) + rotating hashed refresh tokens, reuse detection | ✅ | `TokenService`, `AuthTests` |
| Role-based policies on every admin endpoint | ✅ | `Program.cs`, `AdminTests.Teacher_can_edit_but_not_publish` |
| Students can only read their own attempts/orders/bookmarks | ✅ | `ExamEngineTests.Other_students_cannot_read…` |
| Answer keys/explanations never sent before submission | ✅ | `ExamEngineTests.Attempt_never_exposes…` |
| Server-authoritative timing; grace window; auto-submit | ✅ | `ExamService`, `ExamTimingTests` |
| Idempotent submit and payment settlement (row locks, unique keys) | ✅ | `ExamEngineTests`, `CommerceTests` |
| Razorpay checkout + webhook HMAC verification, amount check, event de-dup | ✅ | `PaymentsController`, `CommerceTests` |
| Entitlement checks on paid video, notes, practice and tests | ✅ | `EntitlementService`, `LearningTests` |
| Signed, expiring media URLs | 🟡 | `BunnyVideoUrlSigner`, `HmacDocumentUrlSigner` — confirm token format with chosen provider |
| OTP: hashed at rest, 5-min expiry, single use, 5 wrong tries, 30 s resend gap, 5 per 15 min | ✅ | `AuthController`, `AuthTests` |
| Auth rate limiting per IP | 🟡 | `RateLimit:AuthPerMinute`; behind a proxy, configure forwarded headers |
| Parameterised SQL only (EF Core / interpolated raw SQL) | ✅ | |
| Input validation (DataAnnotations + domain checks) | ✅ | |
| Audit log for admin mutations | ✅ | `AuditService` |
| Minimal PII to staff (Analyst sees aggregates only) | ✅ | `AdminOpsController` policies |
| Guardian consent recorded for minors | ✅ | `ProfileController` |
| Fake gateway / log OTP refused in Production | ✅ | `Program.cs` startup guard |
| Secrets only from environment | ✅ | `appsettings.json` has empty secrets |
| HTTPS + HSTS in Production | 🟡 | Terminate TLS at proxy/load balancer |
| Real OTP provider (MSG91, DLT template) | ⬜ | Implement `IOtpSender` |
| Account deletion completion job + data export | ⬜ | Request is recorded; erasure job is P1 |
| Dependency scanning (`dotnet list package --vulnerable`, Dependabot) | ⬜ | Add to CI |
| Error monitoring (Sentry) | ⬜ | |
| Legal review: privacy policy (DPDP Act 2023), terms, refunds, children's data | ⬜ | Owner |

Never log: passwords, OTP codes (only the dev sender does, refused in Production), tokens, Razorpay secrets, answer keys.

## Backup & recovery

| Item | Policy |
|---|---|
| Database | Managed Postgres automated daily backups + point-in-time recovery (7–14 days). Additionally a nightly `pg_dump` to object storage in another region, kept 30 days. |
| Media | Video lives at the provider; PDFs in versioned object storage. |
| Config/secrets | Stored in the hosting platform's secret manager; documented in a private runbook. |

```bash
# Nightly logical backup
pg_dump --format=custom --no-owner "$DATABASE_URL" > learnpath-$(date +%F).dump
# Restore into a fresh database (rehearse monthly on staging)
createdb learnpath_restore
pg_restore --no-owner --dbname=learnpath_restore learnpath-YYYY-MM-DD.dump
```
Recovery targets to adopt once measured: RPO ≤ 24 h (≤ 5 min with PITR), RTO ≤ 4 h.

## Production-readiness checklist

- [ ] Real Razorpay keys (test mode first), webhook URL registered, webhook secret set
- [ ] MSG91 sender implemented and DLT template approved
- [ ] Video provider account; signed playback confirmed end-to-end on Android, iOS, web
- [ ] `Database:MigrateOnStartup=false`; reviewed migration script run in release pipeline
- [ ] Health check `/health` wired to uptime monitoring
- [ ] Sentry (API + apps), structured logs shipped and retained ≤ 30 days
- [ ] Backups verified by a restore rehearsal
- [ ] Load test on staging for the exam-start spike (e.g. k6: 500 concurrent starts) — no performance claims until done
- [ ] Privacy policy, terms, refund policy published and linked in apps
- [ ] Play Store data-safety form and App Store privacy labels completed
- [ ] Content: only original or licensed material; sample content (`Seed:SampleContent`) disabled
