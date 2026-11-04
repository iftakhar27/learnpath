# LearnPath student app (Flutter)

Android and iOS app for students: sign-in (email or mobile OTP), onboarding, home dashboard with today's study plan,
course catalog and syllabus, video lessons with resume, notes, practice with instant solutions, timed tests with a
question palette, results and answer review, progress, bookmarks, notifications, purchases (Razorpay) and support.
English and Hindi throughout.

Flutter 3.47 · Dart 3.13 · `material_ui` (the decoupled Material library) · Riverpod 3 · go_router 18 · Dio.

## Run it

```bash
flutter pub get
# Android emulator talking to a development API on your computer (default):
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5080
# Physical phone on the same Wi-Fi: use your computer's LAN IP (and allow it in network_security_config.xml),
# or better, a deployed HTTPS API:
flutter run --dart-define=API_BASE_URL=https://api.example.com
```

The backend must be running (see the root README). Development sample login: any account you register in the app, or
`priya.sharma@example.com` / `Passw0rd!x` if you created the demo data.

## Build

```bash
flutter build apk --release --dart-define=API_BASE_URL=https://api.example.com      # Android APK
flutter build appbundle --release --dart-define=API_BASE_URL=https://api.example.com # Play Store
flutter build ipa --release --dart-define=API_BASE_URL=https://api.example.com      # iOS (needs macOS + Xcode)
```

Release builds are currently signed with the debug key so they install for testing. **Before publishing**, create an
upload keystore and configure `android/key.properties` / `signingConfigs` (see Flutter's "Build and release an Android
app"), and change `applicationId` (`com.learnpath.app`) to your own.

### Build-time settings (`--dart-define`)

| Key | Default | Purpose |
|---|---|---|
| `API_BASE_URL` | `http://10.0.2.2:5080` | Backend URL without `/api/v1`. Must be HTTPS outside development |
| `BRAND_NAME` | `LearnPath` | Product name shown in the app |
| `PRIVACY_URL`, `TERMS_URL` | example.com | Legal pages opened from the app |
| `ENABLE_SEMANTICS` | `false` | Forces the accessibility tree on (used for browser-driven demos) |

## Tests

```bash
flutter analyze
flutter test
```

- `test/api_client_test.dart` — token attach/refresh: concurrent requests share **one** refresh (the API revokes every
  session if a rotated refresh token is reused), retry after 401, session expiry, problem-details parsing.
- `test/exam_session_test.dart` — server-derived timer, palette states, autosave contents, resume, offline retry,
  closed-attempt handling.
- `test/models_test.dart` — generated models against real API JSON.
- `test/app_flow_test.dart` — the whole app against a fake API: Hindi switch, form validation, server errors, sign-in →
  onboarding → home redirects, option selection rules.

## Code map

```
lib/
  main.dart                 app, theme, localization
  app/router.dart           routes + auth/onboarding redirects + bottom tabs
  app/theme.dart            design tokens (shared with the web portal)
  core/api/models.g.dart    GENERATED from ../docs/openapi.json — do not edit
  core/api/api_client.dart  Dio, token refresh, ApiException
  core/api/learnpath_api.dart  typed endpoints
  core/providers.dart       session, language, API providers
  features/                 auth, onboarding, home, learn (+video), practice, tests (+exam engine), profile, payments
  widgets/                  shared widgets, QuestionView
  l10n/app_en.arb, app_hi.arb  all UI strings
tool/gen_models.dart        model generator
```

When the API contract changes: `../scripts/export-openapi.sh`, then `dart run tool/gen_models.dart`.
After editing ARB files: `flutter gen-l10n` (also runs on build).

## Notes and known gaps

- Hindi translations were written by the developer and need review by a native speaker before launch.
- Video plays through an HLS stream URL from the API (`Video:CdnHost` / `Video:CdnTokenKey` on the backend). Without
  that configured, the video screen shows "This video can't be played right now". DRM and offline downloads are out
  of MVP scope.
- Notes (PDFs) open in the phone's viewer via a signed link.
- Razorpay checkout works on Android/iOS only; the web build shows a message instead. Access is granted only after
  the server verifies the payment signature.
- Push notifications (FCM) are not wired yet; notifications are in-app.
- iOS builds need a Mac with Xcode; they were not built in this (Linux) environment.
