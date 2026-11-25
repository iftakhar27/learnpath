import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:learnpath/app/theme.dart';
import 'package:learnpath/core/api/models.g.dart';
import 'package:learnpath/core/api/token_store.dart';
import 'package:learnpath/core/providers.dart';
import 'package:learnpath/features/auth/auth_screens.dart';
import 'package:learnpath/features/home/app_drawer.dart';
import 'package:learnpath/features/home/notification_bell.dart';
import 'package:learnpath/features/onboarding/onboarding_screen.dart';
import 'package:learnpath/l10n/app_localizations.dart';
import 'package:material_ui/material_ui.dart';

import 'app_flow_test.dart' show FakeApi, app;

class _ThreeUnread extends UnreadCountController {
  @override
  int build() => 3;
}

const _profile = ProfileDto(
  userId: 'u1',
  fullName: 'Priya Sharma',
  email: 'priya@example.com',
  targetExamId: 'neet',
  targetYear: 2027,
  language: 'en',
  dailyStudyMinutes: 120,
  isMinor: false,
  onboardingComplete: true,
  roles: ['Student'],
);

/// The tab bar scaffold with the side menu, stand-in tab pages and the pages it opens, themed like the real app.
Widget drawerHarness(MemoryTokenStore store) {
  final router = GoRouter(
    initialLocation: '/home',
    routes: [
      ShellRoute(
        builder: (_, _, child) => Scaffold(
          drawer: const AppDrawer(),
          body: child,
          bottomNavigationBar: const SizedBox(height: 80, child: Text('tab bar')),
        ),
        routes: [
          for (final tab in ['home', 'learn', 'practice', 'tests', 'profile'])
            GoRoute(
              path: '/$tab',
              builder: (_, _) => Scaffold(
                appBar: AppBar(leading: const MenuButton(), title: Text('$tab page')),
              ),
            ),
        ],
      ),
      for (final page in ['plan', 'progress', 'bookmarks', 'doubts', 'notifications', 'orders', 'support'])
        GoRoute(
          path: '/$page',
          builder: (_, _) => Scaffold(appBar: AppBar(title: Text('$page page'))),
        ),
    ],
  );
  return ProviderScope(
    retry: (_, _) => null,
    overrides: [
      tokenStoreProvider.overrideWithValue(store),
      profileProvider.overrideWithValue(_profile),
      unreadCountProvider.overrideWith(_ThreeUnread.new),
      examsProvider.overrideWith((ref) async => const [ExamDto(id: 'neet', code: 'NEET', name: 'NEET (UG)')]),
    ],
    child: Consumer(
      builder: (context, ref, _) => MaterialApp.router(
        routerConfig: router,
        theme: buildTheme(Brightness.light),
        darkTheme: buildTheme(Brightness.dark),
        themeMode: ref.watch(themeModeProvider),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
      ),
    ),
  );
}

Brightness brightnessOf(WidgetTester tester) => Theme.of(tester.element(find.byType(AppBar).last)).brightness;

