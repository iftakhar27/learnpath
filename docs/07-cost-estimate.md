# MVP Budget & Running Costs (INR, estimates as of late 2026 — verify current vendor pricing)

## One-time build (lean, AI-assisted, 1 full-stack dev + 1 part-time Flutter dev + 1 designer/QA, ~12–14 weeks)

| Item | Low | High | Notes |
|---|---|---|---|
| Backend + admin API | ₹30,000 | ₹60,000 | Much of it exists in this repo; remaining: integrations, hardening |
| Admin portal (Next.js) | ₹20,000 | ₹40,000 | |
| Student web (Next.js) | ₹20,000 | ₹40,000 | |
| Flutter app (Android + iOS) | ₹40,000 | ₹80,000 | |
| UI design system & screens | ₹10,000 | ₹25,000 | |
| QA on devices, bug fixing | ₹10,000 | ₹25,000 | |
| Apple developer account | ₹8,000 | ₹8,000 | USD 99/yr |
| Google Play account | ₹2,100 | ₹2,100 | USD 25 one-time |
| Legal: privacy policy, terms, refund policy review | ₹5,000 | ₹20,000 | DPDP Act, consumer protection |
| **Total build** | **≈ ₹1.45 lakh** | **≈ ₹3.0 lakh** | |

**Not included:** content creation. One SME producing a chapter (videos + notes + 100 questions) costs roughly
₹3,000–₹15,000 depending on quality. A single subject for one exam is 20–30 chapters. **Content is likely to cost more
than software** — start with 1 exam × 1–2 subjects.

## Monthly running cost (first ~1,000 active students)

| Service | ₹/month | Notes |
|---|---|---|
| App server (2 vCPU / 4 GB VM, Mumbai/Bangalore) | 1,500–3,000 | |
| Managed PostgreSQL (smallest tier) + backups | 1,200–2,500 | Or self-host on the VM to start (₹0, more risk) |
| Video hosting & streaming (Bunny Stream) | 1,000–5,000 | ~₹0.4–0.8/GB delivered; 1,000 students × 3 GB ≈ 3 TB |
| Object storage + CDN for PDFs | 200–800 | |
| OTP SMS (MSG91, ~₹0.20–0.25/SMS) | 500–2,000 | Prefer email/password + OTP fallback |
| Transactional email | 0–800 | Free tiers usually suffice |
| Firebase Cloud Messaging | 0 | |
| Error monitoring (Sentry free tier) | 0 | |
| Domain + DNS | ~100 | |
| Razorpay | ~2% per transaction | Not a fixed cost |
| **Total** | **≈ ₹4,500–14,000** | Grows mostly with video bandwidth |
