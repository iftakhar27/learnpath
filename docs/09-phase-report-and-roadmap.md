# Phase Report & Roadmap

## Phase 0 + Phase 1 (backend) — report, 26 Oct 2026

### Completed
- Planning artifacts: PRD, module matrix, architecture, data model + ERD, API spec + OpenAPI, screen map + design tokens,
  cost estimate, security/ops checklist.
- ASP.NET Core 8 modular-monolith API (94 endpoints) on PostgreSQL 16 with EF Core migrations and seed data:
  identity (email/password, OTP, rotating refresh tokens, consent, guardian consent, deletion request), catalog & syllabus,
  entitlements, signed media URLs, video progress/resume, bookmarks, learning history, practice engine, exam engine,
  results & analytics, rule-based study planner, dashboard, Razorpay orders/verify/webhooks, notifications, support
  tickets, question reports, search, admin content/question/test management, CSV import, admin dashboard, audit log.

### Verified
- `dotnet test`: **53 passed, 0 failed** (19 unit tests on scoring/validation/CSV/phone parsing; 34 integration tests
  that boot the real API against a throwaway PostgreSQL database).
- Manual curl walk-through on the running API: register → onboard (Hindi) → enrol → test → submit → duplicate submit →
  dashboard/plan → paywall on paid test.

### Not verified / known gaps
- Docker image and `docker-compose.yml` are written but **not built here** (no Docker daemon in this environment).
- `RazorpayGateway.CreateOrderAsync` is untested against the real Razorpay API (tests use the offline gateway; the HMAC
  verification code is shared and tested).
- Video/document URL signing format must be confirmed against the chosen provider.
- OTP delivery is a log-only stub. No MSG91 integration yet.
- No load testing; no performance claims are made.
- Not implemented in this phase: Flutter app, Next.js student web, Next.js admin UI, push notifications (FCM), email,
  coupons/subscriptions, previous-year paper repository, account-erasure job, data export.

### Database changes
- Migration `InitialCreate` (38 tables plus the EF migrations history table). See `docs/schema.sql`.

## Roadmap (estimates assume one full-stack dev + part-time Flutter dev, AI-assisted)

| Phase | Scope | Duration | Est. cost |
|---|---|---|---|
| 1b | Next.js admin portal (catalog tree editor, question editor + CSV upload, test builder, tickets, orders, dashboard) | 2–3 weeks | ₹25–45k |
| 1c | Next.js student web (onboarding, learn, video player, practice, exam UI, results, plan, checkout) | 3 weeks | ₹25–45k |
| 1d | Flutter app (same journeys; Riverpod + Dio + go_router; generated API client; ARB en/hi) | 4–5 weeks | ₹40–80k |
| 1e | Integrations: MSG91, Razorpay live test mode, Bunny Stream signed playback, FCM, Sentry; staging deploy | 1–2 weeks | ₹10–20k |
| 1f | QA on devices, accessibility pass, store submission | 1–2 weeks | ₹10–25k |
| 2 | PYQ papers, coupons & subscriptions, revision scheduling, richer analytics, bulk import UI, erasure/export jobs | 6–8 weeks | ₹1–1.5 lakh |
| 3 | Adaptive practice, AI-assisted explanations (SME-reviewed), offline/DRM, live classes, cohort percentiles, leaderboards | scoped later | — |

Total to a launchable MVP (Phases 1b–1f) ≈ **12–14 weeks, ₹1.1–2.2 lakh** on top of the work already done,
excluding content production. See `07-cost-estimate.md`.
