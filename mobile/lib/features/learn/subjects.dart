import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';

final libraryProvider = FutureProvider.autoDispose<LibraryDto>((ref) => ref.watch(apiProvider).library());
final bookProvider = FutureProvider.autoDispose.family<LibrarySubjectDto, (String, String)>(
  (ref, k) => ref.watch(apiProvider).libraryBook(k.$1, k.$2),
);
final ncertChapterProvider = FutureProvider.autoDispose.family<LibraryChapterDetailDto, String>(
  (ref, id) => ref.watch(apiProvider).libraryChapter(id),
);

/// Colour per subject, lighter in night mode so small text on it stays readable (the website uses the same hues).
Color subjectColor(BuildContext context, String subject) {
  final dark = Theme.of(context).brightness == Brightness.dark;
  return switch (subject) {
    'Chemistry' => dark ? const Color(0xFFFB923C) : const Color(0xFFEA580C),
    'Biology' => dark ? const Color(0xFF34D399) : const Color(0xFF059669),
    _ => dark ? const Color(0xFF60A5FA) : const Color(0xFF2563EB),
  };
}

/// One icon per book: motion, current, flask, benzene ring, leaf, microscope.
IconData subjectIcon(String subject, String classLevel) => switch ((subject, classLevel)) {
  ('Physics', '11') => Icons.rocket_launch_outlined,
  ('Physics', _) => Icons.bolt_rounded,
  ('Chemistry', '11') => Icons.science_outlined,
  ('Chemistry', _) => Icons.hexagon_outlined,
  ('Biology', '11') => Icons.eco_outlined,
  _ => Icons.biotech_outlined,
};

String roman(String classLevel) => classLevel == '11' ? 'XI' : 'XII';

/// The icon inside a ring that fills with the student's progress.
class ProgressRing extends StatelessWidget {
  const ProgressRing({
    super.key,
    required this.subject,
    required this.classLevel,
    required this.percent,
    this.size = 60,
  });
  final String subject;
  final String classLevel;
  final int percent;
  final double size;

  @override
  Widget build(BuildContext context) {
    final color = subjectColor(context, subject);
    return SizedBox.square(
      dimension: size,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Container(
            decoration: BoxDecoration(color: color.withValues(alpha: 0.12), shape: BoxShape.circle),
          ),
          SizedBox.square(
            dimension: size,
            child: CircularProgressIndicator(
              value: percent / 100,
              strokeWidth: 3.5,
              color: color,
              backgroundColor: Theme.of(context).colorScheme.outlineVariant,
              strokeCap: StrokeCap.round,
            ),
          ),
          Icon(subjectIcon(subject, classLevel), color: color, size: size * 0.44),
        ],
      ),
    );
  }
}

