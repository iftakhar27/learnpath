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
`guardian_consent_required`, `no_quiz`, `quiz_session`, `doubt_limit` (429), `custom_test_limit` (429), `invalid_chapter`,
`too_many_lines`, `internal_error` (500).

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

## Buying from the app ("Buy on website")

The apps take no payments. On a paid course the app calls `POST /auth/handoff` (signed in) → `{code, expiresAt}` (valid
2 minutes, single use) and opens `<website>/handoff?code=…&next=/learn/{courseId}?buy=1&from=app` in the browser. The
website exchanges the code with `POST /auth/handoff/redeem {code, deviceSlot:"browser", deviceId, takeOver?}` → the same
tokens as a sign-in (the one-browser rule applies: `409 session_active`, retry with `takeOver`; `400 handoff_invalid`
when used or expired), then opens the course with Razorpay checkout started. Back in the app, the course is refreshed.

## Browse by NCERT book

- `GET /library` → `{tiles:[{classLevel, subject, title, chapters, readyChapters, progressPercent}], resume}` (6 books; `resume`
  is the NCERT chapter of the last lesson studied). Open to visitors; progress needs sign-in.
- `GET /library/{classLevel}/{subject}` (e.g. `/library/11/physics`) → the book's chapters with topic/video/note counts,
  `ready` (some published course teaches it), `locked` (only in courses the student doesn't own) and progress.
- `GET /library/chapters/{id}` → the chapter's topics and lessons from every published course that teaches it (owned and
  free courses first), `canPractise`, `practiceQuestionCount`, `buyCourseId` when nothing is open, previous/next chapter.
- `POST /practice/sessions {mode:"ncert", ncertChapterId}` practises it. Admins link course chapters with
  `ncertChapterId` on `/admin/chapters` (omit to keep or, on create, to match by title; an empty GUID unlinks);
  `GET /admin/ncert-chapters` lists them.

## Custom tests

- `GET /tests/custom/options` → `{subjects:[{subject, title, chapters:[{id, classLevel, number, title, easy, medium, hard,
  locked}]}], maxQuestions: 180, minutesPerQuestion: 1}`: NCERT chapters with questions, counted by difficulty among
  courses the student can open; `locked` = more in courses they haven't bought.
- `POST /tests/custom {chapterIds, count (1–180), difficulty?, skipCorrect = true}` → a `TestSummaryDto` (`type: "Custom"`).
  The test is private to the student, spread across the chosen chapters, one section per subject (Physics, Chemistry,
  Biology), one minute per question (at least 5), the questions' own marks (NEET: +4 / −1). With `skipCorrect`,
  questions already answered correctly come last. Fewer questions than asked when there aren't enough. `402` when the
  chapters' questions are only in paid courses, `409 no_questions`, `400 invalid_chapter`, up to 20 a day
  (`429 custom_test_limit`).
- It then runs like any test: `GET /tests/{id}`, `POST /tests/{id}/attempts`, results and review. `GET /tests/custom`
  lists the student's latest 30; they never appear in `GET /tests`, search, study plans or the admin test list.

## Flash cards

- `GET /flashcards` → `{decks:[{chapterId, classLevel, subject, subjectTitle, number, title, cards, due, mastered}],
  dueTotal}`: NCERT chapters with published cards. Free for every signed-in student.
- `GET /flashcards/{chapterId}` → `{deck, cards:[{id, front, back, box, isNew, isDue, dueAt}], nextDueAt}`: cards still
  being learnt first, then overdue reviews, then new cards in deck order, then cards not due yet.
- `POST /flashcards/cards/{id}/review {known}` → `{box, dueAt, mastered}`. Leitner boxes: "got it" moves a card up one
  box (most 5) and due again in 1, 3, 7, 16 or 35 days; "still learning" puts it back in box 0, due now. Box 3 or
  more counts as mastered. Reviews count towards the streak.
- `GET /library/chapters/{id}` includes `flashcardCount`.
- Admin (content editors): `GET /admin/flashcards/decks` (all 79 chapters with published/draft counts),
  `GET /admin/flashcards?ncertChapterId=`, `POST /admin/flashcards`, `PUT /admin/flashcards/{id}`,
  `DELETE /admin/flashcards/{id}`, `POST /admin/flashcards/import {ncertChapterId, text, status}`: one card per line,
  `front | back` (or tab-separated), optional `| front in Hindi | back in Hindi`, `#` comments; returns `{added,
  problems:[{line, reason}]}` (missing back, too long, already in the deck). Up to 500 lines.

