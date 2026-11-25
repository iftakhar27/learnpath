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

### Razorpay test mode (5 Nov 2026)

**Changed:** startup now refuses real payments without Key Id / Key Secret / Webhook Secret or with a Key Id not starting
`rzp_`; Razorpay's error description is logged when order creation fails (never the secret); network failures map to
`502 payment_gateway_error`; Razorpay settings are managed in Render (`sync: false`). **Verified:** 5 new backend tests
against a fake Razorpay (order request: Basic auth, amount in paise, receipt; HMAC checkout and webhook signatures
against independently computed values incl. tampering; "Authentication failed" logged without the secret; startup
refusals). **Live (5 Nov 2026):** with the owner's test keys in Render, the API created real Razorpay test orders and the
webhook endpoint rejects unsigned calls; the owner paid for the paid sample course on the website through Razorpay
Checkout (test mode, Netbanking) and the course unlocked. **Still to check in the dashboard:** the payment shows as
Captured and the webhook delivery succeeded; the Android app's checkout on a device.

### Android first (7 Nov 2026)

Owner's call: focus on Android, not iOS, for now. `.github/workflows/android.yml` analyzes, tests and builds the APK on
every push to `main` that touches `mobile/` (artifact **LearnPath-android-apk**, 30 days); the iOS workflow now runs only
on demand.

