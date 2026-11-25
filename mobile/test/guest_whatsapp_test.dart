import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/payments/buy_on_website.dart';
import 'package:learnpath/main.dart';
import 'package:material_ui/material_ui.dart';

/// The public endpoints a guest uses; anything that needs an account answers 401.
class GuestApi implements HttpClientAdapter {
  final requests = <String>[];

  ResponseBody _json(Object body, [int status = 200]) => ResponseBody.fromString(
    jsonEncode(body),
    status,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static Map<String, dynamic> _course(String id, String title, {required bool free}) => {
    'id': id,
    'slug': id,
    'examId': 'neet',
    'title': title,
    'description': 'Sample course',
    'classLevel': '11',
    'isFree': free,
    'priceInPaise': free ? 0 : 199900,
    'accessDays': null,
    'enrolled': false,
  };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    requests.add('${o.method} ${o.path}');
    if (o.headers['Authorization'] != null) return _json({'status': 400, 'title': 'Guests send no token'}, 400);
    return switch ('${o.method} ${o.path}') {
      'GET /app-config' => _json({
        'whatsAppNumber': '919876543210',
        'whatsAppChannelUrl': 'https://whatsapp.com/channel/0029VaExample',
        'websiteUrl': 'https://learnpath.example',
      }),
      'GET /library' => _json({
        'tiles': [
          for (final (cls, subject) in [
            ('11', 'Physics'),
            ('11', 'Chemistry'),
            ('11', 'Biology'),
            ('12', 'Physics'),
            ('12', 'Chemistry'),
            ('12', 'Biology'),
          ])
            {
              'classLevel': cls,
              'subject': subject,
              'title': subject,
              'chapters': 10,
              'readyChapters': 1,
              'progressPercent': 0,
            },
        ],
        'resume': null,
      }),
      'GET /courses' => _json({
        'items': [_course('c1', 'Physics Starter', free: true), _course('c2', 'NEET Mechanics', free: false)],
        'page': 1,
        'pageSize': 20,
        'total': 2,
      }),
      'GET /courses/c1/syllabus' => _json({
        'course': _course('c1', 'Physics Starter', free: true),
        'hasAccess': false,
        'completionPercent': 0,
        'subjects': [
          {
            'id': 's1',
            'title': 'Physics',
            'completionPercent': 0,
            'chapters': [
              {
                'id': 'ch1',
                'unit': null,
                'title': 'Units and Measurements',
                'completionPercent': 0,
                'topics': [
                  {
                    'id': 't1',
                    'title': 'Dimensional Analysis',
                    'estimatedMinutes': 20,
                    'completed': false,
                    'resourceCount': 2,
                    'completedResources': 0,
                  },
                ],
              },
            ],
          },
        ],
      }),
      _ => _json({'status': 401, 'code': 'unauthorized', 'title': 'Sign in'}, 401),
    };
  }

  @override
  void close({bool force = false}) {}
}

Widget guestApp(GuestApi api, MemoryTokenStore store, List<Uri> opened) => ProviderScope(
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
  child: const LearnPathApp(),
);

void main() {
  testWidgets('guests browse subjects and courses, meet a sign-up prompt to study, and reach the team on WhatsApp', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.5;
    addTearDown(tester.view.reset);
    final api = GuestApi();
    final store = MemoryTokenStore()..prefs['intro_seen'] = '1';
    final opened = <Uri>[];
    await tester.pumpWidget(guestApp(api, store, opened));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Explore without an account'));
    await tester.pumpAndSettle();
    expect(find.text("You're exploring as a guest"), findsOneWidget);
    expect(store.prefs['guest'], '1');
    expect(find.text('Subjects'), findsOneWidget); // the NCERT grid works without an account
    await tester.scrollUntilVisible(find.text('NEET Mechanics'), 300);
    expect(find.text('Physics Starter'), findsOneWidget);

    // A free course asks them to sign up; it can be shared on WhatsApp with its website link.
    await tester.tap(find.text('Physics Starter'));
    await tester.pumpAndSettle();
    expect(find.text('Sign up free to start'), findsOneWidget);
    await tester.tap(find.byTooltip('Share on WhatsApp'));
    await tester.pumpAndSettle();
    expect(opened.last.host, 'wa.me');
    expect(opened.last.queryParameters['text'], contains('https://learnpath.example/learn/c1'));

    // Studying needs an account: the welcome screen explains, and Back returns to browsing.
    await tester.tap(find.text('Dimensional Analysis')); // the only chapter starts expanded
    await tester.pumpAndSettle();
    expect(
      find.text('Create a free account to watch lessons, practise and take tests. It takes a minute.'),
      findsOneWidget,
    );
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    expect(find.text('Sign up free to start'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    // Practice and Tests explain what a free account adds.
    await tester.tap(find.text('Practice'));
    await tester.pumpAndSettle();
    expect(find.text('Create a free account to continue'), findsOneWidget);
    expect(find.textContaining('flash cards'), findsOneWidget);

    // Profile keeps the settings and the WhatsApp links.
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();
    expect(find.text('Language'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Chat with us on WhatsApp'), 200);
    await tester.tap(find.text('Chat with us on WhatsApp'));
    await tester.pumpAndSettle();
    expect(opened.last.toString(), startsWith('https://wa.me/919876543210?text='));
    expect(opened.last.queryParameters['text'], 'Hi! I have a question about LearnPath.');
    await tester.tap(find.text('Join our WhatsApp channel'));
    await tester.pumpAndSettle();
    expect(opened.last.toString(), 'https://whatsapp.com/channel/0029VaExample');

    // The guest side menu: browsing, WhatsApp, settings and the way in.
    await tester.tap(find.byTooltip('Open navigation menu'));
    await tester.pumpAndSettle();
    expect(find.text('Browse freely. A free account saves your progress.'), findsOneWidget);
    await tester.tap(find.descendant(of: find.byType(Drawer), matching: find.text('Invite friends on WhatsApp')));
    await tester.pumpAndSettle();
    expect(opened.last.queryParameters['text'], contains('https://learnpath.example'));
    expect(find.byType(Drawer), findsNothing, reason: 'the menu closes before opening WhatsApp');

    // Nothing that needs an account was asked for.
    expect(api.requests.where((r) => r.contains('/student/') || r.contains('/notifications')), isEmpty);

    // Next launch carries on browsing.
    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(guestApp(api, store, opened));
    await tester.pumpAndSettle();
    expect(find.text("You're exploring as a guest"), findsOneWidget);
  });
}
