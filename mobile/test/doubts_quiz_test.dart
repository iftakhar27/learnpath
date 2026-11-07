import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/doubts/doubt_screens.dart';
import 'package:learnpath/features/learn/quiz_screen.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

/// Fake of the quiz and doubt endpoints; records what the app sends.
class FakeApi implements HttpClientAdapter {
  final bodies = <String, Map<String, dynamic>>{};

  ResponseBody _json(Object body, [int status = 200]) => ResponseBody.fromString(
    jsonEncode(body),
    status,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static Map<String, dynamic> _question(String id, String stem) => {
    'questionId': id,
    'type': 'SingleCorrect',
    'difficulty': 'Easy',
    'stem': stem,
    'imageUrl': null,
    'hint': null,
    'options': [
      {'id': '$id-a', 'text': 'Option A of $id'},
      {'id': '$id-b', 'text': 'Option B of $id'},
    ],
    'marks': 4,
    'negativeMarks': 1,
  };

  static const _context = {
    'courseId': 'c1',
    'course': 'Physics',
    'topicId': 't1',
    'topic': 'Friction',
    'resourceId': null,
    'resource': null,
    'questionId': null,
    'question': null,
  };

  Map<String, dynamic> _doubt(String text) => {
    'id': 'd2',
    'text': text,
    'status': 'Open',
    'context': _context,
    'messages': <Object>[],
    'createdAt': '2026-11-07T10:00:00Z',
    'updatedAt': '2026-11-07T10:00:00Z',
  };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    final key = '${o.method} ${o.path}';
    if (o.data is Map) bodies[key] = Map<String, dynamic>.from(o.data as Map);
    return switch (key) {
      'POST /topics/t1/quiz' => _json({
        'sessionId': 's1',
        'topicId': 't1',
        'topic': 'Friction',
        'passPercent': 60,
        'questions': [_question('q1', 'What opposes sliding?'), _question('q2', 'Which friction is larger?')],
      }),
      'POST /practice/quizzes/s1/submit' => _json({
        'sessionId': 's1',
        'topicId': 't1',
        'total': 2,
        'correct': 1,
        'incorrect': 0,
        'partial': 0,
        'skipped': 1,
        'scorePercent': 50,
        'passPercent': 60,
        'passed': false,
        'nextTopicId': null,
        'items': [
          {
            'questionId': 'q1',
            'outcome': 'Correct',
            'marksIfInTest': 4,
            'correctOptionIds': ['q1-a'],
            'correctNumericAnswer': null,
            'explanation': 'Friction opposes relative motion.',
            'videoSolutionId': null,
          },
          {
            'questionId': 'q2',
            'outcome': 'Skipped',
            'marksIfInTest': 0,
            'correctOptionIds': ['q2-b'],
            'correctNumericAnswer': null,
            'explanation': 'Limiting static friction is larger.',
            'videoSolutionId': null,
          },
        ],
      }),
      'GET /doubts' => _json({
        'items': [
          {
            'id': 'd1',
            'text': 'Why is kinetic friction smaller?',
            'status': 'Answered',
            'context': _context,
            'replies': 1,
            'createdAt': '2026-11-06T10:00:00Z',
            'updatedAt': '2026-11-06T12:00:00Z',
          },
        ],
        'page': 1,
        'pageSize': 50,
        'total': 1,
      }),
      'POST /doubts' => _json(_doubt((o.data as Map)['text'] as String)),
      _ => _json({'status': 404, 'code': 'not_found', 'title': 'Not found'}, 404),
    };
  }

  @override
  void close({bool force = false}) {}
}

Widget screen(FakeApi api, Widget child) {
  final store = MemoryTokenStore()
    ..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
  return ProviderScope(
    retry: (_, _) => null,
    overrides: [
      tokenStoreProvider.overrideWithValue(store),
      apiClientProvider.overrideWith(
        (ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: api),
      ),
    ],
    child: MaterialApp(
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
      home: child,
    ),
  );
}

void main() {
  testWidgets('topic quiz asks before submitting with unanswered questions, then shows the score and answers', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    final api = FakeApi();
    await tester.pumpWidget(screen(api, const TopicQuizScreen(topicId: 't1')));
    await tester.pumpAndSettle();

    expect(find.text('2 questions · pass mark 60%'), findsOneWidget);
    await tester.tap(find.text('Option A of q1'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Submit quiz'));
    await tester.pumpAndSettle();
    expect(find.text('1 question is unanswered. Submit anyway?'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Submit'));
    await tester.pumpAndSettle();

    final sent = api.bodies['POST /practice/quizzes/s1/submit']!['answers'] as List<dynamic>;
    expect(sent, hasLength(2));
    expect((sent[0] as Map)['response'], {
      'selectedOptionIds': ['q1-a'],
    });
    expect((sent[1] as Map)['response'], isNull);

    expect(find.text('50%'), findsOneWidget);
    expect(find.text('You need 60% to pass. Revise the lessons and try again.'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Limiting static friction is larger.'), 300);
    expect(find.text('Not answered'), findsOneWidget);
    expect(find.text('Ask a doubt'), findsWidgets);
  });

  testWidgets('doubts list shows replies and asking a new doubt needs a few words', (tester) async {
    final api = FakeApi();
    await tester.pumpWidget(screen(api, const DoubtsScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Why is kinetic friction smaller?'), findsOneWidget);
    expect(find.text('Answered'), findsOneWidget);
    expect(find.textContaining('About: Friction · 1 reply'), findsOneWidget);

    await tester.tap(find.widgetWithText(FloatingActionButton, 'Ask a doubt'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), 'Why');
    await tester.tap(find.widgetWithText(FilledButton, 'Send'));
    await tester.pumpAndSettle();
    expect(find.text('Write at least 5 characters.'), findsOneWidget);
    expect(api.bodies['POST /doubts'], isNull);

    await tester.enterText(find.byType(TextFormField), 'How do I draw a free-body diagram for a block on an incline?');
    await tester.tap(find.widgetWithText(FilledButton, 'Send'));
    await tester.pumpAndSettle();
    expect(api.bodies['POST /doubts']!['text'], 'How do I draw a free-body diagram for a block on an incline?');
    expect(find.text('Doubt sent. A teacher will reply soon.'), findsOneWidget);
  });
}
