# Deploying to Render (staging)

This creates a **staging** setup: the API, a PostgreSQL database and the admin portal, in Render's Singapore region, loaded with the
sample content. Payments are simulated and sign-in codes for mobile numbers aren't sent by SMS (staff see them in the
admin portal), so it is for testing the apps, not for real
students. Everything is defined in [`render.yaml`](../render.yaml).

## One-time setup (about 10 minutes)

1. Go to <https://dashboard.render.com> and sign up or sign in **with GitHub**.
2. When asked, allow Render to access the `iftakhar27/learnpath` repository.
3. Click **New → Blueprint**.
4. Choose the `learnpath` repository and the branch **`main`**.
5. Render reads `render.yaml` and shows two resources: `learnpath-db` (PostgreSQL) and `learnpath-api` (web service).
   It asks for one value:
   - **`Seed__AdminEmail`**: the email you want for the first admin account.
6. Click **Apply**. The first build takes 5–10 minutes (Docker build, then database migrations on start-up).
7. Open the `learnpath-api` service. When its status is **Live**, copy its URL, e.g.
   `https://learnpath-api.onrender.com`, and check `https://<your-url>/health` shows `Healthy`.
8. Admin password: in the service, open **Environment** and reveal **`Seed__AdminPassword`** (Render generated it).

Send the URL to Claude to rebuild the mobile app against it.

## Admin portal (second service)

`render.yaml` also defines `learnpath-admin`, the Next.js web app (Node 22, built from `web/`): the **student website**
at `/` and the **admin portal** at `/admin`. Its server calls
the API, so login tokens stay in httpOnly cookies and the API needs no CORS settings.

1. If you created the Blueprint before this service was added: open **Blueprints → learnpath** in Render and click
   **Sync** (Render may also sync by itself after the push). A new service `learnpath-admin` appears.
2. Set **`API_BASE_URL`** to the API service's URL, without `/api/v1`, e.g. `https://learnpath-api-xxxx.onrender.com`.
   Render asks for it when the Blueprint is first created; otherwise open `learnpath-admin` → **Environment**, add it,
   and choose **Save, rebuild, and deploy**. Until it is set, the login page says the portal isn't connected to the API.
3. Open `https://<admin-service>.onrender.com/admin` and sign in with `Seed__AdminEmail` / `Seed__AdminPassword`.
   To give someone else access, have them register (in the app), then open them under **Students** and add a role
   (e.g. Teacher or ContentAdmin) instead of sharing the admin login.

Notes: the web server passes each visitor's address to the API (shared key `Bff__SharedKey` → `BFF_SHARED_KEY`, set
by the Blueprint; **sync the Blueprint once** so both services get it). Without it, every website sign-in would share
the web server's limit (20 a minute). The free portal also sleeps when idle, so the first page can take up
to a minute (longer if the API is asleep too).

## What you get

| Item | Value |
|---|---|
| API base URL | `https://<service>.onrender.com/api/v1` |
| API docs (staging only) | `https://<service>.onrender.com/swagger` |
| Student website | `https://<admin-service>.onrender.com/` |
| Admin portal | `https://<admin-service>.onrender.com/admin` |
| Admin account | `Seed__AdminEmail` / `Seed__AdminPassword` |
| Students | register in the app with email, or with a mobile number: no SMS is sent on staging, so the code appears on the admin portal's **Dashboard → Test sign-in codes** (and in the API service's **Logs**) |
| Payments | simulated until Razorpay test keys are set (see **Payments** below); then real test-mode checkout |

## Payments (Razorpay)

Staging starts with simulated payments (`Razorpay__UseFakeGateway=true`): the Buy button explains that checkout can't
open, and admins grant access instead. To take **test-mode** payments (no real money):

