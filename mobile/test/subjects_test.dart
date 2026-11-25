import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/models.g.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/learn/subjects.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

class LibraryApi implements HttpClientAdapter {
  ResponseBody _json(Object body) => ResponseBody.fromString(
    jsonEncode(body),
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static Map<String, dynamic> _tile(String cls, String subject, int chapters, int ready, int progress) => {
    'classLevel': cls,
    'subject': subject,
    'title': subject,
    'chapters': chapters,
    'readyChapters': ready,
    'progressPercent': progress,
  };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    return switch (o.path) {
      '/library' => _json({
        'tiles': [
          _tile('11', 'Physics', 14, 2, 25),
          _tile('11', 'Chemistry', 9, 0, 0),
          _tile('11', 'Biology', 19, 0, 0),
          _tile('12', 'Physics', 14, 0, 0),
          _tile('12', 'Chemistry', 10, 0, 0),
          _tile('12', 'Biology', 13, 0, 0),
        ],
        'resume': {'chapterId': 'n1', 'title': 'Units and Measurement', 'classLevel': '11', 'subject': 'Physics'},
      }),
      '/library/11/physics' => _json({
        'classLevel': '11',
        'subject': 'Physics',
        'title': 'Physics',
        'progressPercent': 25,
        'chapters': [
          {
            'id': 'n1',
            'number': 1,
            'title': 'Units and Measurement',
            'topics': 1,
            'videos': 1,
            'notes': 1,
            'ready': true,
            'locked': false,
            'progressPercent': 50,
          },
          {
            'id': 'n4',
            'number': 4,
            'title': 'Laws of Motion',
            'topics': 2,
            'videos': 2,
            'notes': 0,
            'ready': true,
            'locked': true,
            'progressPercent': 0,
          },
          {
            'id': 'n7',
            'number': 7,
            'title': 'Gravitation',
            'topics': 0,
            'videos': 0,
            'notes': 0,
            'ready': false,
            'locked': false,
            'progressPercent': 0,
          },
        ],
      }),
      '/library/chapters/n1' => _json({
        'id': 'n1',
        'number': 1,
        'title': 'Units and Measurement',
        'classLevel': '11',
        'subject': 'Physics',
        'subjectTitle': 'Physics',
        'previousChapterId': null,
        'nextChapterId': 'n2',
        'practiceQuestionCount': 3,
        'canPractise': true,
        'buyCourseId': null,
        'topics': [
          {
            'id': 't1',
            'title': 'Dimensional Analysis',
            'courseId': 'c1',
            'course': 'Physics Starter',
            'hasAccess': true,
            'resources': [
              {
                'id': 'r1',
                'type': 'Video',
                'title': 'Dimensions explained',
                'language': 'en',
                'durationSeconds': 540,
                'isFreePreview': false,
                'locked': false,
                'completed': true,
                'positionSeconds': 0,
              },
              {
                'id': 'r2',
                'type': 'Document',
                'title': 'Summary notes',
                'language': 'en',
                'durationSeconds': null,
                'isFreePreview': false,
                'locked': false,
                'completed': false,
                'positionSeconds': 0,
              },
            ],
          },
        ],
      }),
      _ => ResponseBody.fromString('{}', 404),
    };
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  testWidgets('grid → subject → chapter tabs, with continue and practice', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.5;
    addTearDown(tester.view.reset);
    final store = MemoryTokenStore()
      ..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
    final practice = <CreatePracticeRequest>[];
    final router = GoRouter(
      routes: [
        GoRoute(
          path: '/',
          builder: (_, _) => Scaffold(body: ListView(children: const [SubjectGrid()])),
        ),
        GoRoute(
          path: '/subject/:cls/:subject',
          builder: (_, s) => SubjectScreen(classLevel: s.pathParameters['cls']!, subject: s.pathParameters['subject']!),
        ),
        GoRoute(
          path: '/ncert/:id',
          builder: (_, s) => NcertChapterScreen(chapterId: s.pathParameters['id']!),
        ),
        GoRoute(
          path: '/practice/session',
          builder: (_, s) {
            practice.add(s.extra! as CreatePracticeRequest);
            return const Scaffold(body: Text('Practice started'));
          },
        ),
      ],
    );
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: [
          tokenStoreProvider.overrideWithValue(store),
          apiClientProvider.overrideWith(
            (ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: LibraryApi()),
          ),
        ],
        child: MaterialApp.router(
          routerConfig: router,
          supportedLocales: AppLocalizations.supportedLocales,
          localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Subjects'), findsOneWidget);
    expect(find.text('Continue: Units and Measurement'), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'^Physics, Class 11: 2 of 14 chapters ready$')), findsOneWidget);
    expect(find.text('0 of 19 chapters ready'), findsOneWidget);

    await tester.tap(find.byType(ProgressRing).first); // the Physics XI tile
    await tester.pumpAndSettle();
    expect(find.text('Physics · Class 11'), findsOneWidget);
    expect(find.text('Lessons coming soon'), findsOneWidget);
    expect(find.byIcon(Icons.lock_outline_rounded), findsOneWidget);

    await tester.tap(find.text('Units and Measurement'));
    await tester.pumpAndSettle();
    expect(find.text('Chapter 1'), findsOneWidget);
    expect(find.text('Dimensions explained'), findsOneWidget);
    expect(find.text('From Physics Starter'), findsOneWidget);

    await tester.tap(find.text('Notes'));
    await tester.pumpAndSettle();
    expect(find.text('Summary notes'), findsOneWidget);

    await tester.tap(find.text('Practice'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Practise this chapter (3 questions)'));
    await tester.pumpAndSettle();
    expect(find.text('Practice started'), findsOneWidget);
    expect(practice.single.mode, 'ncert');
    expect(practice.single.ncertChapterId, 'n1');
  });
}
