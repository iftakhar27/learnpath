# REST API Specification (v1)

The authoritative, machine-readable contract is **[`openapi.json`](openapi.json)** (exported from the running API;
Swagger UI is served at `/swagger` outside Production). Generate typed clients from it:

```bash
npx @openapitools/openapi-generator-cli generate -i docs/openapi.json -g typescript-fetch -o web/src/api   # Next.js
npx @openapitools/openapi-generator-cli generate -i docs/openapi.json -g dart-dio -o mobile/packages/api  # Flutter
```

## Conventions

| Topic | Rule |
|---|---|
| Base path | `/api/v1` — breaking changes go to `/api/v2` |
| Auth | `Authorization: Bearer <access token>` (15 min). Refresh with `POST /auth/refresh` (rotating; reuse revokes all sessions) |
| Language | `Accept-Language: hi` or `?lang=hi`; falls back to English per field when Hindi is missing. Clients send the profile language |
| Money | Integer paise (`priceInPaise: 199900` = ₹1,999.00), GST-inclusive, currency `INR` |
| Time | ISO-8601 UTC on the wire; "today" for plans/streaks is computed in Asia/Kolkata |
| Paging | `?page=1&pageSize=20` (max 100) → `{ items, page, pageSize, total }` |
| Errors | RFC 7807 problem details with a stable `code`, e.g. `{"status":402,"code":"entitlement_required","title":"…","traceId":"…"}` |
| Enums | Serialized as strings (`"SingleCorrect"`, `"InProgress"`) |

### Error codes clients should handle
`invalid_credentials`, `email_taken`, `terms_required`, `otp_invalid`, `otp_too_soon` (429), `invalid_refresh_token` (401),
`entitlement_required` (402 → show paywall), `attempt_closed`, `attempt_expired`, `max_attempts_reached`, `test_not_open`,
`test_closed`, `invalid_answer`, `no_questions`, `signature_invalid`, `already_owned`, `exam_required`,
`guardian_consent_required`, `internal_error` (500).

## Test-taking flow

```mermaid
sequenceDiagram
  participant App
  participant API
  App->>API: POST /tests/{id}/attempts
  API-->>App: attempt (questions without keys, expiresAt, remainingSeconds, serverTime)
  loop each answer change
    App->>API: PUT /attempts/{id}/answers {testQuestionId, response, markedForReview, timeSpentSeconds}
  end
  Note over App: network drop → retry PUT; on relaunch POST /tests/{id}/attempts resumes the same attempt
  App->>API: POST /attempts/{id}/submit   (safe to retry)
  API-->>App: result (score, breakdowns, review with explanations)
  Note over API: after expiresAt + 30 s grace, saves are rejected and the attempt auto-submits on next read
```

Answer payloads: option questions `{"selectedOptionIds":["<optionId>"]}`; numerical/integer `{"numericValue": 9.8}`;
clear a response with `"response": null`.

## Payment flow (Razorpay)

1. `POST /payments/orders {courseId}` → `{gatewayOrderId, keyId, amountInPaise}`; open Razorpay Checkout with these.
2. Checkout returns `razorpay_payment_id`, `razorpay_signature` → `POST /payments/verify`. Server checks
   `HMAC_SHA256(order_id|payment_id, key_secret)` and only then grants access.
3. Razorpay also calls `POST /payments/webhooks` (`payment.captured`, `order.paid`, `payment.failed`), verified with
   `X-Razorpay-Signature` and de-duplicated by `X-Razorpay-Event-Id`. Whichever arrives first grants access; the other is a no-op.
   Configure the webhook URL and secret in the Razorpay dashboard.

## Endpoint index

