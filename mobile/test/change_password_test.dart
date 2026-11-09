import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/profile/profile_screens.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

class PasswordApi implements HttpClientAdapter {
  final bodies = <Map<String, dynamic>>[];

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    if ('${o.method} ${o.path}' == 'POST /auth/change-password') {
      bodies.add(Map<String, dynamic>.from(o.data as Map));
      if ((o.data as Map)['currentPassword'] != 'Abcd-Efgh-Jkmn') {
        return ResponseBody.fromString(
          jsonEncode({'status': 400, 'code': 'invalid_credentials', 'title': 'Current password is incorrect.'}),
          400,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType],
          },
        );
      }
      return ResponseBody.fromString('', 204);
    }
    return ResponseBody.fromString('{}', 404);
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('a student replaces a temporary password from Profile', (tester) async {
    final api = PasswordApi();
    final store = MemoryTokenStore()
      ..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
    final router = GoRouter(
      routes: [
        GoRoute(
          path: '/',
          builder: (context, _) => Scaffold(
            body: Center(
              child: TextButton(onPressed: () => context.push('/change-password'), child: const Text('Open')),
            ),
          ),
        ),
        GoRoute(path: '/change-password', builder: (_, _) => const ChangePasswordScreen()),
      ],
    );
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: [
          tokenStoreProvider.overrideWithValue(store),
          apiClientProvider.overrideWith(
            (ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: api),
          ),
        ],
        child: MaterialApp.router(
          routerConfig: router,
          supportedLocales: AppLocalizations.supportedLocales,
          localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
        ),
      ),
    );
    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();

    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), 'Abcd-Efgh-Jkmn');
    await tester.enterText(fields.at(1), 'short');
    await tester.enterText(fields.at(2), 'different');
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await tester.pumpAndSettle();
    expect(find.text('Use at least 8 characters'), findsOneWidget);
    expect(find.text("The passwords don't match"), findsOneWidget);
    expect(api.bodies, isEmpty);

    // A wrong current password shows the server's reason and stays on the screen.
    await tester.enterText(fields.at(0), 'wrong-one');
    await tester.enterText(fields.at(1), 'My-new-Passw0rd');
    await tester.enterText(fields.at(2), 'My-new-Passw0rd');
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await tester.pumpAndSettle();
    expect(find.text('Current password is incorrect.'), findsOneWidget);

    await tester.enterText(fields.at(0), 'Abcd-Efgh-Jkmn');
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await tester.pumpAndSettle();
    expect(api.bodies.last, {'currentPassword': 'Abcd-Efgh-Jkmn', 'newPassword': 'My-new-Passw0rd'});
    expect(find.text('Password changed. Other devices were signed out.'), findsOneWidget);
    expect(find.text('Open'), findsOneWidget, reason: 'back on the previous screen');
  });
}
