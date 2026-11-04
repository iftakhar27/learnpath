# Deploying to Render (staging)

This creates a **staging** setup: the API, a PostgreSQL database and the admin portal, in Render's Singapore region, loaded with the
sample content. Payments are simulated and OTP codes go to the server log, so it is for testing the apps, not for real
students. Everything is defined in [`render.yaml`](../render.yaml).

## One-time setup (about 10 minutes)

1. Go to <https://dashboard.render.com> and sign up or sign in **with GitHub**.
2. When asked, allow Render to access the `iftakhar27/learnpath` repository.
3. Click **New → Blueprint**.
4. Choose the `learnpath` repository and the branch **`mobile-app`** (switch to `main` once that branch is merged).
5. Render reads `render.yaml` and shows two resources: `learnpath-db` (PostgreSQL) and `learnpath-api` (web service).
   It asks for one value:
   - **`Seed__AdminEmail`**: the email you want for the first admin account.
6. Click **Apply**. The first build takes 5–10 minutes (Docker build, then database migrations on start-up).
7. Open the `learnpath-api` service. When its status is **Live**, copy its URL, e.g.
   `https://learnpath-api.onrender.com`, and check `https://<your-url>/health` shows `Healthy`.
8. Admin password: in the service, open **Environment** and reveal **`Seed__AdminPassword`** (Render generated it).

Send the URL to Claude to rebuild the mobile app against it.

## Admin portal (second service)

`render.yaml` also defines `learnpath-admin`, the Next.js admin portal (Node 22, built from `web/`). Its server calls
the API, so login tokens stay in httpOnly cookies and the API needs no CORS settings.

1. If you created the Blueprint before this service was added: open **Blueprints → learnpath** in Render and click
   **Sync** (Render may also sync by itself after the push). A new service `learnpath-admin` appears.
2. Set **`API_BASE_URL`** to the API service's URL, without `/api/v1`, e.g. `https://learnpath-api-xxxx.onrender.com`.
   Render asks for it when the Blueprint is first created; otherwise open `learnpath-admin` → **Environment**, add it,
   and choose **Save, rebuild, and deploy**. Until it is set, the login page says the portal isn't connected to the API.
3. Open `https://<admin-service>.onrender.com/admin` and sign in with `Seed__AdminEmail` / `Seed__AdminPassword`.
   To give someone else access, have them register (in the app), then open them under **Students** and add a role
   (e.g. Teacher or ContentAdmin) instead of sharing the admin login.

Notes: all portal sign-ins reach the API from the portal server's address, so they share one sign-in rate limit
(20 a minute by default; fine for a small staff). The free portal also sleeps when idle, so the first page can take up
to a minute (longer if the API is asleep too).

## What you get

| Item | Value |
|---|---|
| API base URL | `https://<service>.onrender.com/api/v1` |
| API docs (staging only) | `https://<service>.onrender.com/swagger` |
| Admin portal | `https://<admin-service>.onrender.com/admin` |
| Admin account | `Seed__AdminEmail` / `Seed__AdminPassword` |
| Students | register in the app with email; mobile OTP codes appear in the service **Logs** |
| Payments | simulated: checkout cannot complete. Grant course access from the admin portal (Students → Grant course access) or the API |

## Costs and limits on the free plans

- **Free web service:** sleeps after ~15 minutes without traffic. The next request wakes it in ~30–60 seconds, so the
  app may show a network error once; try again. Upgrade to **Starter** (USD 7/month) to stay on.
- **Free PostgreSQL:** expires after 30 days. Upgrade to **Basic-256mb** (about USD 6/month) to keep the data.
- Free web services share 750 instance hours a month per workspace. Two services that sleep when idle fit easily;
  two that are busy around the clock would not (Render suspends free services for the rest of the month).
- Change plans in each resource's **Settings → Instance Type**, or edit `plan:` in `render.yaml`.

## Updating

`autoDeploy: true` redeploys on every push to the configured branch, but only the service whose files changed: the API
for `src/`, `Dockerfile` or `.dockerignore`; the portal for `web/`. Changes to `mobile/` and `docs/` redeploy nothing.
Migrations run on start-up (`Database__MigrateOnStartup`), so schema changes apply automatically.

## Before using this for real students (production)

Create a separate service with `ASPNETCORE_ENVIRONMENT=Production`. The API refuses to start in Production with the
simulated gateway or logged OTPs, so you will need:

- Razorpay live keys: `Razorpay__UseFakeGateway=false`, `Razorpay__KeyId`, `Razorpay__KeySecret`,
  `Razorpay__WebhookSecret`, and the webhook URL `https://<api>/api/v1/payments/webhooks` registered in Razorpay.
- `Otp__Provider=disabled` until an SMS provider (MSG91 with a DLT-approved template) is implemented.
- `Seed__SampleContent=false`, a paid database plan with backups, and the Bunny Stream settings for video
  (`Video__LibraryId`, `Video__TokenKey`, `Video__CdnHost`, `Video__CdnTokenKey`).
- `Database__MigrateOnStartup=false` with migrations run as a release step, and a custom domain.
- The items in [08-security-and-operations.md](08-security-and-operations.md).