void main() {
  testWidgets('first launch shows the intro slides once, then the welcome screen', (tester) async {
    final store = MemoryTokenStore();
    await tester.pumpWidget(app(FakeApi(), store));
    await tester.pumpAndSettle();

    expect(find.text('Every NCERT chapter, one tap away'), findsOneWidget);
    expect(find.bySemanticsLabel('Slide 1 of 4'), findsOneWidget);
    expect(find.text('Continue with mobile number'), findsNothing);

    // The language can be chosen on the first screen.
    await tester.tap(find.text('हिंदी'));
    await tester.pumpAndSettle();
    expect(find.text('हर NCERT अध्याय, बस एक टैप पर'), findsOneWidget);
    await tester.tap(find.text('English'));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Next'));
    await tester.pumpAndSettle();
    expect(find.text('Practise the NEET way'), findsOneWidget);
    expect(find.bySemanticsLabel('Slide 2 of 4'), findsOneWidget);

    // Swiping works too.
    await tester.drag(find.byType(PageView), const Offset(-500, 0));
    await tester.pumpAndSettle();
    expect(find.text('Stuck? Ask a doubt'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Next'));
    await tester.pumpAndSettle();
    expect(find.text('Plan, track, improve'), findsOneWidget);
    expect(find.text('Skip'), findsNothing, reason: 'the last slide has only "Get started"');

    await tester.tap(find.widgetWithText(FilledButton, 'Get started'));
    await tester.pumpAndSettle();
    expect(find.text('Continue with mobile number'), findsOneWidget);
    expect(store.prefs['intro_seen'], '1');

    // Next launch goes straight to the welcome screen.
    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(app(FakeApi(), store));
    await tester.pumpAndSettle();
    expect(find.text('Continue with mobile number'), findsOneWidget);
    expect(find.text('Every NCERT chapter, one tap away'), findsNothing);
  });

  testWidgets('skip leaves the intro from the first slide', (tester) async {
    final store = MemoryTokenStore();
    await tester.pumpWidget(app(FakeApi(), store));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Skip'));
    await tester.pumpAndSettle();
    expect(find.text('Continue with mobile number'), findsOneWidget);
    expect(store.prefs['intro_seen'], '1');
  });

  testWidgets('students already signed in never get the intro after signing out', (tester) async {
    final store = MemoryTokenStore()
      ..tokens = const StoredTokens(accessToken: 'a.eyJleHAiOjk5OTk5OTk5OTl9.s', refreshToken: 'r');
    await tester.pumpWidget(app(FakeApi(), store));
    await tester.pumpAndSettle();
    expect(find.text('Which exam are you preparing for?'), findsOneWidget);
    expect(store.prefs['intro_seen'], '1');
  });

  testWidgets('the side menu shows the student, switches tabs, opens pages and turns on night mode', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.5;
    addTearDown(tester.view.reset);
    final store = MemoryTokenStore();
    await tester.pumpWidget(drawerHarness(store));
    await tester.pumpAndSettle();
    expect(brightnessOf(tester), Brightness.light);

    await tester.tap(find.byTooltip('Open navigation menu'));
    await tester.pumpAndSettle();
    expect(find.text('Priya Sharma'), findsOneWidget);
    expect(
      tester.getRect(find.byType(Drawer)).bottom,
      tester.getRect(find.text('tab bar')).bottom,
      reason: 'the menu covers the tab bar',
    );
    expect(find.text('Preparing for NEET (UG) 2027'), findsOneWidget);
    expect(tester.widget<ListTile>(find.widgetWithText(ListTile, 'Home')).selected, isTrue);
    expect(
      find.descendant(of: find.widgetWithText(ListTile, 'Notifications'), matching: find.text('3')),
      findsOneWidget,
    );

    // A page opens on top; the menu is closed when the student comes back.
    await tester.tap(find.text('Study plan'));
    await tester.pumpAndSettle();
    expect(find.text('plan page'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();
    expect(find.byType(Drawer), findsNothing);

    // Night mode switches the whole app and is remembered.
    await tester.tap(find.byTooltip('Open navigation menu'));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.widgetWithText(SwitchListTile, 'Night mode'),
      200,
      scrollable: find.descendant(of: find.byType(Drawer), matching: find.byType(Scrollable)),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(SwitchListTile, 'Night mode'));
    await tester.pumpAndSettle();
    expect(brightnessOf(tester), Brightness.dark);
    expect(store.prefs['theme'], 'dark');
    expect(tester.widget<SwitchListTile>(find.byType(SwitchListTile)).value, isTrue);
    await tester.scrollUntilVisible(
      find.text('Change password'),
      200,
      scrollable: find.descendant(of: find.byType(Drawer), matching: find.byType(Scrollable)),
    );
    expect(find.text('Sign out'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Subjects & courses'),
      -200,
      scrollable: find.descendant(of: find.byType(Drawer), matching: find.byType(Scrollable)),
    );

    await tester.ensureVisible(find.text('Subjects & courses'));
    await tester.pumpAndSettle();

    // Tabs switch in place.
    await tester.tap(find.text('Subjects & courses'));
    await tester.pumpAndSettle();
    expect(find.text('learn page'), findsOneWidget);
    expect(find.byType(Drawer), findsNothing);
  });

  testWidgets('the theme setting is restored on the next launch and can follow the phone again', (tester) async {
    final store = MemoryTokenStore()..prefs['theme'] = 'dark';
    await tester.pumpWidget(
      ProviderScope(
        overrides: [tokenStoreProvider.overrideWithValue(store)],
        child: Consumer(
          builder: (context, ref, _) => MaterialApp(
            theme: buildTheme(Brightness.light),
            darkTheme: buildTheme(Brightness.dark),
            themeMode: ref.watch(themeModeProvider),
            localizationsDelegates: [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
            home: Scaffold(
              appBar: AppBar(title: const Text('Profile')),
              body: const Center(child: ThemeModeToggle()),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(brightnessOf(tester), Brightness.dark);

    await tester.tap(find.text('Light'));
    await tester.pumpAndSettle();
    expect(brightnessOf(tester), Brightness.light);
    expect(store.prefs['theme'], 'light');

    await tester.tap(find.text('Auto'));
    await tester.pumpAndSettle();
    expect(store.prefs['theme'], 'system');
    expect(brightnessOf(tester), Brightness.light, reason: 'the test phone is in light mode');
  });
}
