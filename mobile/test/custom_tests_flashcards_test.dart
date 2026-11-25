import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/practice/flashcards.dart';
import 'package:learnpath/features/tests/custom_test_screen.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

/// Fake endpoints for the generator and flash cards; it remembers what was sent.
class RevisionApi implements HttpClientAdapter {
  Map<String, dynamic>? createdWith;
  final reviews = <String, bool>{};

  ResponseBody _json(Object body) => ResponseBody.fromString(
    jsonEncode(body),
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static Map<String, dynamic> _chapter(
    String id,
    int number,
    String title,
    int easy,
    int medium,
    int hard,
    int locked,
  ) => {
    'id': id,
    'classLevel': '11',
    'number': number,
    'title': title,
    'easy': easy,
    'medium': medium,
    'hard': hard,
    'locked': locked,
  };

  Map<String, dynamic> get _deckSummary => {
    'chapterId': 'n1',
    'classLevel': '11',
    'subject': 'Physics',
    'subjectTitle': 'Physics',
    'number': 1,
    'title': 'Units and Measurement',
    'cards': 3,
    'due': 3 - reviews.values.where((k) => k).length,
    'mastered': 0,
  };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    final route = '${o.method} ${o.path}';
    if (route.startsWith('POST /flashcards/cards/')) {
      final id = o.path.split('/')[3];
      final known = (o.data as Map)['known'] as bool;
      reviews[id] = known;
      return _json({'id': id, 'box': known ? 1 : 0, 'dueAt': '2030-01-01T00:00:00Z', 'mastered': false});
    }
    return switch (route) {
      'GET /tests/custom/options' => _json({
        'maxQuestions': 180,
        'minutesPerQuestion': 1,
        'subjects': [
          {
            'subject': 'Physics',
            'title': 'Physics',
            'chapters': [
              _chapter('n1', 1, 'Units and Measurement', 1, 2, 0, 0),
              _chapter('n4', 4, 'Laws of Motion', 0, 0, 0, 5),
            ],
          },
        ],
      }),
      'POST /tests/custom' => () {
        createdWith = Map<String, dynamic>.from(o.data as Map);
        return _json({
          'id': 't9',
          'title': 'Custom test: Units and Measurement',
          'type': 'Custom',
          'durationMinutes': 5,
          'questionCount': 3,
          'maxMarks': 12,
          'isFree': true,
          'locked': false,
          'attemptsUsed': 0,
          'maxAttempts': null,
          'inProgressAttemptId': null,
          'lastAttemptId': null,
          'availableFrom': null,
          'availableUntil': null,
        });
      }(),
      'GET /flashcards' => _json({
        'decks': [_deckSummary],
        'dueTotal': _deckSummary['due'],
      }),
      'GET /flashcards/n1' => _json({
        'deck': _deckSummary,
        'nextDueAt': null,
        'cards': [
          for (final (id, front, back) in [('c1', 'Q1', 'A1'), ('c2', 'Q2', 'A2'), ('c3', 'Q3', 'A3')])
            {
              'id': id,
              'front': front,
              'back': back,
              'box': reviews[id] == true ? 1 : 0,
              'isNew': !reviews.containsKey(id),
              'isDue': reviews[id] != true,
              'dueAt': null,
            },
        ],
      }),
      _ => ResponseBody.fromString('{}', 404),
    };
  }

  @override
  void close({bool force = false}) {}
}

