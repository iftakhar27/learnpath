import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/config.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../auth/auth_screens.dart' show LanguageToggle, ThemeModeToggle;
import '../learn/learn_screens.dart' show CatalogQuery, PriceFilter, catalogProvider;
import '../learn/subjects.dart' show SubjectGrid, libraryProvider;
import '../support/whatsapp.dart';
import 'app_drawer.dart' show MenuButton;

/// Shows [guest] while browsing without an account, [child] otherwise.
class GuestOr extends ConsumerWidget {
  const GuestOr({super.key, required this.guest, required this.child});
  final Widget guest;
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(sessionProvider) is Guest ? guest : child;
}

/// Create a free account (primary) and sign in, side by side.
class JoinButtons extends StatelessWidget {
  const JoinButtons({super.key});

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Row(
      children: [
        Expanded(
          flex: 3,
          child: FilledButton(onPressed: () => context.push('/register'), child: Text(l.createFreeAccount)),
        ),
        const SizedBox(width: 12),
        Expanded(
          flex: 2,
          child: OutlinedButton(onPressed: () => context.push('/welcome'), child: Text(l.signIn)),
        ),
      ],
    );
  }
}

/// Home while exploring: what a free account adds, the subject grid, courses to look at, and how to reach the team.
class GuestHomeScreen extends ConsumerWidget {
  const GuestHomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final courses = ref.watch(catalogProvider(const CatalogQuery('', PriceFilter.all)));
    return Scaffold(
      appBar: AppBar(
        leading: const MenuButton(),
        title: Text(AppConfig.brandName),
        actions: [
          IconButton(
            tooltip: l.search,
            icon: const Icon(Icons.search_rounded),
            onPressed: () => context.push('/search'),
          ),
        ],
      ),
      body: RefreshableList(
        onRefresh: () async {
          ref.invalidate(libraryProvider);
          ref.invalidate(catalogProvider(const CatalogQuery('', PriceFilter.all)));
        },
        children: [
          Card(
            margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            color: scheme.primaryContainer,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    l.guestTitle,
                    style: Theme.of(context).textTheme.titleLarge
                        ?.copyWith(fontWeight: FontWeight.w700, color: scheme.onPrimaryContainer),
                  ),
                  const SizedBox(height: 6),
                  Text(l.guestBody, style: TextStyle(color: scheme.onPrimaryContainer)),
                  const SizedBox(height: 12),
                  const JoinButtons(),
                ],
              ),
            ),
          ),
          const SubjectGrid(),
          SectionHeader(
            l.coursesToExplore,
            action: TextButton(onPressed: () => context.go('/learn'), child: Text(l.browseCourses)),
          ),
          ...switch (courses) {
            AsyncData(:final value) => [
              for (final c in value.take(5))
                ListTile(
                  title: Text(c.title),
                  subtitle: Text(c.isFree ? l.free : formatPaise(c.priceInPaise)),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () => context.push('/course/${c.id}'),
                ),
            ],
            AsyncError(:final error) => [
              ErrorView(
                error: error,
                onRetry: () => ref.invalidate(catalogProvider(const CatalogQuery('', PriceFilter.all))),
              ),
            ],
            _ => [
              const Padding(
                padding: EdgeInsets.all(24),
                child: Center(child: CircularProgressIndicator()),
              ),
            ],
          },
          const SizedBox(height: 8),
          const Card(margin: EdgeInsets.symmetric(horizontal: 16), child: WhatsAppTiles(share: false)),
        ],
      ),
    );
  }
}

/// Practice, Tests and Profile for guests: what they'd get with a free account, and the way in.
class GuestGateScreen extends StatelessWidget {
  const GuestGateScreen({
    super.key,
    required this.title,
    required this.icon,
    required this.body,
    this.settings = false,
  });
  final String title;
  final IconData icon;
  final String body;

  /// Profile: language, night mode and contact links are useful before signing in too.
  final bool settings;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(leading: const MenuButton(), title: Text(title)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          Center(
            child: CircleAvatar(
              radius: 36,
              backgroundColor: scheme.primaryContainer,
              child: Icon(icon, size: 36, color: scheme.primary),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            l.guestLockedTitle,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Text(body, textAlign: TextAlign.center),
          const SizedBox(height: 20),
          const JoinButtons(),
          if (settings) ...[
            const SizedBox(height: 24),
            const Divider(),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.translate_rounded),
              title: Text(l.language),
              trailing: const LanguageToggle(),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.dark_mode_outlined),
              title: Text(l.appearance),
              trailing: const ThemeModeToggle(),
            ),
            const WhatsAppTiles(padding: EdgeInsets.zero),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.privacy_tip_outlined),
              title: Text(l.privacyPolicy),
              trailing: const Icon(Icons.open_in_new_rounded, size: 18),
              onTap: () => launchUrl(Uri.parse(AppConfig.privacyPolicyUrl), mode: LaunchMode.externalApplication),
            ),
          ],
        ],
      ),
    );
  }
}