**Bell badge (owner's report: the count only appeared after opening the bell):** `GET /notifications/unread-count`; the
app re-checks it every 30 seconds while on screen and on returning to the foreground (paused in the background), the
website every 30 seconds while the tab is visible and on focus; reading notifications updates it at once. Alerts while
the app is closed need push notifications (FCM), not built yet. Verified by an app test (polling, pause and resume) and
a browser test (the badge updates without reloading).

### Lesson notes upload (25 Nov 2026)

- **Before:** notes inside courses only pointed at a file host that was never set up, so students couldn't open them.
- **Now (admin course editor):** "Notes (PDF)" resources take an uploaded file (PDF, or a PNG/JPEG image; up to
  20 MB), kept in the same file store as free material.
  - The editor shows the current file and its size. A new file replaces it.
  - The outline marks notes without a file ("No file yet"), and publishing them is refused until there is one.
  - An external file host key is still possible, under "Use a file on an external host instead".
- **Students (website and app):** opening notes works as before (a new tab or the phone's browser). Access to the lesson
  is checked first: paid notes stay locked until the course is owned, and free previews stay open. The notes then load
  through a short-lived signed link that needs no sign-in token.
- **Samples:** the two sample lessons' notes (Dimensional formulae summary, Friction formula sheet) now have real
  one-page PDFs, so notes open on the live site.
- **Verified:**
  - 2 backend tests: sample notes open and paid ones are locked until enrolled; staff upload, replace and remove, with
    the publish check.
  - 2 browser tests: a student opens sample notes and gets a PDF; staff build a course with uploaded notes and a student
    opens them.
  - 1 app test: notes open at the API's address.
  - All suites green: 130 backend, 49 browser, 39 app.
  - Free material moved onto the same signed-link code.

### Free study material (25 Nov 2026)

- **Students (website `/materials`, app menu, Learn tab, guest home):** "Free study material".
  - Notes, formula sheets, mind maps, previous year and sample papers, filtered by subject (or general), class and
    type.
  - Anyone can browse the list. Downloading needs a free account, so guests see "Sign up free to download"; it brings
    in sign-ups.
  - A download opens in the browser's PDF viewer (or downloads, on phones) through a signed link valid 10 minutes, so
    it works without the app passing its sign-in token. Links to other sites (e.g. Google Drive) open directly.
  - Downloads are counted.
- **Admin ("Free material"):**
  - Add material with English and optional Hindi title and description, type, subject, class and draft/published.
  - The source is an uploaded file (PDF, PNG or JPEG, checked by its content rather than its name; up to 20 MB,
    `Materials__MaxUploadMegabytes`) or an https link.
  - Edit, replace the file and delete. The page shows how much file storage is used.
- **Storage:** uploaded files are kept in the database for now (no extra service or account; backed up with it), behind
  one interface (`IFileStore`) so an object store such as Cloudflare R2 can take over when the library grows. Render's
  free database is 1 GB in total, so keep an eye on "Files stored" and prefer links for very large files.
- **Samples:** three one-page PDFs (Units and Measurement formula sheet, Laws of Motion notes, Cell key points) and a
  link to NCERT's official textbook site, added with the sample content.
- **Verified:**
  - 5 backend tests: list and filters, sign-in and signed links (altered or expired refused), well-formed sample PDFs,
    admin upload checks and file cleanup, size limit.
  - 1 browser test (upload, refuse a fake PDF, student download returns the PDF, guest prompt, download counted,
    delete) and 2 app tests.
  - The sample PDFs open in two PDF readers.
  - Screenshots on phone in light and dark, and of the admin form.
  - Found and fixed along the way:
    - App cards merged their Download button into one screen-reader item.
    - The guest button overflowed on narrow screens.
  - All suites green: 128 backend, 48 browser, 39 app.
- **Android APK:** a phone showed "There was a problem while parsing the package" when the APK was opened straight from
  WhatsApp. The file was intact; some phones' installers can't read an APK handed over by WhatsApp. Saving it and
  opening it from the Files app works. A smaller 64-bit-only build (21 MB instead of 61 MB) is now sent too.

### Guest browsing and WhatsApp links (25 Nov 2026)

- **Guest browsing (app and website):** look around without an account.
  - **App:** "Explore without an account" on the welcome screen.
    - Home shows a guest card (create free account / sign in), the NCERT subject grid, courses to explore, and WhatsApp.
    - Learn, course pages, subject books and chapters work as normal.
    - Practice, Tests and Profile explain what a free account adds. Profile keeps language, night mode, WhatsApp and the
      privacy policy.
    - Anything else (a lesson, practice, a test, flash cards) opens the sign-up screen, with Back to keep browsing.
    - The guest side menu has browse, WhatsApp, settings and sign in.
    - Guest mode is remembered until the student signs in or signs out. Paid courses open the website without a
      sign-in code.
  - **Website:** the course catalogue (`/learn`), course pages and NCERT subject pages (`/library/...`) are public.
    - Guests get a light shell: a banner, Sign in / Create free account in the header (a bar on phones), and a guest
      menu.
    - Joining or buying sends them to sign up or sign in and back (to checkout for paid courses).
    - Lessons, practice and tests still need sign-in, and a guest asked to sign in isn't told their "session expired".
    - The landing page has "Explore without an account".
- **WhatsApp:**
  - "Chat with us on WhatsApp" opens a chat with a ready message, and "Join our WhatsApp channel" opens the channel.
    Both are set on the server (`Contact__WhatsAppNumber`, `Contact__WhatsAppChannelUrl`; see 10-deploy-render), so
    they change without an app release.
  - The server only accepts a phone number and WhatsApp links, so a typo can't send students to another site.
  - They appear in the side menu (both apps), Help & support, the guest home and Profile (app), and on the landing page
    (website, a round chat button and a channel link).
  - "Share on WhatsApp" for courses (with the course's website link) and "Invite friends on WhatsApp" work without
    settings.
- **Verified:**
  - 2 backend tests (settings checked and normalised), 2 browser tests (guest browsing end to end; signed-in WhatsApp
    links) and 1 app test (the whole guest flow with WhatsApp links).
  - Screenshots of the guest screens on phone in light and dark.
  - Found and fixed along the way:
    - Next.js's Cache Components needs the session check inside the loading boundary.
    - "Create free account" appeared twice on phones.
    - The WhatsApp button covered the landing page's last line.
  - All suites green: 123 backend, 47 browser, 37 app.

### Custom test generator and flash cards (25 Nov 2026)

- **Custom test generator (app and website):** Tests → "Create a custom test".
  - The student picks NCERT chapters (Physics, Chemistry, Biology; Class 11 and 12), the number of questions (10, 20,
    30, 45, 90, 180, or all there are), the difficulty, and whether to skip questions already answered correctly.
  - Each chapter shows how many questions the student can use and how many more are in paid courses. Chapters only in
    paid courses can't be picked.
  - The test is private to the student. Questions are spread across the chosen chapters, with one section per subject
    in NEET order, one minute per question (at least 5), and NEET marking (+4 / −1).
  - It runs in the existing exam player with the same results, review and analytics, and is listed under
    "My custom tests".
  - Custom tests never appear in the shared test list, search, study plans or the admin test list. Up to 20 a day per
    student.
- **Flash cards (app and website):** a deck per NCERT chapter, free for every signed-in student.
  - Reached from Practice, the side menu and the chapter page.
  - Tap (or Space) flips a card. The student answers "Got it" or "Still learning" with the buttons, a swipe (app) or the
    arrow keys (website).
  - Spaced repetition: "got it" brings a card back in 1, 3, 7, 16, then 35 days; "still learning" brings it back
    straight away. Three in a row counts as mastered.
  - Decks show cards due and mastered, and a session ends with a summary. Reviews count towards the streak.
- **Admin:** a new "Flash cards" page.
  - Lists all 79 chapters with their card counts.
  - Per chapter: add a card (English, optional Hindi, draft or published), paste many at once ("front | back" per line,
    problems reported by line), edit and delete.
- **Starter decks:** 43 cards for four chapters (Units and Measurement, Laws of Motion, Some Basic Concepts of
  Chemistry, Cell: The Unit of Life), added with the sample content. They use standard textbook facts but should be
  reviewed by a subject expert. Hindi isn't written yet; English shows instead.
- **Verified:**
  - 5 backend tests: options, privacy and unseen-first selection, errors and the daily limit, spaced repetition,
    admin writing and pasting.
  - 4 browser tests (the last checks every student page needs sign-in) and 2 app tests.
  - Screenshots in light and dark on phone, plus the admin editor.
  - Found and fixed along the way:
    - A "Hard only" request in a free chapter wrongly said "buy a course".
    - Deck tiles in the app were announced as progress bars to screen readers.
    - "Add card" and "Add cards" were too alike in the admin editor.
    - After the deploy, `/flashcards` didn't send signed-out visitors to sign in (the gate's path list missed it).
  - All suites green: 121 backend, 45 browser, 36 app.

### Intro slides, side menu and night mode (25 Nov 2026)

- **Intro slides (app):** four slides on first launch, before the sign-in choices: NCERT chapters, NEET-style
  practice and tests, ask a doubt, plan and progress. They have Skip, Next / Get started, swipe, and the English/हिंदी
  switch. They are shown once per phone. Students who are already signed in never see them, even after signing out.
  The website keeps its landing page instead.
- **Side menu (app and website):** opened from the ☰ button on the tab screens (app) or the header (website), and it
  covers the whole screen. It holds:
  - the student's name and target exam, linking to Profile;
  - **Study:** home, subjects & courses, practice, tests, study plan, progress, saved;
  - **Help:** ask a doubt, notifications with the unread count, purchases, support;
  - **Settings:** a night-mode switch, language, study preferences, change password (app, email accounts only),
    privacy policy;
  - **Sign out.**
- **Night mode:** **Auto** (follow the device, the default), **Light** or **Dark**. The menu has a one-tap switch and
  Profile → Appearance has all three. The choice is kept on the phone (app) or in the browser (website).
  - On the website, the choice is applied before the page paints, so there is no white flash. It syncs across tabs,
    and the phone's address-bar colour follows it.
  - In the app, subject colours use lighter shades at night so small text on them stays readable.
- **Verified:**
  - 5 new app tests: intro once, Skip, signed-in students skip it, menu, theme restored.
  - 1 new browser test: menu links, closing, night mode from the menu, reload, Profile Auto/Light/Dark against a dark
    device, sign out from the menu.
  - Screenshots of every new screen in light and dark, at phone and desktop widths.
  - All suites green: 116 backend, 41 browser, 34 app.

### Subject grid and NCERT chapters (25 Nov 2026)

Browse by book, after a review of a competitor's app for ideas (structure only; our own design and icons): the home
and Learn screens (website and app) open with a **subject grid**, Physics, Chemistry, Biology × Class 11 and 12, with
each tile's progress ring, "x of y chapters ready" and a "Continue: <chapter>" shortcut. A tile opens the full **NCERT
chapter list** (rationalised 2023-24 syllabus, 79 chapters; ones without lessons say "coming soon"; switch class or
subject from the page), and a chapter shows **Videos / Notes / Practice** gathered from every published course that
teaches it (locked lessons and a "view course" link when it's only in a paid course; free previews still play).
Course chapters link to NCERT chapters automatically by subject and title (existing ones once, new ones on creation);
admins can change or remove the link in the syllabus editor. Practice works per NCERT chapter. Hindi chapter titles
are not filled in yet (English shows). Also: **Next.js 16.4.3 → 16.4.6** (the build now refuses versions with known
advisories; 16.4.6 fixes seven, including response disclosure when forwarding fetched responses, which the website
does). **Verified:** 4 backend tests, 1 browser test (grid → book → chapter tabs → practice; locked chapter; book
switching), 1 app test; all suites green (116 / 40 / 29).

### Forgot password by email (9 Nov 2026)

"Forgot password?" on the website's student and admin sign-in pages and on the app's email sign-in emails a link to the
website's **Choose a new password** page: single use, valid 30 minutes, only its hash stored, at most one email a minute
and five an hour per account; the answer never reveals whether an email has an account; resetting signs out every
device. Email goes through Brevo's HTTPS API (`Email:Provider=brevo`; Render's free plan blocks SMTP); staging defaults
to `log` (emails in the API's Logs and a staff-only test outbox), and Production refuses `log`. The email is in English
with a Hindi line. **Verified:** 3 backend tests, 2 browser tests (sign-in → forgot → link → new password → sign in →
link refused the second time; admin sign-in link), 1 app test. **To do by the owner:** a Brevo account and key (steps in
`10-deploy-render.md`).

### Password reset by staff, and change password (9 Nov 2026)

There was no way to recover a forgotten password. Now **Admin → Students → (student) → Reset password** sets a
temporary password (3 groups of 4 easy-to-read characters, shown once with a Copy button), signs the account out
everywhere, notifies the student and records it in the audit log. Support and super admins can reset students; only a
super admin can reset staff; mobile-number accounts have no password. Students change it under **Profile → Change
password** in the app and on the website; changing it now keeps the current device signed in and signs out the others
(before, it signed out every device including the current one). **Verified:** 2 backend tests (+1 assertion in the
session tests), 1 browser test (portal reset → sign in with the temporary password → change it), 1 app test
(validation, wrong current password, success). A self-service "Forgot password?" by email still needs an email service.

### Buy on website (9 Nov 2026)

Owner's choice: the app has no checkout; courses are bought on the website. On a paid course the app shows **Buy on
website** (and a short note): it gets a one-time sign-in code (2 minutes, single use, only its hash stored), opens the
website's `/handoff` page, which signs the student in as their browser (never asking to sign the app out) and opens the
course with Razorpay checkout already started; when the student returns to the app the course refreshes and shows as
unlocked, and the website says "go back to the app". The Razorpay SDK was removed from the app. `WEB_PURCHASE=false`
hides the button for a store build if Google or Apple object to pointing to outside payment (their policies restrict
it for digital content). **Verified:** 2 backend tests (single use, expiry, sign-in required, one-browser rule with
take-over), 1 browser test (handoff → course with checkout started → back-to-app note → link refused the second time),
1 app test (opens the right link, no payment calls, unlocks on return).

### iOS build (6 Nov 2026)

**Built:** `.github/workflows/ios.yml` builds the app on GitHub's macOS runners (Xcode 26.3, Swift 6.2.4; the newest
Xcode on the image is selected because `video_player_avfoundation` needs Swift 6.2) on every push to `main` that touches
`mobile/`: an unsigned iPhone `.ipa` (9 MB) and an iOS Simulator build, kept as workflow artifacts for 30 days. App id
`com.learnpath.app` on both platforms. **Verified:** both builds compile and package (first green run 42172622581).
**Not verified:** running on an iPhone or the Simulator (needs the owner's device or a Mac); signed TestFlight builds
need the Apple Developer Program.

### Home-screen app and one person per account (7 Nov 2026)

**Home-screen app:** web app manifest (standalone, starts at Home), LearnPath icons (incl. Apple touch and maskable),
iPhone home-screen metadata and safe-area padding, so "Add to Home Screen" opens full screen like an app.

**One person per student account (owner's choice: ask, then take over; one phone + one browser):** a `UserSessions`
table records each signed-in device (slot, name, hashed device id, last active, end reason); refresh tokens and access
tokens carry the session. A sign-in on another device of the same kind gets `409 session_active` with the other device
and when it was last active; confirming (`takeOver`) ends that session, and the replaced device is refused on its very
next request (even public pages) with `401 session_replaced`, so it can explain why it was signed out. The same device
(its own random id: an httpOnly cookie on the web, secure storage in the app) signs in again without a prompt; signing
out frees the slot; password changes end every session; staff accounts are exempt. The website and the app show a
"Continue here?" dialog and the signed-out explanation (English and Hindi, shared strings). **Verified:** 7 backend
tests, 2 browser tests (two real browser contexts: ask, cancel, take over, explanation; phone + computer together),
2 app tests; all suites green (99 backend, 32 browser, 22 app). **To check:** on real phones after installing the new
app builds.

### Ask a doubt and topic quizzes (7 Nov 2026)

**Ask a doubt:** students ask a teacher from a topic, a lesson, a practice or test question, a quiz answer, or their
"My doubts" page (website and app; Home and Profile link to it). Each doubt is a thread: the teacher's reply notifies the
student, who can follow up (re-opens it) or mark it solved. Doubts about paid topics need the course; 10 new doubts a
day per student. Teachers, content admins and super admins answer under **Admin → Doubts** (open ones oldest first,
search, the student and what the doubt is about, reply and/or resolve); the Dashboard counts doubts waiting.

**Topic quizzes:** every topic offers a short quiz (default 5 questions, 60% to pass; set per topic in the syllabus
editor, 0 turns it off) drawn from its published questions, unseen ones first. It is highlighted once all the topic's
lessons are done, and finishing the last lesson prompts for it. Answers are scored together on submit, with the score,
pass/fail, every answer and explanation, "Ask a doubt" per question, retake and next topic; quiz answers count towards
accuracy and weak topics like practice. Existing topics got the default quiz.

**Verified:** 6 backend tests (thread and notifications, paid-topic access and privacy, daily limit, quiz scoring and
idempotent submit, admin settings, completion prompt), 3 browser tests (topic → doubt → admin reply → follow-up →
resolved; covered topic → quiz with an unanswered question → result → card shows best score; admin quiz settings),
2 app tests (quiz confirm and result, doubts list and validation); all suites green (105 backend, 35 browser, 24 app).
**Built:** the Android release APK (60 MB, staging API). **Not verified:** running it on a phone.
**Not included yet:** photos in doubts (needs file storage), assigning doubts to particular teachers.

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
