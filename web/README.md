# LearnPath web: student website and admin portal

Next.js 16 (App Router, Cache Components) + TypeScript + Tailwind CSS v4, TanStack Query, react-hook-form.
Types for every API call are generated from the backend's OpenAPI spec.

## How it talks to the API

```
Browser ──► Next.js server ──► ASP.NET Core API
            /api/session/*     login, register, mobile OTP, logout, current user (sets httpOnly cookies)
            /api/v1/*          same-origin proxy that adds the Bearer token
```

- Tokens live only in httpOnly cookies (`lp_at` access, `lp_rt` refresh). Browser JavaScript never sees them.
- The proxy refreshes an expired access token. Refreshes are de-duplicated per refresh token because the API
  revokes every session if a rotated refresh token is reused. **Run one Next.js instance (or sticky sessions).**
- `/api/v1/auth/*` and the payment webhook are blocked in the proxy; state-changing requests must be same-origin.
- `src/proxy.ts` sends signed-out visitors to `/login` (student areas) or `/admin/login`, and signed-in visitors from
  `/` and `/login` to `/home`. Real authorisation is always enforced by the API; the portal hides navigation a role can't use.
- With `BFF_SHARED_KEY` (same value as the API's `Bff__SharedKey`) every API call carries the visitor's address, so
  sign-in limits count per visitor instead of per web server.

## Commands

```bash
npm install
npm run gen:api      # regenerate src/lib/api/schema.d.ts from ../docs/openapi.json
npm run dev          # http://localhost:3000 (student site) and /admin (needs the API on :5080)
npm run gen:messages # regenerate src/lib/i18n/messages.gen.ts from ../mobile/lib/l10n/app_*.arb
npm run build && npm start
npm run typecheck
npm run lint
npm run test:e2e     # needs `npm run build` first; starts its own API (:5180, database learnpath_e2e) and web (:3100)
```

When the API contract changes: run `../scripts/export-openapi.sh`, then `npm run gen:api`.

## Environment

| Variable | Default | Purpose |
|---|---|---|
| `API_BASE_URL` | `http://localhost:5080` | Backend API, server-side only |
| `NEXT_PUBLIC_BRAND_NAME` | `LearnPath` | Product name shown in the UI |
| `COOKIE_SECURE` | `true` in production | Set `false` only for plain-HTTP local testing |
| `BFF_SHARED_KEY` | — | 32+ character secret shared with the API (`Bff__SharedKey`); enables per-visitor sign-in limits |
| `CLIENT_IP_HEADER` | `true-client-ip` on Render | Header the hosting edge sets to the visitor's address |
| `NEXT_PUBLIC_PRIVACY_URL` | example.com | Privacy policy linked from the profile |

## Layout

```
src/app/page.tsx, login, register, onboarding   student landing and sign-in (mobile OTP or email)
src/app/(student)/...            student pages inside the site frame: home, learn, topics, watch, practice, tests,
                                 results, plan, progress, profile, notifications, bookmarks, purchases, support,
                                 doubts, topic quizzes (topics/[id]/quiz)
src/app/exam/[attemptId]         full-screen exam player (outside the frame)
src/app/admin/login              staff sign-in page
src/app/admin/(portal)/...       admin pages (thin server components; each wraps a client feature in <Suspense>)
src/app/api/session, api/v1      BFF route handlers
src/features/student/...         student screens; tests/exam-session.ts is the exam engine (port of the app's)
src/features/admin/...           screens: catalog, questions, tests, students, ops (doubts, orders, support, announcements, audit)
src/lib/i18n                     English/Hindi: strings generated from the app's ARB files + web-messages.ts
src/components/ui                design-system primitives (tokens in app/globals.css, light + dark)
src/lib                          API client + generated types, session/permissions, formatting
e2e/                             Playwright tests
```

### Next.js 16 notes for contributors
- Visited pages stay mounted but hidden (React `<Activity>`). Close transient UI in layout-effect cleanups (see
  `components/ui/dialog.tsx`), and don't rely on element ids being unique across pages: form labels wrap their control.
- Anything reading `params`, `searchParams` or `usePathname` for a dynamic route must sit inside `<Suspense>`.
- Read `node_modules/next/dist/docs/` before using an API you haven't used in this version.

### Student website notes
- **Strings** are shared with the mobile app: edit `mobile/lib/l10n/app_*.arb`, then `npm run gen:messages`. Website-only
  strings live in `src/lib/i18n/web-messages.ts`. Hindi text needs native-speaker review before launch.
- **Language** is stored in the `lp_lang` cookie and, once a student is set up, in their profile (the app uses it too).
  The API returns content in that language (`Accept-Language`). The server renders English first; the browser then
  switches, so a Hindi visitor can see English for a moment on a full page load.
- **Video** plays HLS through hls.js (native on Safari) from the API's signed stream URL; without a video CDN configured
  the lesson shows "can't be played". Progress saves every 10 s, on pause/end and when leaving.
- **Payments** open Razorpay Checkout; the API verifies the signature. With the API's simulated gateway (test servers),
  the site explains that instead of opening checkout.
- **Charts** use the `--chart-1` token through Tailwind classes (`stroke-chart-1`): Tailwind drops CSS variables that
  no class uses.
