# Product Requirements Document — LearnPath (working codename)

> The brand name is configurable (`Branding:ProductName` in backend settings, `NEXT_PUBLIC_BRAND_NAME` on web,
> a build-time constant in Flutter). "LearnPath" is only a placeholder codename used in code and docs.

## 1. Problem

Indian students preparing for NEET, JEE Main/Advanced and CBSE/state boards juggle videos, notes, question banks and
mock tests across several apps. Affordable options rarely connect *what you studied* with *how you performed* and
*what to do next*.

## 2. Goal of the MVP

A real student can go: **register → pick exam → learn (video + notes) → practice → take a timed test → see a correct
result → review mistakes → follow a study plan → buy a course**, on Android, iOS and the web, in English or Hindi. Staff
can run all of this from an admin portal without a developer.

## 3. Users

| Persona | Needs |
|---|---|
| Student (Class 11–12, droppers, Class 9–10 boards) | Structured syllabus, short videos, exam-like practice, honest feedback, cheap |
| Parent/guardian | Visibility into progress; consent for minors |
| Teacher / SME | Author questions, notes, tests; fix reported errors |
| Content admin | Build syllabus trees, publish/schedule content |
| Support agent | Resolve tickets, look up a student's orders/access |
| Finance operator | Orders, refunds, subscription status |
| Super admin | Roles, configuration, audit |

## 4. Scope

### In MVP (Phase 1)
Registration/login (email+password, mobile OTP), profile & exam selection, catalog & syllabus tree, recorded video
(provider-hosted) with resume, PDF notes, topic MCQ practice with explanations, timed tests with server-side timing
and deterministic scoring, results with subject/chapter breakdown, incorrect-answer review, rule-based study plan,
course purchase via Razorpay with webhook verification and entitlements, admin for courses/questions/tests, in-app
notifications, English + Hindi.

### Phase 2
Previous-year papers, coupons, subscriptions with renewal, bulk CSV import (UI), detailed analytics, revision
scheduling, richer admin reports, push via FCM, accessibility pass.

### Phase 3
Adaptive practice, AI-assisted explanations (with SME review), offline downloads with DRM, live classes, cohort
percentiles/rank, leaderboards, dedicated search engine, more Indian languages.

### Explicitly out of MVP
Live classes, DRM/offline video, AI features, official AIR/rank predictions, scraped or unlicensed question content.

## 5. Success metrics (measure after launch; no targets claimed yet)
- Activation: % of registrations that finish onboarding and start a topic within 24h.
- Learning: weekly active learners; practice questions per active learner.
- Assessment: test completion rate (started → submitted).
- Revenue: free→paid conversion; payment success rate.
- Quality: reported-question rate per 1,000 attempts; median fix time.

## 6. Key product rules
1. Never display fabricated stats — every number on the dashboard derives from stored events.
2. Rank/percentile is shown only when a cohort of ≥ `Analytics:MinCohortForPercentile` (default 100) exists, and is
   labelled "among students on this platform", never "AIR".
3. Recommendations are labelled "Suggested" (rule-based). The word "AI" is not used until an AI service exists.
4. Client payment success screens are never proof of payment; only server-verified signatures/webhooks grant access.
5. Submitted test attempts are immutable; scores are reproducible from the stored question snapshots.

## 7. Assumptions & open questions (need owner decisions)
| # | Question | Default assumed |
|---|---|---|
| 1 | Video provider? | Bunny Stream (cheap, HLS, signed URLs, India PoPs). Alternatives: Cloudflare Stream, Mux, VdoCipher (DRM, pricier). |
| 2 | SMS/OTP provider? | MSG91 (DLT-registered template required by TRAI). Dev mode logs OTP to console only in Development. |
| 3 | Hosting? | Single VM/container (e.g. DigitalOcean Bangalore / AWS Mumbai Lightsail) + managed Postgres. |
| 4 | GST registration and invoice format? | Prices stored GST-inclusive; GSTIN on invoice is configurable. Needs CA review. |
| 5 | Minimum age / parental consent? | Under-18 users must record guardian consent (DPDP Act 2023). Needs legal review. |
| 6 | Content source? | Original content authored by hired SMEs. No PYQ redistribution without verifying licence status. |
| 7 | Refund policy? | Manual approval within 7 days if < 20% consumed. Needs owner sign-off. |
