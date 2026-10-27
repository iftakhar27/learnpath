# Prioritized Module Matrix

Priority: **P0** = MVP launch blocker, **P1** = Phase 2, **P2** = Phase 3.
Status reflects *this repository*, verified by automated tests where noted. "Planned" means not started.

| Module | Scope in MVP (P0) | Later (P1/P2) | Backend status |
|---|---|---|---|
| A. Auth & onboarding | Email+password, mobile OTP, JWT + rotating refresh tokens, profile, exam target, consent, account deletion request | Google/Apple sign-in (P1), data export (P1), guardian profiles (P1) | **Implemented + tested** (OTP sender is a dev stub; MSG91 adapter pending) |
| B. Home dashboard | Continue learning, enrolled courses, recent results, weak topics, today's plan, streak | Announcements (P1) | **Implemented + tested** |
| C. Course & syllabus | Exam → Course → Subject → Chapter → Topic → Resource, free preview flags, publish status | Prerequisites UI, versioning UI, search filters (P1) | **Implemented + tested** |
| D. Video learning | Provider video ID, signed playback URL hook, resume position, completion at 90% | Captions, chapters, quality (provider-dependent) | **Implemented** (progress); provider signing is an interface with a stub |
| E. Notes | PDF documents behind entitlement | Highlights, personal notes (P1) | **Implemented** (metadata + access check) |
| F. Question bank & practice | Single/multi-correct, numerical, integer, true/false; explanations; topic practice; retry without touching tests | Match, assertion-reason, passage, subjective (P1); bookmarked/incorrect practice (P1) | **Implemented + tested** |
| G. Test engine | Server timing, autosave, mark-for-review, idempotent submit, auto-expire, per-question marks/negative marks, deterministic shuffle, question snapshots | Section-wise timers (P1), descriptive manual evaluation (P1) | **Implemented + tested** |
| H. Results & analytics | Score, accuracy, correct/incorrect/skipped, subject & chapter breakdown, question review, attempt history | Percentile (needs cohort), difficulty/time analytics (P1) | **Implemented + tested** |
| I. Study planner | Rule-based daily tasks from untouched topics + weak topics + pending tests | Weekly plans, revision spacing (P1) | **Implemented + tested** |
| J. Revision & bookmarks | Bookmarks, incorrect-answer list | Revision due dates (P1) | **Implemented** |
| K. Previous-year papers | — | Paper repository (P1), licence-checked | Planned |
| L. Notifications | In-app notifications | FCM push, email/SMS, quiet hours (P1) | **Implemented** (in-app) |
| M. Payments | Razorpay orders, signature verify, idempotent webhook, entitlements | Coupons, subscriptions renewal, refunds UI, invoices (P1) | **Implemented + tested** (HMAC verified; live keys not configured) |
| N. Search | Simple ILIKE search over courses/topics | Postgres FTS → Meilisearch (P2) | **Implemented** |
| O. Profile & settings | Profile, language, study target, logout, delete | Export (P1) | **Implemented** |
| P. Content management | CRUD for exams/courses/subjects/chapters/topics/resources/questions/tests, publish, CSV import | Media upload to provider, scheduling, audit diff (P1) | **API + admin UI implemented, e2e tested** |
| Q. Admin dashboard | Counts, attempts, payments, reported issues, students, orders, audit log | Role dashboards, charts (P1) | **API + admin UI implemented, e2e tested** |
| R. Support | Tickets + question reports | Helpdesk integration (P2) | **API + admin UI implemented, e2e tested** |
| S. Gamification | Streak | Badges, leaderboards (P2) | Streak implemented |
| T. Localization & a11y | `en`/`hi` content variants on title fields, `Accept-Language` | Full translation tooling (P1) | Data model ready |
| Mobile app (Flutter) | All P0 student screens | — | **Planned** (next phase) |
| Web app (Next.js) | Admin portal | — | **Implemented + e2e tested** |
| Web app (Next.js) | Student portal | — | **Planned** (next phase) |
