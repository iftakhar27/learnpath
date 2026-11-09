import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/learn/learn_screens.dart';
import 'package:learnpath/features/payments/buy_on_website.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

/// A paid course the student doesn't own until [owned] is set (as if they paid on the website).
class CourseApi implements HttpClientAdapter {
  bool owned = false;
  final requests = <String>[];

  ResponseBody _json(Object body) => ResponseBody.fromString(
    jsonEncode(body),
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    requests.add('${o.method} ${o.path}');
    return switch ('${o.method} ${o.path}') {
      'POST /auth/handoff' => _json({'code': 'one-time-code', 'expiresAt': '2099-01-01T00:00:00Z'}),
      _ => _json({
        'course': {
          'id': 'c1',
          'slug': 'mechanics',
          'examId': 'neet',
          'title': 'Mechanics',
          'description': null,
          'classLevel': '11',
          'isFree': false,
          'priceInPaise': 199900,
          'accessDays': 365,
          'enrolled': owned,
        },
        'hasAccess': owned,
        'completionPercent': 0,
        'subjects': <Object>[],
      }),
    };
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('paid courses are bought on the website, signed in, and unlock on returning to the app', (tester) async {
    final api = CourseApi();
    final opened = <Uri>[];
    final store = MemoryTokenStore()
      ..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: [
          tokenStoreProvider.overrideWithValue(store),
          apiClientProvider.overrideWith(
            (ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: api),
          ),
          openUrlProvider.overrideWithValue((uri) async {
            opened.add(uri);
            return true;
          }),
        ],
        child: MaterialApp(
          supportedLocales: AppLocalizations.supportedLocales,
          localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
          home: const CourseScreen(courseId: 'c1'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Buy on website · ₹1,999'));
    await tester.pumpAndSettle();
    expect(api.requests, contains('POST /auth/handoff'));
    expect(opened.single.path, '/handoff');
    expect(opened.single.queryParameters, {'code': 'one-time-code', 'next': '/learn/c1?buy=1&from=app'});
    expect(api.requests, isNot(contains('POST /payments/orders')), reason: 'the app takes no payments itself');

    // The student pays in the browser and comes back to the app.
    api.owned = true;
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await tester.pumpAndSettle();
    expect(find.text('You have access to this course'), findsOneWidget);
    expect(find.textContaining('Buy on website'), findsNothing);
  });
}