## Free study material

- `GET /materials?subject=&classLevel=&type=` (public) → published material: `{id, title, description, type
  (Notes, FormulaSheet, MindMap, PreviousYearPaper, SamplePaper, Other), subject (null = general), classLevel (null =
  both), format (PDF, Image, Link), sizeBytes, downloads}`. `subject=general` lists material for no particular subject;
  `classLevel=12` includes material for both classes.
- `POST /materials/{id}/download` (signed in; counts the download) → `{url, external, fileName, expiresAt}`. For an
  uploaded file, `url` is relative to the API host (`/api/v1/materials/{id}/file?expires=…&sig=…`), valid 10 minutes,
  and needs no token, so a browser or PDF viewer can open it; for a link, it's the external page.
- `GET /materials/{id}/file?expires&sig` serves the file inline with its download name (`403 link_expired` when the
  link is old or altered).
- Admin (content editors), multipart/form-data: `POST /admin/materials` and `PUT /admin/materials/{id}` with
  `titleEn, titleHi?, descriptionEn?, descriptionHi?, type, subject?, classLevel?, status, sortOrder` and either
  `file` (PDF, PNG or JPEG, recognised by content; up to `Materials:MaxUploadMegabytes`, default 20) or `externalUrl`
  (https). On edit, a file or link replaces the source and sending neither keeps it. `GET /admin/materials` →
  `{items, storedBytes, maxUploadMegabytes}`; `DELETE /admin/materials/{id}` removes the file too. Errors:
  `file_or_link_required`, `unsupported_file`, `file_too_large`, `empty_file`, `invalid_link`.
- Files are kept in the database for now (behind `IFileStore`), so they're backed up with it; an object store can
  replace it later.

## Lesson notes

- Staff upload a notes resource's file with `POST /admin/resources/{id}/document` (multipart `file`: PDF, PNG or JPEG, up
  to `Materials:MaxUploadMegabytes`); a new upload replaces the old one. `DELETE /admin/resources/{id}/document` removes
  it (not while the notes are published without another source: `409 document_in_use`). Notes can be created without a
  file, but publishing them needs one (`409 document_missing`) or a storage key for an external host. The course tree
  shows `documentFileName` and `documentSizeBytes`.
- `GET /resources/{id}/playback` for uploaded notes returns `url` relative to the API
  (`/api/v1/resources/{id}/file?expires=…&sig=…`, valid 2 hours, no token needed); access to the lesson is checked
  there. Notes with only a storage key keep the external host's signed URL.

## App settings and WhatsApp

- `GET /app-config` (public, cached 5 minutes) → `{whatsAppNumber, whatsAppChannelUrl, websiteUrl}`. Set on the server:
  `Contact__WhatsAppNumber` (country code included; returned as digits for `https://wa.me/<number>`) and
  `Contact__WhatsAppChannelUrl` (https on whatsapp.com, chat.whatsapp.com or wa.me only). Unset or invalid values come
  back as `null` and the apps hide those links. Sharing (`https://wa.me/?text=…`) needs no setting.
- Signed-out visitors can use the public endpoints: `/exams`, `/courses`, `/courses/{id}/syllabus`, `/library/*` and
  `/search`; the apps' guest browsing is built on these.

## Forgotten passwords

- `POST /auth/password/forgot {email}` → `202` whether or not the email has an account. If it does, an email with a link
  to `<website>/reset-password?token=…` is sent (single use, 30 minutes; at most one a minute and five an hour).
- `POST /auth/password/reset {token, newPassword}` → `204`; every device is signed out. `400 reset_invalid` when the link
  was used or expired.
- Staff: `POST /admin/users/{id}/reset-password` → `{temporaryPassword}` (shown once); signed-in users change theirs
  with `POST /auth/change-password` (this device stays signed in, others are signed out).

## Topic quiz

A short quiz at the end of each topic: up to `Topic.QuizQuestionCount` (default 5, set per topic in the admin portal;
0 = no quiz) of the topic's published questions, unseen ones first.

1. `GET /topics/{id}` includes `quiz` (`questionCount`, `passPercent`, `attempts`, `bestPercent`, `passed`), or `null`
   when the topic has no quiz. `POST /learning/progress` returns `topicCompleted` and the same `quiz` when that lesson
   finished the topic, so clients can offer it straight away.
