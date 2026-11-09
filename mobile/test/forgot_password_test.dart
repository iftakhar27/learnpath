import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/auth/auth_screens.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

class ForgotApi implements HttpClientAdapter {
  final bodies = <Map<String, dynamic>>[];

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    if ('${o.method} ${o.path}' == 'POST /auth/password/forgot') {
      bodies.add(Map<String, dynamic>.from(o.data as Map));
      return ResponseBody.fromString('', 202);
    }
    return ResponseBody.fromString('{}', 404);
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('forgot password asks for the email, sends it, and says to check the inbox', (tester) async {
    final api = ForgotApi();
    final store = MemoryTokenStore();
    final router = GoRouter(
      routes: [
        GoRoute(
          path: '/',
          builder: (context, _) => Scaffold(
            body: Center(
              child: TextButton(
                onPressed: () => context.push('/forgot-password', extra: 'typed@before.test'),
                child: const Text('Open'),
              ),
            ),
          ),
        ),
        GoRoute(
          path: '/forgot-password',
          builder: (_, s) => ForgotPasswordScreen(email: s.extra is String ? s.extra! as String : null),
        ),
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

    // The email typed on the sign-in screen is carried over.
    expect(find.widgetWithText(TextFormField, 'typed@before.test'), findsOneWidget);
    await tester.enterText(find.byType(TextFormField), 'not-an-email');
    await tester.tap(find.widgetWithText(FilledButton, 'Send reset link'));
    await tester.pumpAndSettle();
    expect(find.text('Enter a valid email'), findsOneWidget);
    expect(api.bodies, isEmpty);

    await tester.enterText(find.byType(TextFormField), ' priya@example.com ');
    await tester.tap(find.widgetWithText(FilledButton, 'Send reset link'));
    await tester.pumpAndSettle();
    expect(api.bodies.single, {'email': 'priya@example.com'});
    expect(find.textContaining('If an account exists for priya@example.com'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Back to sign in'));
    await tester.pumpAndSettle();
    expect(find.text('Open'), findsOneWidget);
  });
}