Future<void> pumpWith(WidgetTester tester, RevisionApi api, List<RouteBase> routes) async {
  tester.view.physicalSize = const Size(1080, 2400);
  tester.view.devicePixelRatio = 2.5;
  addTearDown(tester.view.reset);
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
      ],
      child: MaterialApp.router(
        routerConfig: GoRouter(routes: routes),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('the generator picks chapters, counts and difficulty, then opens the new test', (tester) async {
    final api = RevisionApi();
    await pumpWith(tester, api, [
      GoRoute(path: '/', builder: (_, _) => const CustomTestScreen()),
      GoRoute(
        path: '/test/:id',
        builder: (_, s) => Scaffold(body: Text('Test ${s.pathParameters['id']}')),
      ),
    ]);

    expect(find.text('Create a custom test'), findsOneWidget);
    expect(find.text('Choose at least one chapter'), findsOneWidget);
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Create test')).onPressed, isNull);
    // Only in paid courses: can't be picked.
    final laws = find.widgetWithText(CheckboxListTile, '4. Laws of Motion');
    expect(tester.widget<CheckboxListTile>(laws).onChanged, isNull);
    expect(find.text('Only in paid courses'), findsOneWidget);

    await tester.tap(find.text('1. Units and Measurement'));
    await tester.pumpAndSettle();
    expect(find.text('1 chapter selected'), findsOneWidget);
    expect(tester.widget<ChoiceChip>(find.widgetWithText(ChoiceChip, 'All (3)')).selected, isTrue);
    expect(find.text('3 questions · 5 min'), findsOneWidget);

    // Difficulty narrows what's available.
    await tester.tap(find.text('Easy'));
    await tester.pumpAndSettle();
    expect(find.text('1 question · 5 min'), findsOneWidget);
    await tester.tap(find.text('Any'));
    await tester.pumpAndSettle();
    await tester.tap(find.text("Skip questions I've already got right"));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Create test'));
    await tester.pumpAndSettle();
    expect(find.text('Test t9'), findsOneWidget);
    expect(api.createdWith, {
      'chapterIds': ['n1'],
      'count': 3,
      'skipCorrect': false,
    });
  });

  testWidgets('flash cards flip, take answers by button and swipe, and come back when not known', (tester) async {
    final api = RevisionApi();
    await pumpWith(tester, api, [
      GoRoute(path: '/', builder: (_, _) => const FlashcardDecksScreen()),
      GoRoute(
        path: '/flashcards/:id',
        builder: (_, s) => FlashcardStudyScreen(chapterId: s.pathParameters['id']!),
      ),
    ]);

    expect(find.text('Physics · Class 11'), findsOneWidget);
    expect(find.text('3 cards · 3 cards due'), findsOneWidget);
    // One button for screen readers, not a progress bar.
    expect(tester.getSemantics(find.text('Units and Measurement')), isSemantics(isButton: true));
    await tester.tap(find.text('Units and Measurement'));
    await tester.pumpAndSettle();

    expect(find.text('Card 1 of 3'), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'^Question: Q1\.')), findsOneWidget);
    await tester.tap(find.text('Show answer'));
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('Answer: A1'), findsOneWidget);
    await tester.tap(find.text('Got it'));
    await tester.pumpAndSettle();
    expect(api.reviews, {'c1': true});

    // Tapping the card flips it.
    expect(find.text('Card 2 of 3'), findsOneWidget);
    await tester.tap(find.bySemanticsLabel(RegExp(r'^Question: Q2\.')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Still learning'));
    await tester.pumpAndSettle();
    expect(api.reviews['c2'], isFalse);

    // Swiping right is "got it".
    expect(find.text('Card 3 of 3'), findsOneWidget);
    await tester.tap(find.text('Show answer'));
    await tester.pumpAndSettle();
    await tester.fling(find.bySemanticsLabel('Answer: A3'), const Offset(400, 0), 1500);
    await tester.pumpAndSettle();
    expect(api.reviews['c3'], isTrue);

    expect(find.text('Session done'), findsOneWidget);
    expect(find.text('You knew 2 of 3.'), findsOneWidget);
    expect(find.text('1 card will come back soon.'), findsOneWidget);

    // Studying again brings back only the card still being learnt.
    await tester.tap(find.text('Study again'));
    await tester.pumpAndSettle();
    expect(find.text('Card 1 of 1'), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'^Question: Q2\.')), findsOneWidget);
  });
}
