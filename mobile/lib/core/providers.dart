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

final apiClientProvider = Provider<ApiClient>(
  (ref) => ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    store: ref.watch(tokenStoreProvider),
    language: () => ref.read(localeProvider).languageCode,
    onSessionExpired: () => ref.read(sessionProvider.notifier).expired(),
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
  const SignedOut({this.expired = false});
  final bool expired;
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

  Future<void> restore() async {
    state = const SessionLoading();
    if (await _store.read() == null) {
      state = const SignedOut();
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
    await _loadProfile();
  }

  Future<void> login(String email, String password) async => _signedIn(await _api.login(email, password));

  Future<void> register({required String fullName, required String email, required String password}) async =>
      _signedIn(await _api.register(fullName: fullName, email: email, password: password, acceptTerms: true));

  Future<void> verifyOtp({required String phone, required String code, String? fullName}) async =>
      _signedIn(await _api.verifyOtp(phone: phone, code: code, fullName: fullName, acceptTerms: true));

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
    state = const SignedOut();
  }

  void expired() {
    if (state is SignedIn) state = const SignedOut(expired: true);
  }
}

final sessionProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);

/// The signed-in student's profile. Only read this below the signed-in part of the app.
final profileProvider = Provider<ProfileDto>((ref) {
  final s = ref.watch(sessionProvider);
  if (s is SignedIn) return s.profile;
  throw StateError('Not signed in');
});
