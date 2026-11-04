import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/models.g.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:learnpath/main.dart';
import 'package:learnpath/widgets/question_view.dart';
import 'package:material_ui/material_ui.dart';

/// Minimal fake of the endpoints the sign-in and onboarding flow touches.
class FakeApi implements HttpClientAdapter {
  bool onboarded = false;
  final requests = <String>[];

  ResponseBody _json(Object body, [int status = 200]) =>
      ResponseBody.fromString(jsonEncode(body), status, headers: {Headers.contentTypeHeader: [Headers.jsonContentType]});

  Map<String, dynamic> get _profile => {
        'userId': 'u1',
        'fullName': 'Priya Sharma',
        'email': 'priya@example.com',
        'phone': null,
        'classLevel': null,
        'board': null,
        'targetExamId': onboarded ? 'neet' : null,
        'targetYear': onboarded ? 2027 : null,
        'language': 'en',
        'dailyStudyMinutes': 120,
        'targetScore': null,
        'isMinor': false,
        'guardianName': null,
        'onboardingComplete': onboarded,
        'roles': ['Student'],
      };

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? body, Future<void>? cancel) async {
    requests.add('${o.method} ${o.path}');
    return switch ('${o.method} ${o.path}') {
      'POST /auth/login' => (o.data as Map)['password'] == 'Passw0rd!x'
          ? _json({'accessToken': 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', 'accessTokenExpiresAt': '2099-01-01T00:00:00Z', 'refreshToken': 'r', 'userId': 'u1', 'roles': ['Student'], 'onboardingComplete': false})
          : _json({'status': 401, 'code': 'invalid_credentials', 'title': 'Incorrect email or password.'}, 401),
      'GET /student/profile' => _json(_profile),
      'GET /exams' => _json([
          {'id': 'neet', 'code': 'NEET', 'name': 'NEET (UG)', 'nextExamDate': null},
          {'id': 'jee', 'code': 'JEE_MAIN', 'name': 'JEE Main', 'nextExamDate': null},
        ]),
      'PUT /student/profile' => () {
          onboarded = true;
          return _json(_profile);
        }(),
      _ => _json({'status': 404, 'code': 'not_found', 'title': 'Not found'}, 404),
    };
  }

  @override
  void close({bool force = false}) {}
}

Widget app(FakeApi api, MemoryTokenStore store) => ProviderScope(
      retry: (_, _) => null,
      overrides: [
        tokenStoreProvider.overrideWithValue(store),
        apiClientProvider.overrideWith((ref) => ApiClient(baseUrl: 'https://api.test', store: store, language: () => ref.read(localeProvider).languageCode, adapter: api)),
      ],
      child: const LearnPathApp(),
    );

void main() {
  testWidgets('welcome screen switches the whole UI to Hindi', (tester) async {
    await tester.pumpWidget(app(FakeApi(), MemoryTokenStore()));
    await tester.pumpAndSettle();
    expect(find.text('Continue with mobile number'), findsOneWidget);

    await tester.tap(find.text('हिंदी'));
    await tester.pumpAndSettle();
    expect(find.text('मोबाइल नंबर से जारी रखें'), findsOneWidget);
    expect(find.text('NEET, JEE और बोर्ड परीक्षाओं की तैयारी करें'), findsOneWidget);
  });

  testWidgets('email sign-in validates, shows server errors, then routes a new student to onboarding', (tester) async {
    final api = FakeApi();
    final store = MemoryTokenStore();
    await tester.pumpWidget(app(api, store));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Sign in with email'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.text('This field is required'), findsNWidgets(2));
    expect(api.requests.where((r) => r.contains('/auth/login')), isEmpty);

    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'priya@example.com');
    await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'wrong-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.text('Incorrect email or password.'), findsOneWidget);
    expect(store.tokens, isNull);

    await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'Passw0rd!x');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(store.tokens?.refreshToken, 'r');
    expect(find.text('Which exam are you preparing for?'), findsOneWidget);
    await tester.pump(const Duration(seconds: 5)); // let the earlier error snackbar time out
    await tester.pumpAndSettle();

    // Onboarding requires an exam before moving on.
    await tester.tap(find.widgetWithText(FilledButton, 'Next'));
    await tester.pumpAndSettle();
    expect(find.text('Choose an exam to continue'), findsOneWidget);
    await tester.tap(find.text('NEET (UG)'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Next'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Next'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Get started'));
    await tester.pumpAndSettle();
    expect(api.requests, contains('PUT /student/profile'));
    // Onboarding complete: the router sends the student to the tabbed home.
    expect(find.byType(NavigationBar), findsOneWidget);
    expect(find.text('Practice'), findsOneWidget);
  });

  testWidgets('single-correct options are exclusive; multiple-correct options toggle', (tester) async {
    AnswerResponse? response;
    Widget view(String type) => MaterialApp(
          home: Scaffold(
            body: StatefulBuilder(
              builder: (context, setState) => QuestionView(
                type: type,
                stem: 'Pick',
                options: const [PlayerOptionDto(id: 'a', text: 'Alpha'), PlayerOptionDto(id: 'b', text: 'Beta')],
                response: response,
                onChanged: (r) => setState(() => response = r),
              ),
            ),
          ),
          localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
        );

    await tester.pumpWidget(view('SingleCorrect'));
    await tester.tap(find.text('Alpha'));
    await tester.pump();
    await tester.tap(find.text('Beta'));
    await tester.pump();
    expect(response!.selectedOptionIds, ['b']);

    response = null;
    await tester.pumpWidget(view('MultipleCorrect'));
    await tester.tap(find.text('Alpha'));
    await tester.pump();
    await tester.tap(find.text('Beta'));
    await tester.pump();
    expect(response!.selectedOptionIds!.toSet(), {'a', 'b'});
    await tester.tap(find.text('Alpha'));
    await tester.pump();
    expect(response!.selectedOptionIds, ['b']);
  });
}
