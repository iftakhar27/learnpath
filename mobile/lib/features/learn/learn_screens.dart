import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../payments/checkout.dart';

enum PriceFilter { all, free, paid }

class CatalogQuery {
  const CatalogQuery(this.q, this.filter);
  final String q;
  final PriceFilter filter;
  @override
  bool operator ==(Object other) => other is CatalogQuery && other.q == q && other.filter == filter;
  @override
  int get hashCode => Object.hash(q, filter);
}

final catalogProvider = FutureProvider.autoDispose.family<List<CourseSummaryDto>, CatalogQuery>((ref, query) async {
  final page = await ref
      .watch(apiProvider)
      .courses(
        q: query.q.isEmpty ? null : query.q,
        free: switch (query.filter) {
          PriceFilter.free => true,
          PriceFilter.paid => false,
          PriceFilter.all => null,
        },
      );
  return page.items;
});

final courseProvider = FutureProvider.autoDispose.family<CourseDetailDto, String>((ref, id) => ref.watch(apiProvider).syllabus(id));
final topicProvider = FutureProvider.autoDispose.family<TopicDetailDto, String>((ref, id) => ref.watch(apiProvider).topic(id));

class CatalogScreen extends ConsumerStatefulWidget {
  const CatalogScreen({super.key});

  @override
  ConsumerState<CatalogScreen> createState() => _CatalogScreenState();
}

class _CatalogScreenState extends ConsumerState<CatalogScreen> {
  String _q = '';
  PriceFilter _filter = PriceFilter.all;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final query = CatalogQuery(_q, _filter);
    final courses = ref.watch(catalogProvider(query));
    return Scaffold(
      appBar: AppBar(title: Text(l.tabLearn)),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: TextField(
              decoration: InputDecoration(hintText: l.searchHint, prefixIcon: const Icon(Icons.search_rounded)),
              textInputAction: TextInputAction.search,
              onSubmitted: (v) => setState(() => _q = v.trim()),
            ),
          ),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Row(
              children: [
                for (final f in PriceFilter.values)
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(switch (f) {
                        PriceFilter.all => l.allCourses,
                        PriceFilter.free => l.free,
                        PriceFilter.paid => l.paid,
                      }),
                      selected: _filter == f,
                      onSelected: (_) => setState(() => _filter = f),
                    ),
                  ),
              ],
            ),
          ),
          Expanded(
            child: AsyncView(
              value: courses,
              onRetry: () => ref.invalidate(catalogProvider(query)),
              data: (list) => list.isEmpty
                  ? EmptyState(icon: Icons.menu_book_outlined, message: l.coursesEmpty)
                  : RefreshableList(
                      onRefresh: () async => ref.invalidate(catalogProvider(query)),
                      padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                      children: [
                        for (final c in list)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: CourseCard(course: c),
                          ),
                      ],
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

class CourseCard extends StatelessWidget {
  const CourseCard({super.key, required this.course});
  final CourseSummaryDto course;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/course/${course.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(course.title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600)),
              if (course.description != null) ...[
                const SizedBox(height: 4),
                Text(course.description!, maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodyMedium),
              ],
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  if (course.enrolled) Pill(l.enrolled, color: scheme.success, background: scheme.successContainer),
                  if (course.isFree) Pill(l.free, color: scheme.primary, background: scheme.primaryContainer) else Pill(formatPaise(course.priceInPaise)),
                  if (!course.isFree) Pill(course.accessDays == null ? l.lifetimeAccess : l.accessDays(course.accessDays!)),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class CourseScreen extends ConsumerWidget {
  const CourseScreen({super.key, required this.courseId});
  final String courseId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final detail = ref.watch(courseProvider(courseId));
    return Scaffold(
      appBar: AppBar(title: Text(detail.value?.course.title ?? '')),
      body: AsyncView(
        value: detail,
        onRetry: () => ref.invalidate(courseProvider(courseId)),
        data: (d) => RefreshableList(
          onRefresh: () async => ref.invalidate(courseProvider(courseId)),
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(d.course.title, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
                  if (d.course.description != null) ...[const SizedBox(height: 8), Text(d.course.description!)],
                  const SizedBox(height: 16),
                  if (d.hasAccess) ...[
                    Row(
                      children: [
                        Icon(Icons.verified_rounded, color: Theme.of(context).colorScheme.success),
                        const SizedBox(width: 8),
                        Expanded(child: Text(l.youHaveAccess)),
                      ],
                    ),
                    const SizedBox(height: 12),
                    LinearProgressIndicator(value: d.completionPercent / 100, minHeight: 8, borderRadius: BorderRadius.circular(4)),
                    const SizedBox(height: 4),
                    Text(l.percentComplete(d.completionPercent)),
                  ] else if (d.course.isFree)
                    AsyncButton(
                      expand: true,
                      onPressed: () async {
                        await ref.read(apiProvider).enroll(courseId);
                        ref.invalidate(courseProvider(courseId));
                        if (context.mounted) showSnack(context, l.enrolledToast);
                      },
                      child: Text(l.enrollFree),
                    )
                  else ...[
                    BuyButton(course: d.course, onPurchased: () => ref.invalidate(courseProvider(courseId))),
                    const SizedBox(height: 6),
                    Text(d.course.accessDays == null ? l.lifetimeAccess : l.accessDays(d.course.accessDays!), style: Theme.of(context).textTheme.bodySmall),
                  ],
                ],
              ),
            ),
            SectionHeader(l.syllabus),
            for (final s in d.subjects)
              for (final ch in s.chapters)
                ExpansionTile(
                  title: Text(ch.title),
                  subtitle: Text('${s.title} · ${l.topicsCount(ch.topics.length)} · ${l.percentComplete(ch.completionPercent)}'),
                  initiallyExpanded: d.subjects.length == 1 && s.chapters.length == 1,
                  children: [
                    for (final t in ch.topics)
                      ListTile(
                        contentPadding: const EdgeInsets.only(left: 32, right: 16),
                        leading: Icon(
                          t.completed ? Icons.check_circle_rounded : Icons.radio_button_unchecked_rounded,
                          color: t.completed ? Theme.of(context).colorScheme.success : Theme.of(context).colorScheme.outline,
                          semanticLabel: t.completed ? l.completed : null,
                        ),
                        title: Text(t.title),
                        subtitle: t.estimatedMinutes > 0 ? Text(l.minutesShort(t.estimatedMinutes)) : null,
                        trailing: const Icon(Icons.chevron_right_rounded),
                        onTap: () => context.push('/topic/${t.id}'),
                      ),
                  ],
                ),
          ],
        ),
      ),
    );
  }
}

