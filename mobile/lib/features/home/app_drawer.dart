import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/config.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../auth/auth_screens.dart' show LanguageToggle;
import '../onboarding/onboarding_screen.dart' show examsProvider;
import '../support/whatsapp.dart';
import 'notification_bell.dart';

/// Opens the side menu from a tab screen's app bar. The menu belongs to the outer (tab bar) scaffold so that it covers
/// the whole screen, bottom tabs included; a tab's own app bar would only look for its own scaffold.
class MenuButton extends StatelessWidget {
  const MenuButton({super.key});

  @override
  Widget build(BuildContext context) => IconButton(
    icon: const Icon(Icons.menu_rounded),
    tooltip: MaterialLocalizations.of(context).openAppDrawerTooltip,
    onPressed: () {
      ScaffoldState? outermost;
      context.visitAncestorElements((e) {
        if (e is StatefulElement && e.state is ScaffoldState && (e.state as ScaffoldState).hasDrawer) {
          outermost = e.state as ScaffoldState;
        }
        return true;
      });
      outermost?.openDrawer();
    },
  );
}

/// The side menu: everything in the app one tap away, plus night mode and language.
class AppDrawer extends ConsumerWidget {
  const AppDrawer({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(sessionProvider) is Guest) return const _GuestDrawer();
    final l = context.l10n;
    final p = ref.watch(profileProvider);
    final scheme = Theme.of(context).colorScheme;
    final here = GoRouter.of(context).routerDelegate.currentConfiguration.uri.path;
    final unread = ref.watch(unreadCountProvider);
    final examName = ref.watch(examsProvider).value?.where((e) => e.id == p.targetExamId).firstOrNull?.name;
    final dark = Theme.of(context).brightness == Brightness.dark;

    void close() => Scaffold.of(context).closeDrawer();
    // Tabs switch in place; other pages open on top, so the menu closes first and is gone when the student comes back.
    Widget tab(IconData icon, String title, String route) => ListTile(
      leading: Icon(icon),
      title: Text(title),
      selected: here == route,
      onTap: () {
        close();
        if (here != route) context.go(route);
      },
    );
    Widget page(IconData icon, String title, String route, {Widget? trailing}) => ListTile(
      leading: Icon(icon),
      title: Text(title),
      trailing: trailing,
      onTap: () {
        close();
        unawaited(context.push(route));
      },
    );
    Widget heading(String text) => Padding(
      padding: const EdgeInsets.fromLTRB(28, 16, 16, 4),
      child: Semantics(
        header: true,
        child: Text(text, style: Theme.of(context).textTheme.labelLarge?.copyWith(color: scheme.onSurfaceVariant)),
      ),
    );

    return Drawer(
      child: ListView(
        padding: EdgeInsets.zero,
        children: [
          Material(
            color: scheme.primaryContainer,
            child: InkWell(
              onTap: () {
                close();
                context.go('/profile');
              },
              child: Padding(
                padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 20, 20, 20),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 28,
                      backgroundColor: scheme.primary,
                      foregroundColor: scheme.onPrimary,
                      child: Text(
                        p.fullName.isEmpty ? '?' : p.fullName.characters.first.toUpperCase(),
                        style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w600),
                      ),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            p.fullName,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: Theme.of(context).textTheme.titleMedium
                                ?.copyWith(color: scheme.onPrimaryContainer, fontWeight: FontWeight.w600),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            examName != null
                                ? l.preparingFor(examName, '${p.targetYear ?? ''}').trim()
                                : (p.email ?? p.phone ?? ''),
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(color: scheme.onPrimaryContainer),
                          ),
                        ],
                      ),
                    ),
                    Icon(Icons.chevron_right_rounded, color: scheme.onPrimaryContainer),
                  ],
                ),
              ),
            ),
          ),
          heading(l.menuStudy),
          tab(Icons.home_outlined, l.tabHome, '/home'),
          tab(Icons.menu_book_outlined, l.menuSubjectsCourses, '/learn'),
          tab(Icons.edit_note_outlined, l.tabPractice, '/practice'),
          page(Icons.style_outlined, l.flashcardsTitle, '/flashcards'),
          tab(Icons.timer_outlined, l.testsTitle, '/tests'),
          page(Icons.tune_rounded, l.customTestTitle, '/tests/custom'),
          page(Icons.event_note_rounded, l.planTitle, '/plan'),
          page(Icons.insights_rounded, l.myProgress, '/progress'),
          page(Icons.bookmark_outline_rounded, l.bookmarksTitle, '/bookmarks'),
          const Divider(indent: 16, endIndent: 16),
          heading(l.menuHelp),
          page(Icons.forum_outlined, l.askDoubt, '/doubts'),
          page(
            Icons.notifications_none_rounded,
            l.notificationsTitle,
            '/notifications',
            trailing: unread > 0 ? Badge(label: Text(unread > 99 ? '99+' : '$unread')) : null,
          ),
          page(Icons.receipt_long_outlined, l.myPurchases, '/orders'),
          page(Icons.support_agent_rounded, l.helpSupport, '/support'),
          WhatsAppTiles(beforeOpen: close),
          const Divider(indent: 16, endIndent: 16),
          heading(l.menuSettings),
          SwitchListTile(
            secondary: Icon(dark ? Icons.dark_mode_rounded : Icons.dark_mode_outlined),
            title: Text(l.nightMode),
            value: dark,
            onChanged: (on) => ref.read(themeModeProvider.notifier).set(on ? ThemeMode.dark : ThemeMode.light),
          ),
          ListTile(
            leading: const Icon(Icons.translate_rounded),
            title: Text(l.language),
            trailing: const LanguageToggle(),
          ),
          page(Icons.tune_rounded, l.editPreferences, '/preferences'),
          // Mobile-number accounts sign in with one-time codes and have no password.
          if (p.email != null) page(Icons.password_rounded, l.changePassword, '/change-password'),
          ListTile(
            leading: const Icon(Icons.privacy_tip_outlined),
            title: Text(l.privacyPolicy),
            trailing: const Icon(Icons.open_in_new_rounded, size: 18),
            onTap: () => launchUrl(Uri.parse(AppConfig.privacyPolicyUrl), mode: LaunchMode.externalApplication),
          ),
          const Divider(indent: 16, endIndent: 16),
          ListTile(
            leading: const Icon(Icons.logout_rounded),
            title: Text(l.signOut),
            onTap: () {
              close();
              unawaited(ref.read(sessionProvider.notifier).logout());
            },
          ),
          SizedBox(height: MediaQuery.paddingOf(context).bottom + 8),
        ],
      ),
    );
  }
}

