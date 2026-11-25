import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/models.g.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/config.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/learn/materials_screen.dart';
import 'package:learnpath/features/payments/buy_on_website.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart' hide MaterialType;

class MaterialsApi implements HttpClientAdapter {
  final queries = <Map<String, dynamic>>[];

  ResponseBody _json(Object body) => ResponseBody.fromString(
    jsonEncode(body),
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static const _all = [
    {
      'id': 'm1',
      'title': 'Units formula sheet',
      'description': null,
      'type': 'FormulaSheet',
      'subject': 'Physics',
      'classLevel': '11',
      'format': 'PDF',
      'sizeBytes': 1258291,
      'downloads': 3,
    },
    {
      'id': 'm2',
      'title': 'Cell key points',
      'description': 'One page',
      'type': 'Notes',
      'subject': 'Biology',
      'classLevel': '11',
      'format': 'PDF',
      'sizeBytes': 40960,
      'downloads': 0,
    },
    {
      'id': 'm3',
      'title': 'NCERT textbooks',
      'description': null,
      'type': 'Other',
      'subject': null,
      'classLevel': null,
      'format': 'Link',
      'sizeBytes': 0,
      'downloads': 9,
    },
  ];

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    if (o.path == '/materials') {
      queries.add(Map.of(o.queryParameters));
      final subject = o.queryParameters['subject'];
      return _json([
        for (final m in _all)
          if (subject == null || m['subject'] == subject) m,
      ]);
    }
    return switch ('${o.method} ${o.path}') {
      'POST /materials/m1/download' => _json({
        'url': '/api/v1/materials/m1/file?expires=99&sig=abc',
        'external': false,
        'fileName': 'units.pdf',
        'expiresAt': '2030-01-01T00:00:00Z',
      }),
      'POST /materials/m3/download' => _json({
        'url': 'https://ncert.nic.in/textbook.php',
        'external': true,
        'fileName': null,
        'expiresAt': null,
      }),
      _ => ResponseBody.fromString('{}', 404),
    };
  }

  @override
  void close({bool force = false}) {}
}

class _Session extends SessionController {
  _Session(this.initial);
  final SessionState initial;
  @override
  SessionState build() => initial;
}

const _profile = ProfileDto(
  userId: 'u1',
  fullName: 'Priya Sharma',
  language: 'en',
  dailyStudyMinutes: 60,
  isMinor: false,
  onboardingComplete: true,
  roles: ['Student'],
);

Future<void> pump(WidgetTester tester, MaterialsApi api, SessionState session, List<Uri> opened) async {
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
        sessionProvider.overrideWith(() => _Session(session)),
        apiClientProvider.overrideWith(
          (ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => 'en', adapter: api),
        ),
        openUrlProvider.overrideWithValue((uri) async {
          opened.add(uri);
          return true;
        }),
      ],
      child: MaterialApp.router(
        routerConfig: GoRouter(
          routes: [
            GoRoute(path: '/', builder: (_, _) => const MaterialsScreen()),
            GoRoute(
              path: '/register',
              builder: (_, _) => const Scaffold(body: Text('Register page')),
            ),
          ],
        ),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('students filter material and download it through a link that opens in the browser', (tester) async {
    final api = MaterialsApi();
    final opened = <Uri>[];
    await pump(tester, api, const SignedIn(_profile), opened);

    expect(find.text('Free study material'), findsOneWidget);
    expect(find.text('Units formula sheet'), findsOneWidget);
    expect(find.text('PDF · 1.2 MB'), findsOneWidget);
    expect(find.text('Physics · Class 11 · Formula sheet'), findsOneWidget);
    expect(find.text('General · Class 11 & 12 · Other'), findsOneWidget);
    expect(find.text('Opens another website'), findsOneWidget);

    await tester.ensureVisible(find.widgetWithText(ChoiceChip, 'Biology'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ChoiceChip, 'Biology'));
    await tester.pumpAndSettle();
    expect(api.queries.last, {'subject': 'Biology'});
    expect(find.text('Units formula sheet'), findsNothing);
    expect(find.text('Cell key points'), findsOneWidget);
    await tester.ensureVisible(find.widgetWithText(ChoiceChip, 'All subjects'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ChoiceChip, 'All subjects'));
    await tester.pumpAndSettle();

    await tester.tap(find.bySemanticsLabel('Download: Units formula sheet'));
    await tester.pumpAndSettle();
    expect(opened.last.toString(), '${AppConfig.apiBaseUrl}/api/v1/materials/m1/file?expires=99&sig=abc');
    await tester.tap(find.bySemanticsLabel('Open: NCERT textbooks'));
    await tester.pumpAndSettle();
    expect(opened.last.toString(), 'https://ncert.nic.in/textbook.php');
  });

  testWidgets('guests see the list and are asked to sign up to download', (tester) async {
    final opened = <Uri>[];
    await pump(tester, MaterialsApi(), const Guest(), opened);
    expect(find.text('Units formula sheet'), findsOneWidget);
    expect(find.text('Download'), findsNothing);
    await tester.tap(find.text('Sign up free to download').first);
    await tester.pumpAndSettle();
    expect(find.text('Register page'), findsOneWidget);
    expect(opened, isEmpty);
  });
}