| Method | Path |
|---|---|
| `POST` | `/api/v1/admin/announcements` |
| `GET` | `/api/v1/admin/audit-logs` |
| `POST` | `/api/v1/admin/chapters` |
| `PUT` | `/api/v1/admin/chapters/{id}` |
| `GET` | `/api/v1/admin/courses` |
| `POST` | `/api/v1/admin/courses` |
| `PUT` | `/api/v1/admin/courses/{id}` |
| `POST` | `/api/v1/admin/courses/{id}/status` |
| `GET` | `/api/v1/admin/courses/{id}/tree` |
| `GET` | `/api/v1/admin/dashboard` |
| `GET` | `/api/v1/admin/exams` |
| `POST` | `/api/v1/admin/exams` |
| `PUT` | `/api/v1/admin/exams/{id}` |
| `GET` | `/api/v1/admin/imports` |
| `GET` | `/api/v1/admin/orders` |
| `GET` | `/api/v1/admin/question-reports` |
| `POST` | `/api/v1/admin/question-reports/{id}/resolve` |
| `GET` | `/api/v1/admin/questions` |
| `POST` | `/api/v1/admin/questions` |
| `POST` | `/api/v1/admin/questions/import` |
| `GET` | `/api/v1/admin/questions/{id}` |
| `PUT` | `/api/v1/admin/questions/{id}` |
| `POST` | `/api/v1/admin/questions/{id}/status` |
| `POST` | `/api/v1/admin/resources` |
| `PUT` | `/api/v1/admin/resources/{id}` |
| `POST` | `/api/v1/admin/resources/{id}/status` |
| `POST` | `/api/v1/admin/subjects` |
| `PUT` | `/api/v1/admin/subjects/{id}` |
| `GET` | `/api/v1/admin/tests` |
| `POST` | `/api/v1/admin/tests` |
| `PUT` | `/api/v1/admin/tests/{id}` |
| `POST` | `/api/v1/admin/tests/{id}/status` |
| `GET` | `/api/v1/admin/tickets` |
| `POST` | `/api/v1/admin/tickets/{id}/respond` |
| `POST` | `/api/v1/admin/topics` |
| `PUT` | `/api/v1/admin/topics/{id}` |
| `GET` | `/api/v1/admin/users` |
| `GET` | `/api/v1/admin/users/{id}` |
| `POST` | `/api/v1/admin/users/{id}/enrollments` |
| `POST` | `/api/v1/admin/users/{id}/roles` |
| `DELETE` | `/api/v1/admin/users/{id}/roles/{role}` |
| `POST` | `/api/v1/admin/users/{id}/status` |
| `GET` | `/api/v1/attempts` |
| `GET` | `/api/v1/attempts/{attemptId}` |
| `PUT` | `/api/v1/attempts/{attemptId}/answers` |
| `GET` | `/api/v1/attempts/{attemptId}/results` |
| `POST` | `/api/v1/attempts/{attemptId}/submit` |
| `POST` | `/api/v1/auth/change-password` |
| `POST` | `/api/v1/auth/login` |
| `POST` | `/api/v1/auth/logout` |
| `POST` | `/api/v1/auth/otp/request` |
| `POST` | `/api/v1/auth/otp/verify` |
| `POST` | `/api/v1/auth/refresh` |
| `POST` | `/api/v1/auth/register` |
| `GET` | `/api/v1/config` |
| `GET` | `/api/v1/courses` |
| `POST` | `/api/v1/courses/{courseId}/enroll` |
| `GET` | `/api/v1/courses/{courseId}/syllabus` |
| `GET` | `/api/v1/exams` |
| `GET` | `/api/v1/learning/bookmarks` |
| `POST` | `/api/v1/learning/bookmarks` |
| `DELETE` | `/api/v1/learning/bookmarks/{id}` |
| `GET` | `/api/v1/learning/history` |
| `POST` | `/api/v1/learning/progress` |
| `GET` | `/api/v1/notifications` |
| `POST` | `/api/v1/notifications/read-all` |
| `POST` | `/api/v1/notifications/{id}/read` |
| `GET` | `/api/v1/payments/orders` |
| `POST` | `/api/v1/payments/orders` |
| `POST` | `/api/v1/payments/orders/{orderId}/refund-request` |
| `POST` | `/api/v1/payments/verify` |
| `POST` | `/api/v1/payments/webhooks` |
| `POST` | `/api/v1/practice/sessions` |
| `POST` | `/api/v1/practice/sessions/{sessionId}/answers` |
| `POST` | `/api/v1/practice/sessions/{sessionId}/complete` |
| `POST` | `/api/v1/questions/{questionId}/report` |
| `GET` | `/api/v1/resources/{resourceId}/playback` |
| `GET` | `/api/v1/search` |
| `GET` | `/api/v1/student/analytics` |
| `GET` | `/api/v1/student/dashboard` |
| `GET` | `/api/v1/student/profile` |
| `PUT` | `/api/v1/student/profile` |
| `POST` | `/api/v1/student/profile/delete-request` |
| `GET` | `/api/v1/student/study-plan` |
| `GET` | `/api/v1/student/study-plan/history` |
| `POST` | `/api/v1/student/study-plan/regenerate` |
| `POST` | `/api/v1/student/study-plan/tasks/{taskId}/complete` |
| `POST` | `/api/v1/student/study-plan/tasks/{taskId}/skip` |
| `GET` | `/api/v1/support/tickets` |
| `POST` | `/api/v1/support/tickets` |
| `GET` | `/api/v1/tests` |
| `GET` | `/api/v1/tests/{testId}` |
| `POST` | `/api/v1/tests/{testId}/attempts` |
| `GET` | `/api/v1/topics/{topicId}` |
