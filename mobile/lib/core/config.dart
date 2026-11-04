/// Build-time configuration, set with `--dart-define=KEY=value`.
abstract final class AppConfig {
  /// Backend base URL without the `/api/v1` suffix. The default reaches a development API on the host machine
  /// from the Android emulator. Production builds must pass the real HTTPS URL.
  static const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5080');

  static const brandName = String.fromEnvironment('BRAND_NAME', defaultValue: 'LearnPath');

  /// Turns on Flutter's semantics tree at startup (used by browser-driven demos and accessibility audits).
  static const enableSemantics = bool.fromEnvironment('ENABLE_SEMANTICS');

  static const privacyPolicyUrl = String.fromEnvironment('PRIVACY_URL', defaultValue: 'https://example.com/privacy');
  static const termsUrl = String.fromEnvironment('TERMS_URL', defaultValue: 'https://example.com/terms');
}