2. `POST /topics/{id}/quiz` → `{sessionId, questions}` (needs access to the course; a new set each time).
3. `POST /practice/quizzes/{sessionId}/submit {answers:[{questionId, response, timeSpentSeconds}]}` →
   `{scorePercent, passed, correct, incorrect, partial, skipped, items:[{outcome, correctOptionIds, explanation…}], nextTopicId}`.
   Unanswered questions are `Skipped`. Submitting again returns the same result. Answers are not revealed before submit
   (`POST /practice/sessions/{id}/answers` refuses quiz sessions with `quiz_session`). Score: correct = 1, partly
   correct = ½, as a percentage of the questions.

## Doubts

Students ask teachers; teachers (Teacher, ContentAdmin, SuperAdmin) answer in the admin portal.

- `POST /doubts {text, topicId?, resourceId?, questionId?}`: any one link is enough; the API derives the topic and course.
  A doubt about a paid topic needs access to the course (402). Up to 10 new doubts a day (`429 doubt_limit`).
- `GET /doubts`, `GET /doubts/{id}` (thread), `POST /doubts/{id}/messages {text}` (follow-up; status back to `Open`),
  `POST /doubts/{id}/resolve`.
- Staff: `GET /admin/doubts?status=Open&q=…` (open ones oldest first), `GET /admin/doubts/{id}`,
  `POST /admin/doubts/{id}/reply {text, resolve}` (status `Answered`, or `Resolved`; the student gets a notification
  linking to `/doubts/{id}`), `POST /admin/doubts/{id}/resolve`.
- Status: `Open` (waiting for a teacher) → `Answered` → `Resolved`; a follow-up re-opens it.

## Endpoint index

