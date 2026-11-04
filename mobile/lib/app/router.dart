import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../core/api/models.g.dart';
import '../core/providers.dart';
import '../features/auth/auth_screens.dart';
import '../features/home/home_screen.dart';
import '../features/learn/learn_screens.dart';
import '../features/learn/video_screen.dart';
import '../features/onboarding/onboarding_screen.dart';
import '../features/practice/practice_screens.dart';
import '../features/profile/profile_screens.dart';
import '../features/tests/test_screens.dart';
import '../widgets/common.dart';

final _rootKey = GlobalKey<NavigatorState>();
const _publicRoutes = {'/welcome', '/login', '/register', '/otp'};

/// Bridges session changes into go_router's redirect.
class _SessionListenable extends ChangeNotifier {
  _SessionListenable(Ref ref) {
    ref.listen(sessionProvider, (_, _) => notifyListeners());
  }
}

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _SessionListenable(ref);
  ref.onDispose(refresh.dispose);

  return GoRouter(
    navigatorKey: _rootKey,
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, state) {
      final session = ref.read(sessionProvider);
      final loc = state.matchedLocation;
      return switch (session) {
        SessionLoading() || SessionUnreachable() => loc == '/splash' ? null : '/splash',
        SignedOut() => _publicRoutes.contains(loc) ? null : '/welcome',
        SignedIn(:final profile) when !profile.onboardingComplete => loc == '/onboarding' ? null : '/onboarding',
        SignedIn() => (loc == '/splash' || loc == '/onboarding' || _publicRoutes.contains(loc)) ? '/home' : null,
      };
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/welcome', builder: (_, _) => const WelcomeScreen()),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      GoRoute(path: '/register', builder: (_, _) => const RegisterScreen()),
      GoRoute(path: '/otp', builder: (_, _) => const OtpScreen()),
      GoRoute(path: '/onboarding', builder: (_, _) => const OnboardingScreen()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => _HomeShell(shell: shell),
        branches: [
          StatefulShellBranch(
            routes: [GoRoute(path: '/home', builder: (_, _) => const HomeScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/learn', builder: (_, _) => const CatalogScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/practice', builder: (_, _) => const PracticeHomeScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/tests', builder: (_, _) => const TestsScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/profile', builder: (_, _) => const ProfileScreen())],
          ),
        ],
      ),
      // Full-screen pages above the tab bar.
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/course/:id',
        builder: (_, s) => CourseScreen(courseId: s.pathParameters['id']!),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/topic/:id',
        builder: (_, s) => TopicScreen(topicId: s.pathParameters['id']!),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/video/:id',
        builder: (_, s) => VideoScreen(resourceId: s.pathParameters['id']!),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/practice/session',
        builder: (_, s) => PracticeSessionScreen(request: s.extra as CreatePracticeRequest? ?? const CreatePracticeRequest(mode: 'incorrect')),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/test/:id',
        builder: (_, s) => TestIntroScreen(testId: s.pathParameters['id']!),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/exam/:id',
        builder: (_, s) => ExamScreen(attemptId: s.pathParameters['id']!, initial: s.extra as AttemptDto?),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/result/:id',
        builder: (_, s) => ResultScreen(attemptId: s.pathParameters['id']!),
      ),
      GoRoute(
        parentNavigatorKey: _rootKey,
        path: '/review/:id',
        builder: (_, s) => ReviewScreen(attemptId: s.pathParameters['id']!),
      ),
      GoRoute(parentNavigatorKey: _rootKey, path: '/plan', builder: (_, _) => const StudyPlanScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/progress', builder: (_, _) => const ProgressScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/preferences', builder: (_, _) => const PreferencesScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/notifications', builder: (_, _) => const NotificationsScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/bookmarks', builder: (_, _) => const BookmarksScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/orders', builder: (_, _) => const OrdersScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/support', builder: (_, _) => const SupportScreen()),
      GoRoute(parentNavigatorKey: _rootKey, path: '/search', builder: (_, _) => const SearchScreen()),
    ],
  );
});

class _HomeShell extends StatelessWidget {
  const _HomeShell({required this.shell});
  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        onDestinationSelected: (i) => shell.goBranch(i, initialLocation: i == shell.currentIndex),
        destinations: [
          NavigationDestination(icon: const Icon(Icons.home_outlined), selectedIcon: const Icon(Icons.home_rounded), label: l.tabHome),
          NavigationDestination(icon: const Icon(Icons.menu_book_outlined), selectedIcon: const Icon(Icons.menu_book_rounded), label: l.tabLearn),
          NavigationDestination(icon: const Icon(Icons.edit_note_outlined), selectedIcon: const Icon(Icons.edit_note_rounded), label: l.tabPractice),
          NavigationDestination(icon: const Icon(Icons.timer_outlined), selectedIcon: const Icon(Icons.timer_rounded), label: l.tabTests),
          NavigationDestination(icon: const Icon(Icons.person_outline_rounded), selectedIcon: const Icon(Icons.person_rounded), label: l.tabProfile),
        ],
      ),
    );
  }
}
