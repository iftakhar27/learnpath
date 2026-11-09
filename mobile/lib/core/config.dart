/// Build-time configuration, set with `--dart-define=KEY=value`.
abstract final class AppConfig {
  /// Backend base URL without the `/api/v1` suffix. The default reaches a development API on the host machine
  /// from the Android emulator. Production builds must pass the real HTTPS URL.
  static const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5080');

  static const brandName = String.fromEnvironment('BRAND_NAME', defaultValue: 'LearnPath');

  /// Turns on Flutter's semantics tree at startup (used by browser-driven demos and accessibility audits).
  static const enableSemantics = bool.fromEnvironment('ENABLE_SEMANTICS');

  /// The student website (no trailing slash): courses are bought there ("Buy on website").
  static const webBaseUrl = String.fromEnvironment('WEB_BASE_URL', defaultValue: 'http://10.0.2.2:3000');

  /// Show "Buy on website" on paid courses. Builds for a store that doesn't allow pointing to outside payment can turn
  /// it off (`--dart-define=WEB_PURCHASE=false`); the course then just says it can't be bought in the app.
  static const webPurchase = bool.fromEnvironment('WEB_PURCHASE', defaultValue: true);

  static const privacyPolicyUrl = String.fromEnvironment('PRIVACY_URL', defaultValue: 'https://example.com/privacy');
  static const termsUrl = String.fromEnvironment('TERMS_URL', defaultValue: 'https://example.com/terms');
}