| Method | Path |
|---|---|
| `POST` | `/api/v1/admin/announcements` |
| `GET` | `/api/v1/admin/audit-logs` |
| `POST` | `/api/v1/admin/chapters` |
| `PUT` | `/api/v1/admin/chapters/{id}` |
| `DELETE` | `/api/v1/admin/chapters/{id}` |
| `GET` | `/api/v1/admin/courses` |
| `POST` | `/api/v1/admin/courses` |
| `PUT` | `/api/v1/admin/courses/{id}` |
| `POST` | `/api/v1/admin/courses/{id}/status` |
| `GET` | `/api/v1/admin/courses/{id}/tree` |
| `GET` | `/api/v1/admin/dashboard` |
| `GET` | `/api/v1/admin/doubts` |
| `GET` | `/api/v1/admin/doubts/{id}` |
| `POST` | `/api/v1/admin/doubts/{id}/reply` |
| `POST` | `/api/v1/admin/doubts/{id}/resolve` |
| `GET` | `/api/v1/admin/exams` |
| `POST` | `/api/v1/admin/exams` |
| `PUT` | `/api/v1/admin/exams/{id}` |
| `GET` | `/api/v1/admin/flashcards` |
| `POST` | `/api/v1/admin/flashcards` |
| `GET` | `/api/v1/admin/flashcards/decks` |
| `POST` | `/api/v1/admin/flashcards/import` |
| `PUT` | `/api/v1/admin/flashcards/{id}` |
| `DELETE` | `/api/v1/admin/flashcards/{id}` |
| `GET` | `/api/v1/admin/imports` |
| `GET` | `/api/v1/admin/materials` |
| `POST` | `/api/v1/admin/materials` |
| `PUT` | `/api/v1/admin/materials/{id}` |
| `DELETE` | `/api/v1/admin/materials/{id}` |
| `GET` | `/api/v1/admin/ncert-chapters` |
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
| `DELETE` | `/api/v1/admin/resources/{id}` |
| `POST` | `/api/v1/admin/resources/{id}/document` |
| `DELETE` | `/api/v1/admin/resources/{id}/document` |
| `POST` | `/api/v1/admin/resources/{id}/status` |
| `POST` | `/api/v1/admin/subjects` |
| `PUT` | `/api/v1/admin/subjects/{id}` |
| `DELETE` | `/api/v1/admin/subjects/{id}` |
| `GET` | `/api/v1/admin/test-emails` |
| `GET` | `/api/v1/admin/test-otp-codes` |
| `GET` | `/api/v1/admin/tests` |
| `POST` | `/api/v1/admin/tests` |
| `GET` | `/api/v1/admin/tests/{id}` |
| `PUT` | `/api/v1/admin/tests/{id}` |
| `POST` | `/api/v1/admin/tests/{id}/duplicate` |
| `POST` | `/api/v1/admin/tests/{id}/status` |
| `GET` | `/api/v1/admin/tickets` |
| `POST` | `/api/v1/admin/tickets/{id}/respond` |
| `POST` | `/api/v1/admin/topics` |
| `PUT` | `/api/v1/admin/topics/{id}` |
| `DELETE` | `/api/v1/admin/topics/{id}` |
| `GET` | `/api/v1/admin/users` |
| `GET` | `/api/v1/admin/users/{id}` |
| `POST` | `/api/v1/admin/users/{id}/enrollments` |
| `POST` | `/api/v1/admin/users/{id}/reset-password` |
| `POST` | `/api/v1/admin/users/{id}/roles` |
| `DELETE` | `/api/v1/admin/users/{id}/roles/{role}` |
| `POST` | `/api/v1/admin/users/{id}/status` |
| `GET` | `/api/v1/app-config` |
| `GET` | `/api/v1/attempts` |
| `GET` | `/api/v1/attempts/{attemptId}` |
| `PUT` | `/api/v1/attempts/{attemptId}/answers` |
| `GET` | `/api/v1/attempts/{attemptId}/results` |
| `POST` | `/api/v1/attempts/{attemptId}/submit` |
| `POST` | `/api/v1/auth/change-password` |
| `POST` | `/api/v1/auth/handoff` |
| `POST` | `/api/v1/auth/handoff/redeem` |
| `POST` | `/api/v1/auth/login` |
| `POST` | `/api/v1/auth/logout` |
| `POST` | `/api/v1/auth/otp/request` |
| `POST` | `/api/v1/auth/otp/verify` |
| `POST` | `/api/v1/auth/password/forgot` |
| `POST` | `/api/v1/auth/password/reset` |
| `POST` | `/api/v1/auth/refresh` |
| `POST` | `/api/v1/auth/register` |
| `GET` | `/api/v1/config` |
| `GET` | `/api/v1/courses` |
| `POST` | `/api/v1/courses/{courseId}/enroll` |
| `GET` | `/api/v1/courses/{courseId}/syllabus` |
| `POST` | `/api/v1/doubts` |
| `GET` | `/api/v1/doubts` |
| `GET` | `/api/v1/doubts/{id}` |
| `POST` | `/api/v1/doubts/{id}/messages` |
| `POST` | `/api/v1/doubts/{id}/resolve` |
| `GET` | `/api/v1/exams` |
| `GET` | `/api/v1/flashcards` |
| `POST` | `/api/v1/flashcards/cards/{cardId}/review` |
| `GET` | `/api/v1/flashcards/{chapterId}` |
| `GET` | `/api/v1/learning/bookmarks` |
| `POST` | `/api/v1/learning/bookmarks` |
| `DELETE` | `/api/v1/learning/bookmarks/{id}` |
| `GET` | `/api/v1/learning/history` |
| `POST` | `/api/v1/learning/progress` |
| `GET` | `/api/v1/library` |
| `GET` | `/api/v1/library/chapters/{id}` |
| `GET` | `/api/v1/library/{classLevel}/{subject}` |
| `GET` | `/api/v1/materials` |
| `POST` | `/api/v1/materials/{id}/download` |
| `GET` | `/api/v1/materials/{id}/file` |
| `GET` | `/api/v1/notifications` |
| `POST` | `/api/v1/notifications/read-all` |
| `GET` | `/api/v1/notifications/unread-count` |
| `POST` | `/api/v1/notifications/{id}/read` |
| `POST` | `/api/v1/payments/orders` |
| `GET` | `/api/v1/payments/orders` |
| `POST` | `/api/v1/payments/orders/{orderId}/refund-request` |
| `POST` | `/api/v1/payments/verify` |
| `POST` | `/api/v1/payments/webhooks` |
| `POST` | `/api/v1/practice/quizzes/{sessionId}/submit` |
| `POST` | `/api/v1/practice/sessions` |
| `POST` | `/api/v1/practice/sessions/{sessionId}/answers` |
| `POST` | `/api/v1/practice/sessions/{sessionId}/complete` |
| `POST` | `/api/v1/questions/{questionId}/report` |
| `GET` | `/api/v1/resources/{resourceId}/file` |
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
| `POST` | `/api/v1/support/tickets` |
| `GET` | `/api/v1/support/tickets` |
| `GET` | `/api/v1/tests` |
| `GET` | `/api/v1/tests/custom` |
| `POST` | `/api/v1/tests/custom` |
| `GET` | `/api/v1/tests/custom/options` |
| `GET` | `/api/v1/tests/{testId}` |
| `POST` | `/api/v1/tests/{testId}/attempts` |
| `GET` | `/api/v1/topics/{topicId}` |
| `POST` | `/api/v1/topics/{topicId}/quiz` |
