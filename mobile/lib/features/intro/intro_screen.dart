import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../auth/auth_screens.dart' show LanguageToggle;
import '../learn/subjects.dart' show subjectColor, subjectIcon;

/// First-launch slides: what the app offers, before the sign-in choices. Skipping or finishing marks them seen on
/// this phone, and the router moves on to the welcome screen.
class IntroScreen extends ConsumerStatefulWidget {
  const IntroScreen({super.key});

  @override
  ConsumerState<IntroScreen> createState() => _IntroScreenState();
}

class _IntroScreenState extends ConsumerState<IntroScreen> {
  final _pages = PageController();
  int _page = 0;

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  void _finish() => ref.read(introProvider.notifier).finish();

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final slides = [
      (title: l.intro1Title, body: l.intro1Body, art: const _BooksArt()),
      (
        title: l.intro2Title,
        body: l.intro2Body,
        art: const _BadgeArt(
          icon: Icons.timer_outlined,
          badges: [Icons.check_circle_rounded, Icons.bar_chart_rounded, Icons.history_edu_rounded],
        ),
      ),
      (
        title: l.intro3Title,
        body: l.intro3Body,
        art: const _BadgeArt(
          icon: Icons.forum_outlined,
          badges: [Icons.help_outline_rounded, Icons.school_outlined, Icons.notifications_active_outlined],
        ),
      ),
      (
        title: l.intro4Title,
        body: l.intro4Body,
        art: const _BadgeArt(
          icon: Icons.insights_rounded,
          badges: [Icons.event_note_rounded, Icons.local_fire_department_outlined, Icons.emoji_events_outlined],
        ),
      ),
    ];
    final last = _page == slides.length - 1;
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 8, 0),
              child: Row(
                children: [
                  const LanguageToggle(),
                  const Spacer(),
                  if (!last) TextButton(onPressed: _finish, child: Text(l.skip)),
                ],
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _pages,
                itemCount: slides.length,
                onPageChanged: (i) => setState(() => _page = i),
                itemBuilder: (context, i) {
                  final s = slides[i];
                  return LayoutBuilder(
                    builder: (context, box) => SingleChildScrollView(
                      padding: const EdgeInsets.symmetric(horizontal: 28),
                      child: ConstrainedBox(
                        constraints: BoxConstraints(minHeight: box.maxHeight),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            // Same height on every slide, so the titles line up as the student swipes.
                            ExcludeSemantics(
                              child: SizedBox(height: 220, child: Center(child: s.art)),
                            ),
                            const SizedBox(height: 36),
                            Semantics(
                              header: true,
                              child: Text(
                                s.title,
                                textAlign: TextAlign.center,
                                style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
                              ),
                            ),
                            const SizedBox(height: 12),
                            Text(
                              s.body,
                              textAlign: TextAlign.center,
                              style: Theme.of(context).textTheme.bodyLarge
                                  ?.copyWith(color: scheme.onSurfaceVariant, height: 1.4),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
            Semantics(
              label: l.introProgress(_page + 1, slides.length),
              excludeSemantics: true,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  for (var i = 0; i < slides.length; i++)
                    AnimatedContainer(
                      duration: const Duration(milliseconds: 200),
                      margin: const EdgeInsets.symmetric(horizontal: 4),
                      width: i == _page ? 24 : 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: i == _page ? scheme.primary : scheme.outlineVariant,
                        borderRadius: BorderRadius.circular(4),
                      ),
                    ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(24, 24, 24, 16),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: last
                      ? _finish
                      : () => _pages.nextPage(duration: const Duration(milliseconds: 300), curve: Curves.easeOutCubic),
                  child: Text(last ? l.getStarted : l.next),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The six NCERT books as the subject grid draws them.
class _BooksArt extends StatelessWidget {
  const _BooksArt();

  @override
  Widget build(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    Widget book(String subject, String cls) {
      final color = subjectColor(context, subject);
      return Container(
        width: 72,
        height: 72,
        margin: const EdgeInsets.all(6),
        decoration: BoxDecoration(
          color: color.withValues(alpha: dark ? 0.22 : 0.12),
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: color.withValues(alpha: 0.35)),
        ),
        child: Icon(subjectIcon(subject, cls), color: color, size: 34),
      );
    }

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final cls in ['11', '12'])
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final s in ['Physics', 'Chemistry', 'Biology']) book(s, cls),
            ],
          ),
      ],
    );
  }
}

/// A large icon in a soft circle with three small badges around it.
class _BadgeArt extends StatelessWidget {
  const _BadgeArt({required this.icon, required this.badges});
  final IconData icon;
  final List<IconData> badges;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final colors = [scheme.success, scheme.accent, subjectColor(context, 'Physics')];
    const spots = [Alignment(0.95, -0.85), Alignment(-1, 0.1), Alignment(0.8, 0.95)];
    return SizedBox(
      width: 220,
      height: 220,
      child: Stack(
        children: [
          Center(
            child: Container(
              width: 168,
              height: 168,
              decoration: BoxDecoration(color: scheme.primaryContainer, shape: BoxShape.circle),
              child: Icon(icon, size: 80, color: scheme.primary),
            ),
          ),
          for (var i = 0; i < badges.length; i++)
            Align(
              alignment: spots[i],
              child: Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  color: scheme.surface,
                  shape: BoxShape.circle,
                  border: Border.all(color: scheme.outlineVariant),
                  boxShadow: [
                    BoxShadow(color: Colors.black.withValues(alpha: 0.08), blurRadius: 12, offset: const Offset(0, 4)),
                  ],
                ),
                child: Icon(badges[i], color: colors[i], size: 26),
              ),
            ),
        ],
      ),
    );
  }
}
