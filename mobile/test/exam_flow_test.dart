import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/tests/test_screens.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

/// Fake attempt/answer/submit/result endpoints that record what the exam player sends.
class FakeExamApi implements HttpClientAdapter {
  final saved = <Map<String, dynamic>>[];
  var submitted = false;

  ResponseBody _json(Object body, [int status = 200]) =>
      ResponseBody.fromString(jsonEncode(body), status, headers: {Headers.contentTypeHeader: [Headers.jsonContentType]});

  Map<String, dynamic> get attempt => {
        'attemptId': 'att-1',
        'testId': 't-1',
        'testTitle': 'Laws of Motion test',
        'instructions': null,
        'status': submitted ? 'Submitted' : 'InProgress',
        'startedAt': '2026-11-04T10:00:00Z',
        'expiresAt': '2026-11-04T10:20:00Z',
        'remainingSeconds': 1200,
        'serverTime': '2026-11-04T10:00:00Z',
        'questions': [
          for (var i = 0; i < 3; i++)
            {
              'number': i + 1,
              'testQuestionId': 'tq-$i',
              'section': null,
              'subject': 'Physics',
              'type': 'SingleCorrect',
              'stem': 'Question text $i',
              'imageUrl': null,
              'options': [
                {'id': 'q$i-a', 'text': 'Alpha $i'},
                {'id': 'q$i-b', 'text': 'Beta $i'},
              ],
              'marks': 4,
              'negativeMarks': 1,
              'response': null,
              'markedForReview': false,
              'timeSpentSeconds': 0,
            },
        ],
      };

  Map<String, dynamic> get result => {
        'attemptId': 'att-1', 'testId': 't-1', 'testTitle': 'Laws of Motion test', 'status': 'Submitted', 'attemptNumber': 1,
        'score': 4, 'maxScore': 12, 'percentage': 33.3, 'accuracy': 50, 'correct': 1, 'incorrect': 1, 'partial': 0, 'skipped': 1,
        'timeTakenSeconds': 300, 'subjects': <Object>[], 'chapters': <Object>[], 'difficulty': <Object>[], 'questions': <Object>[],
        'previousAttempts': <Object>[], 'percentile': null, 'recommendations': ['Revise friction'],
      };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    final key = '${o.method} ${o.path}';
    if (key == 'GET /attempts/att-1') return _json(attempt);
    if (key == 'PUT /attempts/att-1/answers') {
      saved.add(Map<String, dynamic>.from(o.data as Map));
      return _json({'savedAt': '2026-11-04T10:01:00Z'});
    }
    if (key == 'POST /attempts/att-1/submit') {
      submitted = true;
      return _json(result);
    }
    if (key == 'GET /attempts/att-1/results') return _json(result);
    if (key == 'GET /tests' || key == 'GET /student/dashboard') return _json(<Object>[]);
    return _json({'status': 404, 'code': 'not_found', 'title': 'Not found'}, 404);
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('answer, mark for review, use the palette, submit and land on the result', (tester) async {
    final semantics = tester.ensureSemantics();
    // The exam clock ticks every second, so the UI never "settles"; advance time in fixed steps instead.
    Future<void> settle() async {
      for (var i = 0; i < 6; i++) {
        await tester.pump(const Duration(milliseconds: 200));
      }
    }

    final api = FakeExamApi();
    final store = MemoryTokenStore()..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
    final router = GoRouter(initialLocation: '/exam/att-1', routes: [
      GoRoute(path: '/exam/:id', builder: (_, s) => ExamScreen(attemptId: s.pathParameters['id']!)),
      GoRoute(path: '/result/:id', builder: (_, s) => ResultScreen(attemptId: s.pathParameters['id']!)),
    ]);
    await tester.pumpWidget(ProviderScope(
      retry: (_, _) => null,
      overrides: [
        tokenStoreProvider.overrideWithValue(store),
        apiClientProvider.overrideWithValue(ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: api)),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
        supportedLocales: AppLocalizations.supportedLocales,
      ),
    ));
    await settle();

    expect(find.text('Question 1 of 3'), findsOneWidget);
    expect(find.textContaining(RegExp(r'^(20:00|19:5\d)$')), findsOneWidget, reason: 'timer starts from the server remaining time');

    // Answer Q1: saved straight away with the chosen option.
    await tester.tap(find.text('Alpha 0'));
    await settle();
    expect(api.saved.last['testQuestionId'], 'tq-0');
    expect(api.saved.last['response'], {'selectedOptionIds': ['q0-a']});

    // Q2: mark for review without answering.
    await tester.tap(find.text('Save & next'));
    await settle();
    expect(find.text('Question 2 of 3'), findsOneWidget);
    await tester.tap(find.text('Mark for review'));
    await settle();
    expect(api.saved.last, containsPair('markedForReview', true));
    expect(find.text('Unmark review'), findsOneWidget);

    // Palette shows the legend and lets us jump to Q3.
    await tester.tap(find.byTooltip('Questions'));
    await settle();
    expect(find.text('Not visited'), findsOneWidget);
    await tester.tap(find.bySemanticsLabel(RegExp(r'^Question 3 of 3')));
    await settle();
    expect(find.text('Question 3 of 3'), findsOneWidget);

    // Submit: confirmation shows the real counts, then the result screen opens.
    await tester.tap(find.text('Submit test'));
    await settle();
    expect(find.textContaining('Answered: 1'), findsOneWidget);
    expect(find.textContaining('Marked for review: 1'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Submit'));
    await settle();

    expect(api.submitted, isTrue);
    expect(find.text('Review answers'), findsOneWidget);
    expect(find.text('4 / 12'), findsOneWidget);
    semantics.dispose();
  });
}
