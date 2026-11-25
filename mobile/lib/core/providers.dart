import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import 'api/api_client.dart';
import 'api/learnpath_api.dart';
import 'api/models.g.dart';
import 'api/token_store.dart';
import 'config.dart';

final tokenStoreProvider = Provider<TokenStore>((ref) => SecureTokenStore());

/// UI language. Chosen on the welcome screen before sign-in, then kept in sync with the student's profile.
class LocaleController extends Notifier<Locale> {
  static const supported = ['en', 'hi'];

  @override
  Locale build() {
    unawaited(_restore());
    final device = WidgetsBinding.instance.platformDispatcher.locale.languageCode;
    return Locale(supported.contains(device) ? device : 'en');
  }

  Future<void> _restore() async {
    final saved = await ref.read(tokenStoreProvider).readPreference('language');
    if (saved != null && supported.contains(saved) && ref.mounted) state = Locale(saved);
  }

  Future<void> set(String code, {bool syncProfile = true}) async {
    if (!supported.contains(code) || code == state.languageCode) return;
    state = Locale(code);
    await ref.read(tokenStoreProvider).writePreference('language', code);
    if (syncProfile && ref.read(sessionProvider) is SignedIn) {
      try {
        ref.read(sessionProvider.notifier).profileUpdated(await ref.read(apiProvider).updateProfile(UpdateProfileRequest(language: code)));
      } catch (_) {
        // The language still changes on this device; the profile catches up next time it is saved.
      }
    }
  }
}

final localeProvider = NotifierProvider<LocaleController, Locale>(LocaleController.new);

/// Light, dark (night mode) or following the phone's setting. Kept on this device.
class ThemeModeController extends Notifier<ThemeMode> {
  static const key = 'theme';

  @override
  ThemeMode build() {
    unawaited(_restore());
    return ThemeMode.system;
  }

  Future<void> _restore() async {
    try {
      final saved = await ref.read(tokenStoreProvider).readPreference(key);
      final mode = ThemeMode.values.where((m) => m.name == saved).firstOrNull;
      if (mode != null && ref.mounted) state = mode;
    } catch (_) {
      // Unreadable storage: keep following the phone.
    }
  }

  Future<void> set(ThemeMode mode) async {
    state = mode;
    await ref.read(tokenStoreProvider).writePreference(key, mode.name);
  }
}

final themeModeProvider = NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);

/// Whether this phone has seen the intro slides: null while it is being read. Signing in counts as seen, so students
/// who already use the app never get the intro after signing out.
class IntroController extends Notifier<bool?> {
  static const key = 'intro_seen';

  @override
  bool? build() {
    ref.listen(sessionProvider, (_, s) {
      if (s is SignedIn && state != true) unawaited(finish());
    });
    unawaited(_restore());
    return null;
  }

  Future<void> _restore() async {
    bool seen;
    try {
      seen = await ref.read(tokenStoreProvider).readPreference(key) != null;
    } catch (_) {
      seen = true; // never trap a student on the intro because storage failed
    }
    if (ref.mounted && state == null) state = seen;
  }

  Future<void> finish() async {
    state = true;
    try {
      await ref.read(tokenStoreProvider).writePreference(key, '1');
    } catch (_) {
      // Shown again next launch at worst.
    }
  }
}

final introProvider = NotifierProvider<IntroController, bool?>(IntroController.new);

final apiClientProvider = Provider<ApiClient>(
  (ref) => ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    store: ref.watch(tokenStoreProvider),
    language: () => ref.read(localeProvider).languageCode,
    onSessionExpired: (reason) => ref.read(sessionProvider.notifier).expired(replaced: reason == 'session_replaced'),
  ),
);

final apiProvider = Provider<LearnPathApi>((ref) => LearnPathApi(ref.watch(apiClientProvider)));

// ---------------- Session ----------------

sealed class SessionState {
  const SessionState();
}

class SessionLoading extends SessionState {
  const SessionLoading();
}

class SignedOut extends SessionState {
  const SignedOut({this.expired = false, this.replaced = false});
  final bool expired;

  /// Signed out because the account was signed in on another phone.
  final bool replaced;
}

/// Browsing without an account: public subjects, courses and chapters; studying asks them to sign up. Remembered on
/// this phone until they sign in.
class Guest extends SessionState {
  const Guest();
}

/// Tokens exist but the API could not be reached to confirm them.
class SessionUnreachable extends SessionState {
  const SessionUnreachable();
}

class SignedIn extends SessionState {
  const SignedIn(this.profile);
  final ProfileDto profile;
}

class SessionController extends Notifier<SessionState> {
  @override
  SessionState build() {
    unawaited(restore());
    return const SessionLoading();
  }

  TokenStore get _store => ref.read(tokenStoreProvider);
  LearnPathApi get _api => ref.read(apiProvider);

  static const _guestKey = 'guest';

  Future<void> restore() async {
    state = const SessionLoading();
    if (await _store.read() == null) {
      state = await _store.readPreference(_guestKey) == '1' ? const Guest() : const SignedOut();
      return;
    }
    await _loadProfile();
  }

  Future<void> _loadProfile() async {
    try {
      final profile = await _api.profile();
      state = SignedIn(profile);
      await ref.read(localeProvider.notifier).set(profile.language, syncProfile: false);
    } on ApiException catch (e) {
      if (e.isNetwork) {
        state = const SessionUnreachable();
      } else {
        await _store.clear();
        state = const SignedOut();
      }
    }
  }

  Future<void> _signedIn(AuthTokens tokens) async {
    await _store.save(StoredTokens(accessToken: tokens.accessToken, refreshToken: tokens.refreshToken));
    await _store.writePreference(_guestKey, '0');
    await _loadProfile();
  }

  /// "Explore without an account".
  Future<void> browseAsGuest() async {
    await _store.writePreference(_guestKey, '1');
    state = const Guest();
  }

  Future<void> login(String email, String password, {bool takeOver = false}) async => _signedIn(await _api.login(email, password, takeOver: takeOver));

  Future<void> register({required String fullName, required String email, required String password}) async =>
      _signedIn(await _api.register(fullName: fullName, email: email, password: password, acceptTerms: true));

  Future<void> verifyOtp({required String phone, required String code, String? fullName, bool takeOver = false}) async =>
      _signedIn(await _api.verifyOtp(phone: phone, code: code, fullName: fullName, acceptTerms: true, takeOver: takeOver));

  void profileUpdated(ProfileDto profile) => state = SignedIn(profile);

  Future<void> logout() async {
    final tokens = await _store.read();
    if (tokens != null) {
      try {
        await _api.logout(tokens.refreshToken);
      } catch (_) {
        // Signing out locally must always work, even offline.
      }
    }
    await _store.clear();
    await _store.writePreference(_guestKey, '0');
    state = const SignedOut();
  }

  void expired({bool replaced = false}) {
    if (state is SignedIn) state = SignedOut(expired: true, replaced: replaced);
  }
}

final sessionProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);

/// The signed-in student's profile. Only read this below the signed-in part of the app.
final profileProvider = Provider<ProfileDto>((ref) {
  final s = ref.watch(sessionProvider);
  if (s is SignedIn) return s.profile;
  throw StateError('Not signed in');
});
