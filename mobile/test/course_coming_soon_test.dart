import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/learn/learn_screens.dart';
import 'package:learnpath/features/payments/buy_on_website.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

/// A paid course with its syllabus but no lessons yet, and the team's WhatsApp number set.
class ComingSoonApi implements HttpClientAdapter {
  final requests = <String>[];

  ResponseBody _json(Object body) => ResponseBody.fromString(
    jsonEncode(body),
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static Map<String, dynamic> chapter(String id, String title) => {
    'id': id,
    'unit': null,
    'title': title,
    'topics': <Object>[],
    'completionPercent': 0,
  };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    requests.add('${o.method} ${o.path}');
    return switch ('${o.method} ${o.path}') {
      'GET /app-config' => _json({'whatsAppNumber': '919876543210', 'whatsAppChannelUrl': null, 'websiteUrl': null}),
      _ => _json({
        'course': {
          'id': 'c8',
          'slug': 'class-8-foundation',
          'examId': 'foundation',
          'title': 'Class 8 Foundation',
          'description': 'Goal: strong basics + Olympiad / ITSE preparation.\n10 months · 40+ tests · ₹999/month',
          'classLevel': '8',
          'isFree': false,
          'priceInPaise': 99900,
          'accessDays': 30,
          'enrolled': false,
          'lessonCount': 0,
          'testCount': 0,
        },
        'hasAccess': false,
        'completionPercent': 0,
        'subjects': [
          {
            'id': 's1',
            'title': 'Science',
            'completionPercent': 0,
            'chapters': [chapter('ch1', 'Crop Production and Management'), chapter('ch2', 'Force and Pressure')],
          },
        ],
      }),
    };
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('a course whose classes have not started shows its syllabus and fees, and asks about admission', (
    tester,
  ) async {
    final api = ComingSoonApi();
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
          home: const CourseScreen(courseId: 'c8'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Classes start soon'), findsOneWidget);
    expect(find.text('₹999 · 30 days access'), findsOneWidget);
    expect(find.textContaining('Buy on website'), findsNothing);
    expect(find.text('Crop Production and Management'), findsOneWidget);
    expect(find.text('Science · Lessons coming soon'), findsNWidgets(2));

    await tester.tap(find.text('Ask about admission on WhatsApp'));
    await tester.pumpAndSettle();
    expect(opened.single.host, 'wa.me');
    expect(opened.single.path, '/919876543210');
    expect(opened.single.queryParameters['text'], 'Hi! I\'d like to know about admission to “Class 8 Foundation”.');
  });
}