class TopicScreen extends ConsumerWidget {
  const TopicScreen({super.key, required this.topicId});
  final String topicId;

  Future<void> _openResource(BuildContext context, WidgetRef ref, TopicDetailDto topic, ResourceDto r) async {
    final l = context.l10n;
    if (r.locked) {
      showSnack(context, l.unlockToContinue);
      return;
    }
    if (r.type == 'Video') {
      await context.push('/video/${r.id}');
      ref.invalidate(topicProvider(topicId));
      return;
    }
    try {
      final playback = await ref.read(apiProvider).playback(r.id);
      await launchUrl(Uri.parse(playback.url), mode: LaunchMode.externalApplication);
      await ref.read(apiProvider).saveProgress(r.id, 0, markCompleted: true);
      ref.invalidate(topicProvider(topicId));
    } catch (e) {
      if (context.mounted) showSnack(context, errorText(context, e), error: true);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final topic = ref.watch(topicProvider(topicId));
    return Scaffold(
      appBar: AppBar(title: Text(topic.value?.chapter ?? '')),
      body: AsyncView(
        value: topic,
        onRetry: () => ref.invalidate(topicProvider(topicId)),
        data: (t) => ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(t.subject, style: Theme.of(context).textTheme.labelLarge?.copyWith(color: Theme.of(context).colorScheme.primary)),
                  const SizedBox(height: 4),
                  Text(t.title, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
                  if (t.estimatedMinutes > 0) Text(l.minutesShort(t.estimatedMinutes)),
                  if (t.objectives != null) ...[
                    const SizedBox(height: 16),
                    Text(l.objectives, style: Theme.of(context).textTheme.titleSmall),
                    const SizedBox(height: 4),
                    Text(t.objectives!),
                  ],
                ],
              ),
            ),
            for (final r in t.resources)
              ListTile(
                leading: Icon(r.locked ? Icons.lock_outline_rounded : (r.type == 'Video' ? Icons.play_circle_outline_rounded : Icons.description_outlined)),
                title: Text(r.title),
                subtitle: Wrap(
                  spacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(r.type == 'Video' ? l.video : l.notes),
                    if (r.durationSeconds != null) Text(formatClock(r.durationSeconds!)),
                    if (r.isFreePreview && !t.hasAccess) Pill(l.freePreview),
                    if (r.completed)
                      Pill(l.completed, color: Theme.of(context).colorScheme.success, background: Theme.of(context).colorScheme.successContainer),
                  ],
                ),
                trailing: r.locked ? Text(l.locked) : const Icon(Icons.chevron_right_rounded),
                onTap: () => _openResource(context, ref, t, r),
              ),
            if (!t.hasAccess)
              Padding(
                padding: const EdgeInsets.all(16),
                child: OutlinedButton(onPressed: () => context.push('/course/${t.courseId}'), child: Text(l.viewCourse)),
              ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: t.practiceQuestionCount > 0
                  ? FilledButton.icon(
                      icon: const Icon(Icons.edit_note_rounded),
                      label: Text(l.practiseTopic(t.practiceQuestionCount)),
                      onPressed: t.hasAccess
                          ? () => context.push(
                              '/practice/session',
                              extra: CreatePracticeRequest(mode: 'topic', topicId: t.id),
                            )
                          : null,
                    )
                  : Text(l.noPracticeQuestions, textAlign: TextAlign.center),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: Row(
                children: [
                  if (t.previousTopicId != null)
                    TextButton.icon(
                      onPressed: () => context.pushReplacement('/topic/${t.previousTopicId}'),
                      icon: const Icon(Icons.chevron_left_rounded),
                      label: Text(l.previousTopic),
                    ),
                  const Spacer(),
                  if (t.nextTopicId != null)
                    TextButton.icon(
                      onPressed: () => context.pushReplacement('/topic/${t.nextTopicId}'),
                      icon: const Icon(Icons.chevron_right_rounded),
                      label: Text(l.nextTopic),
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