/// Physics, Chemistry and Biology for Class 11 and 12, with progress and a "continue" shortcut.
class SubjectGrid extends ConsumerWidget {
  const SubjectGrid({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final library = ref.watch(libraryProvider);
    final scheme = Theme.of(context).colorScheme;
    return library.when(
      loading: () => const SizedBox(height: 240, child: Center(child: CircularProgressIndicator())),
      error: (e, _) => ErrorView(error: e, onRetry: () => ref.invalidate(libraryProvider)),
      data: (lib) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(l.subjectsTitle, style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
            Text(l.subjectsSubtitle, style: Theme.of(context).textTheme.bodySmall),
            if (lib.resume case final resume?) ...[
              const SizedBox(height: 8),
              ActionChip(
                avatar: Icon(Icons.play_arrow_rounded, color: subjectColor(context, resume.subject)),
                label: Text(l.continueChapter(resume.title), overflow: TextOverflow.ellipsis),
                onPressed: () => context.push('/ncert/${resume.chapterId}'),
              ),
            ],
            const SizedBox(height: 8),
            Card(
              margin: EdgeInsets.zero,
              clipBehavior: Clip.antiAlias,
              // Rows sized to their tallest tile (long titles, large font sizes), each tile stretched to the full row.
              child: Column(
                children: [
                  for (var row = 0; row < lib.tiles.length; row += 3)
                    IntrinsicHeight(
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          for (final (i, t) in lib.tiles.skip(row).take(3).indexed)
                            Expanded(
                              child: DecoratedBox(
                                decoration: BoxDecoration(
                                  border: Border(
                                    left: i == 0
                                        ? BorderSide.none
                                        : BorderSide(color: scheme.outlineVariant, width: 0.5),
                                    top: row == 0
                                        ? BorderSide.none
                                        : BorderSide(color: scheme.outlineVariant, width: 0.5),
                                  ),
                                ),
                                child: _SubjectTile(tile: t),
                              ),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SubjectTile extends StatelessWidget {
  const _SubjectTile({required this.tile});
  final LibraryTileDto tile;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final t = tile;
    return InkWell(
      onTap: () => context.push('/subject/${t.classLevel}/${t.subject.toLowerCase()}'),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 14),
        child: Semantics(
          label: '${t.title}, ${l.classLevel(t.classLevel)}: ${l.chaptersReady(t.readyChapters, t.chapters)}',
          excludeSemantics: true,
          button: true,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ProgressRing(subject: t.subject, classLevel: t.classLevel, percent: t.progressPercent),
              const SizedBox(height: 8),
              Text.rich(
                TextSpan(
                  text: '${t.title} ',
                  children: [
                    TextSpan(
                      text: roman(t.classLevel),
                      style: TextStyle(color: scheme.onSurfaceVariant),
                    ),
                  ],
                ),
                textAlign: TextAlign.center,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 2),
              Text(
                l.chaptersReady(t.readyChapters, t.chapters),
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.labelSmall,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A book's NCERT chapters, with switches to the other books.
class SubjectScreen extends ConsumerWidget {
  const SubjectScreen({super.key, required this.classLevel, required this.subject});
  final String classLevel;
  final String subject;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final book = ref.watch(bookProvider((classLevel, subject)));
    final title = book.value == null ? '' : '${book.value!.title} · ${l.classLevel(classLevel)}';
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: AsyncView(
        value: book,
        onRetry: () => ref.invalidate(bookProvider((classLevel, subject))),
        data: (b) {
          final color = subjectColor(context, b.subject);
          final ready = b.chapters.where((c) => c.ready).length;
          return RefreshableList(
            onRefresh: () async => ref.invalidate(bookProvider((classLevel, subject))),
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              Row(
                children: [
                  ProgressRing(subject: b.subject, classLevel: b.classLevel, percent: b.progressPercent, size: 64),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Text(
                      'NCERT · ${l.chaptersReady(ready, b.chapters.length)} · ${l.percentComplete(b.progressPercent)}',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final c in ['11', '12'])
                    ChoiceChip(
                      label: Text(l.classLevel(c)),
                      selected: c == b.classLevel,
                      onSelected: (_) => context.pushReplacement('/subject/$c/${b.subject.toLowerCase()}'),
                    ),
                  for (final s in ['Physics', 'Chemistry', 'Biology'])
                    ChoiceChip(
                      label: Text(switch (s) {
                        'Physics' => l.physicsShort,
                        'Chemistry' => l.chemistryShort,
                        _ => l.biologyShort,
                      }),
                      selected: s == b.subject,
                      onSelected: (_) => context.pushReplacement('/subject/${b.classLevel}/${s.toLowerCase()}'),
                    ),
                ],
              ),
              const SizedBox(height: 12),
              for (final c in b.chapters)
                Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    leading: Container(
                      width: 40,
                      height: 40,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        c.number.toString().padLeft(2, '0'),
                        style: TextStyle(color: color, fontWeight: FontWeight.w700),
                      ),
                    ),
                    title: Text(c.title),
                    subtitle: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          c.ready
                              ? [
                                  l.topicsCount(c.topics),
                                  if (c.videos > 0) l.videosCount(c.videos),
                                  if (c.notes > 0) l.notesCount(c.notes),
                                ].join(' · ')
                              : l.chapterComingSoon,
                        ),
                        if (c.progressPercent > 0) ...[
                          const SizedBox(height: 6),
                          LinearProgressIndicator(value: c.progressPercent / 100, color: color),
                        ],
                      ],
                    ),
                    trailing: c.locked
                        ? Icon(Icons.lock_outline_rounded, semanticLabel: l.locked)
                        : const Icon(Icons.chevron_right_rounded),
                    onTap: () => context.push('/ncert/${c.id}'),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

/// A chapter's lessons from every course that teaches it: Videos, Notes and Practice.
class NcertChapterScreen extends ConsumerWidget {
  const NcertChapterScreen({super.key, required this.chapterId});
  final String chapterId;

  Future<void> _open(BuildContext context, WidgetRef ref, ResourceDto r) async {
    final l = context.l10n;
    if (r.locked) {
      showSnack(context, l.unlockToContinue);
      return;
    }
    if (r.type == 'Video') {
      await context.push('/video/${r.id}');
    } else {
      try {
        final playback = await ref.read(apiProvider).playback(r.id);
        await launchUrl(Uri.parse(playback.url), mode: LaunchMode.externalApplication);
        await ref.read(apiProvider).saveProgress(r.id, 0, markCompleted: true);
      } catch (e) {
        if (context.mounted) showSnack(context, errorText(context, e), error: true);
      }
    }
    ref.invalidate(ncertChapterProvider(chapterId));
    ref.invalidate(libraryProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final chapter = ref.watch(ncertChapterProvider(chapterId));
    return DefaultTabController(
      length: 3,
      child: Scaffold(
        appBar: AppBar(
          title: Text(chapter.value == null ? '' : l.chapterNumber(chapter.value!.number)),
          bottom: chapter.value?.topics.isNotEmpty == true
              ? TabBar(
                  tabs: [
                    Tab(text: l.videos),
                    Tab(text: l.notes),
                    Tab(text: l.tabPractice),
                  ],
                )
              : null,
        ),
        body: AsyncView(
          value: chapter,
          onRetry: () => ref.invalidate(ncertChapterProvider(chapterId)),
          data: (ch) {
            final color = subjectColor(context, ch.subject);
            final header = Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${ch.subjectTitle} · ${l.classLevel(ch.classLevel)}',
                    style: TextStyle(color: color, fontWeight: FontWeight.w600),
                  ),
                  Text(
                    ch.title,
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  if (ch.flashcardCount > 0) ...[
                    const SizedBox(height: 8),
                    Card(
                      margin: EdgeInsets.zero,
                      child: ListTile(
                        leading: Icon(Icons.style_outlined, color: color),
                        title: Text(l.flashcardsTitle, style: const TextStyle(fontWeight: FontWeight.w600)),
                        subtitle: Text(l.flashcardsForChapter(ch.flashcardCount)),
                        trailing: const Icon(Icons.chevron_right_rounded),
                        onTap: () => context.push('/flashcards/${ch.id}'),
                      ),
                    ),
                  ],
                  if (ch.buyCourseId case final courseId?) ...[
                    const SizedBox(height: 8),
                    Card(
                      color: Theme.of(context).colorScheme.warningContainer,
                      margin: EdgeInsets.zero,
                      child: ListTile(
                        leading: const Icon(Icons.lock_outline_rounded),
                        title: Text(l.chapterLocked),
                        trailing: TextButton(
                          onPressed: () => context.push('/course/$courseId'),
                          child: Text(l.viewCourse),
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            );
            Widget lessons(String type, String empty) {
              final groups = [for (final t in ch.topics) (t, t.resources.where((r) => r.type == type).toList())]
                  .where((g) => g.$2.isNotEmpty)
                  .toList();
              return ListView(
                padding: const EdgeInsets.only(bottom: 24),
                children: [
                  header,
                  if (groups.isEmpty)
                    Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(empty, textAlign: TextAlign.center),
                    ),
                  for (final (topic, items) in groups) ...[
                    ListTile(
                      dense: true,
                      title: Text(topic.title, style: const TextStyle(fontWeight: FontWeight.w700)),
                      subtitle: Text(l.fromCourse(topic.course)),
                      onTap: () => context.push('/topic/${topic.id}'),
                    ),
                    for (final r in items)
                      ListTile(
                        leading: Icon(
                          r.locked
                              ? Icons.lock_outline_rounded
                              : (r.type == 'Video' ? Icons.play_circle_outline_rounded : Icons.description_outlined),
                          color: r.locked ? null : color,
                        ),
                        title: Text(r.title),
                        subtitle: Wrap(
                          spacing: 8,
                          children: [
                            if (r.durationSeconds != null) Text(formatClock(r.durationSeconds!)),
                            if (r.isFreePreview && r.locked == false && !topic.hasAccess) Pill(l.freePreview),
                            if (r.completed)
                              Pill(
                                l.completed,
                                color: Theme.of(context).colorScheme.success,
                                background: Theme.of(context).colorScheme.successContainer,
                              ),
                          ],
                        ),
                        onTap: () => _open(context, ref, r),
                      ),
                  ],
                ],
              );
            }

            final nav = Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: Row(
                children: [
                  if (ch.previousChapterId != null)
                    TextButton.icon(
                      onPressed: () => context.pushReplacement('/ncert/${ch.previousChapterId}'),
                      icon: const Icon(Icons.chevron_left_rounded),
                      label: Text(l.previous),
                    ),
                  const Spacer(),
                  if (ch.nextChapterId != null)
                    TextButton.icon(
                      onPressed: () => context.pushReplacement('/ncert/${ch.nextChapterId}'),
                      icon: const Icon(Icons.chevron_right_rounded),
                      label: Text(l.next),
                    ),
                ],
              ),
            );
            if (ch.topics.isEmpty) {
              return ListView(
                children: [
                  header,
                  Padding(
                    padding: const EdgeInsets.all(24),
                    child: Text(l.chapterComingSoon, textAlign: TextAlign.center),
                  ),
                  nav,
                ],
              );
            }
            return Column(
              children: [
                Expanded(
                  child: TabBarView(
                    children: [
                      lessons('Video', l.noVideosYet),
                      lessons('Document', l.noNotesYet),
                      ListView(
                        children: [
                          header,
                          Padding(
                            padding: const EdgeInsets.all(16),
                            child: ch.canPractise && ch.practiceQuestionCount > 0
                                ? FilledButton.icon(
                                    icon: const Icon(Icons.edit_note_rounded),
                                    label: Text(l.practiseChapter(ch.practiceQuestionCount)),
                                    onPressed: () => context.push(
                                      '/practice/session',
                                      extra: CreatePracticeRequest(mode: 'ncert', ncertChapterId: ch.id),
                                    ),
                                  )
                                : Text(ch.canPractise ? l.noPracticeQuestions : l.chapterLocked),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                nav,
              ],
            );
          },
        ),
      ),
    );
  }
}