/// The side menu while exploring without an account: what can be browsed, settings, and the way in.
class _GuestDrawer extends ConsumerWidget {
  const _GuestDrawer();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final here = GoRouter.of(context).routerDelegate.currentConfiguration.uri.path;
    final dark = Theme.of(context).brightness == Brightness.dark;
    void close() => Scaffold.of(context).closeDrawer();
    void go(String route) {
      close();
      if (here != route) context.go(route);
    }

    void push(String route) {
      close();
      unawaited(context.push(route));
    }

    return Drawer(
      child: ListView(
        padding: EdgeInsets.zero,
        children: [
          Container(
            color: scheme.primaryContainer,
            padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 20, 20, 20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  l.guestTitle,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(color: scheme.onPrimaryContainer, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 4),
                Text(l.guestDrawerBody, style: TextStyle(color: scheme.onPrimaryContainer)),
                const SizedBox(height: 12),
                FilledButton(onPressed: () => push('/register'), child: Text(l.createFreeAccount)),
              ],
            ),
          ),
          ListTile(leading: const Icon(Icons.home_outlined), title: Text(l.tabHome), selected: here == '/home', onTap: () => go('/home')),
          ListTile(
            leading: const Icon(Icons.menu_book_outlined),
            title: Text(l.menuSubjectsCourses),
            selected: here == '/learn',
            onTap: () => go('/learn'),
          ),
          const Divider(indent: 16, endIndent: 16),
          WhatsAppTiles(beforeOpen: close),
          const Divider(indent: 16, endIndent: 16),
          SwitchListTile(
            secondary: Icon(dark ? Icons.dark_mode_rounded : Icons.dark_mode_outlined),
            title: Text(l.nightMode),
            value: dark,
            onChanged: (on) => ref.read(themeModeProvider.notifier).set(on ? ThemeMode.dark : ThemeMode.light),
          ),
          ListTile(leading: const Icon(Icons.translate_rounded), title: Text(l.language), trailing: const LanguageToggle()),
          const Divider(indent: 16, endIndent: 16),
          ListTile(leading: const Icon(Icons.login_rounded), title: Text(l.signIn), onTap: () => push('/welcome')),
          SizedBox(height: MediaQuery.paddingOf(context).bottom + 8),
        ],
      ),
    );
  }
}