1. In the [Razorpay dashboard](https://dashboard.razorpay.com), switch to **Test Mode** (toggle at the top).
2. **Account & Settings → API Keys → Generate Test Key.** Copy the **Key Id** (`rzp_test_...`) and the **Key Secret**
   (shown once).
3. **Account & Settings → Webhooks → Add New Webhook:**
   - URL: `https://<api-service>.onrender.com/api/v1/payments/webhooks`
   - Secret: make up a long random value and keep it for step 5
   - Active events: `payment.captured`, `payment.failed`, `order.paid`
4. **Account & Settings → Payment Capture:** make sure payments are captured **automatically** (an authorised but
   uncaptured payment is refunded by Razorpay after a few days).
5. **Render → learnpath-api → Environment:** set `Razorpay__UseFakeGateway` = `false`, `Razorpay__KeyId`,
   `Razorpay__KeySecret` and `Razorpay__WebhookSecret` (the value from step 3), then **Save and deploy**. The API refuses
   to start if any of them is missing or the Key Id doesn't start with `rzp_`, so a mistake shows in the deploy log.
6. Try it as a student: open a paid course → **Buy** → Razorpay's checkout opens with a "Test Mode" label. Pay with
   UPI ID `success@razorpay`, or card `4111 1111 1111 1111` (any future expiry, any CVV) and choose **Success** on the
   test bank page. The course unlocks straight away; the payment and the webhook delivery appear in the dashboard.

Access is granted only after the API verifies Razorpay's signature (from the checkout, or from the webhook if the
browser closed early); both paths are idempotent. If order creation fails, the API logs Razorpay's reason
(`Razorpay order creation failed (401): Authentication failed` for wrong keys). For real money later, repeat with
**Live Mode** keys (`rzp_live_...`), a live webhook, and `ASPNETCORE_ENVIRONMENT=Production`.

## SMS sign-in codes (MSG91)

Staging starts with `Otp__Provider=log`: no SMS is sent and codes show on the admin **Dashboard → Test sign-in codes**.
To send real SMS:

1. **DLT registration (India, required by TRAI):** register your business as a Principal Entity on a DLT portal
   (e.g. Jio, Vodafone Idea, Airtel), register a 6-letter **sender ID** (header) and an OTP **content template**, e.g.
   `{#var#} is your LearnPath sign-in code. It is valid for 5 minutes. Do not share it with anyone.` Approval usually takes
   a few days to two weeks.
2. **MSG91:** create an account, add the DLT entity and sender ID, then in **OTP → Templates** create a template whose
   text matches the approved DLT template with `##OTP##` in place of the variable, and enter its DLT template ID. Copy the
   MSG91 **template ID** and, under your account, the **auth key**. Add credit to the account.
3. **Render → learnpath-api → Environment:** set `Otp__Provider` = `msg91`, `Otp__Msg91__AuthKey` = your auth key,
   `Otp__Msg91__TemplateId` = the template ID, then **Save and deploy**. The API refuses to start with `msg91` if either
   value is missing, so a typo shows up in the deploy log rather than as silent failures.
4. Try it: "Continue with mobile number" in the app or on the website. The code arrives by SMS (valid 5 minutes).

The API still generates, stores (hashed) and checks every code; MSG91 only delivers it. If MSG91 rejects a send (no
credit, wrong template, outage) the student sees "We couldn't send the code right now" and can ask again at once, and the
reason is in the API's logs (`MSG91 SendOTP failed`). Codes are never written to the logs.

## Video lessons

On staging the sample lessons play a **public demo stream** (Big Buck Bunny, an open-licence film, from Mux's public
HLS test streams), so playback can be tried before a video host is connected. This only applies to the sample
lessons' placeholder IDs while `Seed__SampleContent=true`; set `Seed__SampleVideoUrl` to another HLS/MP4 URL or to an
empty value to change or turn it off.

To play your own videos, connect **Bunny Stream** (the integration the API is built for):

1. Create a Bunny Stream library and upload a video. Note the **library ID** and the **video ID** (a GUID).
2. In the library's settings, turn on **token authentication** for the pull zone (CDN) and note the **CDN hostname**
   (e.g. `vz-xxxx.b-cdn.net`) and the **token key**.
3. In Render, open **learnpath-api → Environment** and add `Video__LibraryId`, `Video__CdnHost`, `Video__CdnTokenKey`
   (and `Video__TokenKey` for the embed player), then **Save and deploy**.
4. In the admin portal, open the topic under **Courses & syllabus**, add a video lesson and paste the Bunny video ID
   into **Video ID at the streaming provider**.

The API signs a short-lived stream URL for each student with access; the website plays it with hls.js (natively on
Safari) and the app with the phone's player.

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
- `Otp__Provider=msg91` with `Otp__Msg91__AuthKey` and `Otp__Msg91__TemplateId` (see above). The admin portal's test
  sign-in codes panel exists only while `Otp__Provider=log`, which Production refuses.
- `Seed__SampleContent=false`, a paid database plan with backups, and the Bunny Stream settings for video
  (`Video__LibraryId`, `Video__TokenKey`, `Video__CdnHost`, `Video__CdnTokenKey`).
- `Database__MigrateOnStartup=false` with migrations run as a release step, and a custom domain.
- The items in [08-security-and-operations.md](08-security-and-operations.md).
