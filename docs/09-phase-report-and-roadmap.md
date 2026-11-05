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

## Phase 1b (admin portal) — report, 27 Oct 2026

### Completed
- Next.js 16 admin portal in `web/` (17 routes): sign-in, dashboard, courses & syllabus tree editor (subjects,
  chapters, topics, videos/notes with reorder, publish, guarded delete), exams, question bank with URL-synced filters,
  question editor (all five types, bilingual fields, live student preview, versioning notice), CSV import with
  template/topic-ID downloads and row-level errors, test list and builder (sections, question-bank picker, per-question
  marks, reorder, lock + duplicate once attempted), students & staff (search, detail, status, roles, grant access),
  orders, support tickets and question reports, announcements, audit log. Role-aware navigation and page gates.
- Backend-for-frontend session: httpOnly cookies, same-origin proxy, server-side de-duplicated token refresh.
- Backend: typed admin DTOs so the OpenAPI contract generates exact TypeScript types; test detail/duplicate;
  guarded deletes; self-lockout guards; fixed an untranslatable EF query on the student-detail and filtered-orders
  endpoints (found by the e2e tests).

### Verified
- `dotnet test`: **59 passed, 0 failed** (adds 6 admin-portal API tests).
- `npm run test:e2e` (Playwright, real API + PostgreSQL + production Next.js build): **11 passed, 0 failed**, run twice
  in a row. Covers sign-in/redirect/refusal of students, httpOnly cookies, transparent token refresh, sign-out, the
  full course → syllabus → video → question → test → publish flow with a student then taking the test via the API,
  CSV import errors, teacher permissions, ticket reply + course grant + audit entry, and announcements.
