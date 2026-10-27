# LearnPath web (admin portal; student web comes next)

Next.js 16 (App Router, Cache Components) + TypeScript + Tailwind CSS v4, TanStack Query, react-hook-form.
Types for every API call are generated from the backend's OpenAPI spec.

## How it talks to the API

```
Browser ──► Next.js server ──► ASP.NET Core API
            /api/session/*     login, logout, current user (sets httpOnly cookies)
            /api/v1/*          same-origin proxy that adds the Bearer token
```

- Tokens live only in httpOnly cookies (`lp_at` access, `lp_rt` refresh). Browser JavaScript never sees them.
- The proxy refreshes an expired access token. Refreshes are de-duplicated per refresh token because the API
  revokes every session if a rotated refresh token is reused. **Run one Next.js instance (or sticky sessions).**
- `/api/v1/auth/*` and the payment webhook are blocked in the proxy; state-changing requests must be same-origin.
- `src/proxy.ts` sends signed-out visitors to `/admin/login`. Real authorisation is always enforced by the API;
  the portal hides navigation a role can't use.

## Commands

```bash
npm install
npm run gen:api      # regenerate src/lib/api/schema.d.ts from ../docs/openapi.json
npm run dev          # http://localhost:3000/admin (needs the API on :5080)
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

## Layout

```
src/app/admin/login              sign-in page
src/app/admin/(portal)/...       pages (thin server components; each wraps a client feature in <Suspense>)
src/app/api/session, api/v1      BFF route handlers
src/features/admin/...           screens: catalog, questions, tests, students, ops (orders, support, announcements, audit)
src/components/ui                design-system primitives (tokens in app/globals.css, light + dark)
src/lib                          API client + generated types, session/permissions, formatting
e2e/                             Playwright tests
```

### Next.js 16 notes for contributors
- Visited pages stay mounted but hidden (React `<Activity>`). Close transient UI in layout-effect cleanups (see
  `components/ui/dialog.tsx`), and don't rely on element ids being unique across pages: form labels wrap their control.
- Anything reading `params`, `searchParams` or `usePathname` for a dynamic route must sit inside `<Suspense>`.
- Read `node_modules/next/dist/docs/` before using an API you haven't used in this version.
