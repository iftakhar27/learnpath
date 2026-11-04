import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class StoredTokens {
  const StoredTokens({required this.accessToken, required this.refreshToken});
  final String accessToken;
  final String refreshToken;
}

/// Persists the session tokens and small preferences.
abstract interface class TokenStore {
  Future<StoredTokens?> read();
  Future<void> save(StoredTokens tokens);
  Future<void> clear();
  Future<String?> readPreference(String key);
  Future<void> writePreference(String key, String value);
}

/// Keychain on iOS, encrypted storage on Android, WebCrypto-backed storage on web (HTTPS or localhost only).
class SecureTokenStore implements TokenStore {
  SecureTokenStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const _access = 'lp_access_token';
  static const _refresh = 'lp_refresh_token';

  @override
  Future<StoredTokens?> read() async {
    final access = await _storage.read(key: _access);
    final refresh = await _storage.read(key: _refresh);
    if (access == null || refresh == null) return null;
    return StoredTokens(accessToken: access, refreshToken: refresh);
  }

  @override
  Future<void> save(StoredTokens tokens) async {
    await _storage.write(key: _access, value: tokens.accessToken);
    await _storage.write(key: _refresh, value: tokens.refreshToken);
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _access);
    await _storage.delete(key: _refresh);
  }

  @override
  Future<String?> readPreference(String key) => _storage.read(key: 'pref_$key');

  @override
  Future<void> writePreference(String key, String value) => _storage.write(key: 'pref_$key', value: value);
}

/// In-memory store for tests.
class MemoryTokenStore implements TokenStore {
  StoredTokens? tokens;
  final Map<String, String> prefs = {};

  @override
  Future<StoredTokens?> read() async => tokens;
  @override
  Future<void> save(StoredTokens t) async => tokens = t;
  @override
  Future<void> clear() async => tokens = null;
  @override
  Future<String?> readPreference(String key) async => prefs[key];
  @override
  Future<void> writePreference(String key, String value) async => prefs[key] = value;
}