- `tsc --noEmit` and `eslint`: no errors (5 informational React Compiler notices about react-hook-form's `watch()`).

### Known gaps
- Videos and PDFs are referenced by provider ID / storage key; there is no upload-to-provider flow yet.
- Admin UI text is English only (content fields support Hindi). Student-facing apps will use translation keys.
- No Content-Security-Policy headers yet; no visual regression or screen-reader audit beyond semantic markup.
- Token-refresh de-duplication is per Next.js process: run a single instance or sticky sessions.

## Phase 1d (Flutter student app) — report, 4 Nov 2026

### Completed
- `mobile/`: Flutter 3.47 app for Android and iOS (plus a web build used for demos), on the decoupled `material_ui`
  library, Riverpod 3, go_router 18 and Dio. Screens: welcome with language choice, email sign-in, registration,
  mobile OTP, three-step onboarding (exam/year, class/board, study time/language/guardian consent), home dashboard
  (countdown, streak, weekly stats, today's plan, continue learning, courses, weak topics, recent results), catalog
  with search and price filters, course syllabus with free enrolment or Razorpay purchase, topic page, HLS video
  player (resume, progress saving, speed, full screen), notes, practice (topic, mistakes, bookmarked, previous-year)
  with instant solutions, bookmarks and question reports, tests list, instructions, timed exam player (server-derived
  timer, palette, mark for review, clear, autosave with offline retry, auto-submit, leave/resume), results with
  breakdowns and recommendations, answer review with filters, study plan, progress, notifications, purchases, support
  tickets, search, profile and account-deletion request.
- All UI text in English and Hindi via ARB translation keys (261 strings).
- Typed Dart models generated from `docs/openapi.json` by `mobile/tool/gen_models.dart` (125 schemas).
- Backend: HLS `streamUrl` for native players (Bunny CDN token auth), typed learning-history response,
  `AnswerResponse.isEmpty` removed from the contract, Flutter web origin in development CORS.

### Verified
- `dotnet test`: **63 passed, 0 failed** (adds video URL signing tests).
- `flutter analyze`: no issues. `flutter test`: **20 passed, 0 failed** (token refresh single-flight, exam session
  logic, generated models, full-app widget flows with a fake API, exam player end to end).
- Android release APK built (`com.learnpath.app`, Android 7.0+, 58 MB, arm64/armv7/x86_64, debug-signed for testing).
  Not yet installed on a physical device.
- `mobile/tool/browser_demo.js` drove the web build against the live API through 21 screens (welcome in both
  languages, sign-in, onboarding, home, catalog, course, topic, practice with feedback, tests, instructions, exam,
  palette, submit, result, review, profile, Hindi home) with no page errors. It found and fixed: an unsafe route
  `extra` cast that crashed after browser back/forward, and question text exposed to screen readers as a disabled text box.

### Known gaps
- iOS build not produced (needs macOS/Xcode). Razorpay checkout and video playback need device testing with real keys.
- Hindi strings need native-speaker review. Study-plan task titles and reasons are generated in English by the API
  (to be localized server-side). Push notifications (FCM) and offline downloads are not implemented.
- Release APK is signed with the debug key; create an upload key before publishing.

## Phase 1e (part): staging deployment on Render — report, 4 Nov 2026

**Built:** `render.yaml` Blueprint (API web service from the `Dockerfile` + PostgreSQL 16, Singapore region, free plans),
`.dockerignore`, guide in `10-deploy-render.md`. The API now accepts Render's `postgres://` connection URLs, honours
`ASPNETCORE_FORWARDEDHEADERS_ENABLED` for real client IPs, and refuses OTP providers that aren't implemented
(`Otp:Provider` defaults to `disabled`, which returns a clear 503 instead of failing silently).

**Verified locally:** the Release build started with the Staging settings Render uses (URL-style connection string
with no port, generated JWT key, forwarded headers): migrations ran on an empty database, admin and sample content
were seeded, `/health` and `/swagger` returned 200. Sign-in rate limiting was per client: the same forwarded IP got
429 after 20 attempts, 22 different IPs were never limited. 69 backend tests pass (5 new for deployment config).

**Live staging (verified 4 Nov 2026):** the account owner created the Blueprint; Render built the Docker image and the
service came up healthy. A scripted student journey against the live URL passed 26/26 checks: config, exams,
register, profile, dashboard, study plan, catalog, syllabus, free enrolment, topic, practice with explanation, test
attempt, autosave, submit (and idempotent resubmit), results, analytics, notifications, orders, OTP request, refresh
rotation with reuse rejected, and 401 without a token. After enrolling, the study plan filled with next lessons.
Swagger is served (Staging). Sign-up took ~8 s on the free instance (bcrypt on a shared CPU); other calls 0.3–3 s.
The APK was rebuilt against the live API, and the app's response timeout was raised to 60 s so the first request
after the free instance sleeps can wait for it to wake.

**Not verified:** the app on a physical phone against the live server (to be done by the owner), Razorpay and video
playback (simulated gateway, no Bunny Stream configured).

### Admin portal deployment (4 Nov 2026)

**Built:** `learnpath-admin` service in `render.yaml` (Node 22, `web/` root, `npm ci && npm run build`, `npm start`),
`API_BASE_URL` entered once in Render, secure cookies on. Without `API_BASE_URL` in production the portal now answers
503 `api_not_configured` with a plain message instead of trying localhost. Build filters: API changes redeploy only the
API, `web/` changes only the portal.

**Verified here:** a clean `npm ci` + production build of `web/` (as Render runs it), started with `npm start` and
pointed at the live staging API: home 200, admin pages redirect signed-out visitors to login with a relative
`Location`, the catalog loads through the server-side proxy, a student account is refused (`not_staff`, no cookies
set), a wrong password returns the API's 401, cross-site POSTs get 403, `/api/v1/auth/*` isn't exposed, and the
unconfigured copy returns 503. Typecheck clean; all 11 Playwright end-to-end tests pass against a local API.

**Not verified:** the portal running on Render itself and a staff sign-in to the live API (the admin password exists
only in the owner's Render dashboard).

### Test sign-in codes in the admin portal (4 Nov 2026)

**Why:** no SMS provider is integrated yet, so on staging a code requested with "Continue with mobile number" only
reached the server log, which testers couldn't see. **Built:** codes sent by the logging sender are kept in memory
(last 20, one hour) and shown on the admin Dashboard to Support/SuperAdmin with a live expiry countdown. The endpoint
returns 404 unless `Otp:Provider=log` (refused in Production), and phone numbers show only their last 4 digits.
**Verified:** 73 backend tests (4 new: masking/ordering/capacity, role check, absent with a real provider) and 12
Playwright tests (new: a code requested through the API appears on the dashboard and signs the tester in; teachers
don't see the panel). **Still needed for real users:** an SMS provider (see roadmap 1e).

## Phase 1c (student website) — report, 5 Nov 2026

**Built** (Next.js, same app as the admin portal; English/Hindi with the app's strings):
sign-in with mobile OTP or email and account creation, three-step onboarding, home dashboard, course catalog
(search, exam, free/paid), course + syllabus with free enrolment and Razorpay checkout, topic page (lessons, notes via
signed links, save for revision, practice, previous/next), HLS video with resume and progress saving, practice with
feedback/hints/reports and a summary, tests and instructions, a full-screen exam player (port of the app's engine:
server-derived timer, autosave, offline retry, palette, mark for review, confirm with counts, auto-submit), results with
breakdowns and answer review, study plan (done/skip/refresh, last 7 days), progress with a score-trend chart, profile
and study preferences, notifications, saved items, purchases (refund requests) and support tickets.

**Also changed:** the API's playback response includes the lesson title, topic and completion; sign-in rate limits now
count per real visitor behind Render (True-Client-IP) and behind the web server (shared key); modal dialogs are named
by their title for screen readers.

**Verified:** 78 backend tests; 27 Playwright tests (16 new for the student site) covering registration and Hindi
carry-through, OTP sign-in with the test-code panel, redirects and httpOnly cookies, catalog filters, enrolment, notes,
bookmarks, simulated checkout, real playback of a local video (resume + completion), a practice run, a full exam to
review, resume after reload and offline recovery, preferences, plan, the progress chart (tooltip + table), notifications,
support replies, purchases, sign-out and account deletion. Desktop and phone screenshots reviewed, chart in light and
dark. Live: the API's rate-limit key was confirmed to follow the visitor and ignore forged headers.

**Live staging (verified 5 Nov 2026, after the owner set `API_BASE_URL` and synced the Blueprint):** a scripted student
journey through the website passed: register (httpOnly cookies), onboarding in Hindi, home with Hindi content, free
enrolment, exam autosave, submit and result, OTP request; token endpoints are not reachable through the proxy and
sign-out ends the session. Sign-in limits count per visitor through the website (the API keyed requests by the
visitor's address, not the web server's) and forged address headers were ignored. The temporary diagnostic endpoint
used for this was removed afterwards.

**Video on staging (5 Nov 2026):** sample lessons had placeholder IDs, so every lesson said it couldn't play. With sample
content on, they now use a public demo HLS stream (`Seed:SampleVideoUrl`, checked reachable with CORS `*`, H.264/AAC,
635 s); real lessons are never changed (backend test). A new Playwright test plays an HLS lesson through hls.js from a
cross-site "CDN" (the code path Bunny Stream uses).

**Not verified / known gaps:** Razorpay live checkout (needs keys), video from a real Bunny CDN (none configured), Hindi
copy review (study-plan reasons are still generated in English), no search page on the web yet, and a full page load
renders English first for Hindi users.

### SMS sign-in codes via MSG91 (5 Nov 2026)

**Built:** `Otp:Provider=msg91` sends the API's own code through MSG91 SendOTP (template, `mobile` with country code,
`otp`, `otp_length`, `otp_expiry`=5, `authkey` header), checked against MSG91's published OpenAPI and a live call to its
endpoint (errors come back as HTTP 200 with `type: "error"`, so the body decides). Startup fails if the key or template
is missing. A failed send removes the stored code so the student can ask again at once, and returns
`503 otp_send_failed` (translated on the website). The MSG91 client's request logging is removed because the code is in
the URL. **Verified:** 8 new backend tests with a fake MSG91 (request shape, error-in-200, 401, HTML 502, network
failure, full SMS sign-in, immediate retry after a failure, refusal to start without a key) and a check that the
no-codes-in-logs test fails without the safeguard. **Not verified:** a real SMS (needs the owner's DLT registration and
MSG91 account).

## Roadmap (estimates assume one full-stack dev + part-time Flutter dev, AI-assisted)

| Phase | Scope | Duration | Est. cost |
|---|---|---|---|
| 1b | ~~Next.js admin portal~~ **done** (see report above) | — | — |
| 1c | ~~Next.js student web~~ **done** (see report above) | — | — |
| 1d | ~~Flutter app~~ **done** (see report above); device QA and store setup remain under 1f | — | — |
| 1e | Integrations: MSG91, Razorpay live test mode, Bunny Stream signed playback, FCM, Sentry (staging deploy config **done**) | 1–2 weeks | ₹10–20k |
| 1f | QA on devices, accessibility pass, store submission | 1–2 weeks | ₹10–25k |
| 2 | PYQ papers, coupons & subscriptions, revision scheduling, richer analytics, bulk import UI, erasure/export jobs | 6–8 weeks | ₹1–1.5 lakh |
| 3 | Adaptive practice, AI-assisted explanations (SME-reviewed), offline/DRM, live classes, cohort percentiles, leaderboards | scoped later | — |

Remaining to a launchable MVP (Phases 1e–1f) ≈ **2–4 weeks, ₹20–45k** on top of the work already done, excluding
content production. See `07-cost-estimate.md`.
