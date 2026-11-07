import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import 'notification_bell.dart';

final dashboardProvider = FutureProvider.autoDispose<DashboardDto>((ref) => ref.watch(apiProvider).dashboard());

/// Where a study-plan task leads.
void openPlanTask(BuildContext context, PlanTaskDto task) {
  switch (task.type) {
    case 'Learn':
      context.push('/video/${task.entityId}');
    case 'Practice':
      context.push(
        '/practice/session',
        extra: CreatePracticeRequest(mode: 'topic', topicId: task.entityId),
      );
    case 'Test':
      context.push('/test/${task.entityId}');
    default:
      context.push('/topic/${task.entityId}');
  }
}

IconData planTaskIcon(String type) => switch (type) {
  'Learn' => Icons.play_circle_outline_rounded,
  'Practice' => Icons.edit_note_rounded,
  'Test' => Icons.timer_outlined,
  _ => Icons.replay_rounded,
};

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dash = ref.watch(dashboardProvider);
    final l = context.l10n;
    return Scaffold(
      appBar: AppBar(
        title: Text(dash.hasValue ? l.greeting(dash.value!.fullName.split(' ').first) : ''),
        actions: [
          IconButton(tooltip: l.search, icon: const Icon(Icons.search_rounded), onPressed: () => context.push('/search')),
          const NotificationBell(),
        ],
      ),
      body: AsyncView(
        value: dash,
        onRetry: () => ref.invalidate(dashboardProvider),
        data: (d) => RefreshableList(
          onRefresh: () async => ref.invalidate(dashboardProvider),
          children: [
            _Hero(d: d),
            Card(
              margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: ListTile(
                leading: Icon(Icons.forum_outlined, color: Theme.of(context).colorScheme.primary),
                title: Text(l.askDoubt, style: const TextStyle(fontWeight: FontWeight.w600)),
                subtitle: Text(l.doubtsIntro),
                trailing: const Icon(Icons.chevron_right_rounded),
                onTap: () => context.push('/doubts'),
              ),
            ),
            SectionHeader(
              l.todaysPlan,
              action: TextButton(onPressed: () => context.push('/plan'), child: Text(l.viewFullPlan)),
            ),
            if (d.todayPlan.tasks.isEmpty)
              Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: Text(l.planEmpty))
            else
              ...d.todayPlan.tasks.take(4).map((t) => _PlanTile(task: t, onChanged: () => ref.invalidate(dashboardProvider))),
            if (d.continueLearning.isNotEmpty) ...[
              SectionHeader(l.continueLearning),
              SizedBox(
                height: 120,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  itemCount: d.continueLearning.length,
                  separatorBuilder: (_, _) => const SizedBox(width: 12),
                  itemBuilder: (_, i) => _ContinueCard(item: d.continueLearning[i]),
                ),
              ),
            ],
            SectionHeader(
              l.myCourses,
              action: TextButton(onPressed: () => context.go('/learn'), child: Text(l.browseCourses)),
            ),
            if (d.courses.isEmpty)
              Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: Text(l.noCoursesYet))
            else
              for (final c in d.courses)
                ListTile(
                  title: Text(c.title),
                  subtitle: Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        LinearProgressIndicator(value: c.completionPercent / 100, minHeight: 6, borderRadius: BorderRadius.circular(3)),
                        const SizedBox(height: 4),
                        Text(l.percentComplete(c.completionPercent)),
                      ],
                    ),
                  ),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () => context.push('/course/${c.courseId}'),
                ),
            if (d.weakTopics.isNotEmpty) ...[
              SectionHeader(l.weakTopics),
              for (final w in d.weakTopics)
                ListTile(
                  leading: Icon(Icons.trending_down_rounded, color: Theme.of(context).colorScheme.warning),
                  title: Text(w.topic),
                  subtitle: Text(l.accuracyOn(formatNumber(w.accuracy), w.attempted)),
                  trailing: TextButton(
                    onPressed: () => context.push(
                      '/practice/session',
                      extra: CreatePracticeRequest(mode: 'topic', topicId: w.topicId),
                    ),
                    child: Text(l.practise),
                  ),
                ),
            ],
            if (d.recentResults.isNotEmpty) ...[
              SectionHeader(l.recentResults),
              for (final r in d.recentResults)
                ListTile(
                  leading: const Icon(Icons.assignment_turned_in_outlined),
                  title: Text(r.test),
                  subtitle: Text(formatDate(r.submittedAt, context.lang)),
                  trailing: Text(l.scoreOf(formatNumber(r.score), formatNumber(r.maxScore)), style: Theme.of(context).textTheme.titleSmall),
                  onTap: () => context.push('/result/${r.attemptId}'),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Hero extends StatelessWidget {
  const _Hero({required this.d});
  final DashboardDto d;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Card(
        color: scheme.primary,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (d.examName != null && d.daysToExam != null)
                Text(
                  l.daysToExam(d.daysToExam!, d.examName!),
                  style: text.titleLarge?.copyWith(color: scheme.onPrimary, fontWeight: FontWeight.w700),
                )
              else if (d.examName != null)
                Text(
                  d.examName!,
                  style: text.titleLarge?.copyWith(color: scheme.onPrimary, fontWeight: FontWeight.w700),
                ),
              const SizedBox(height: 6),
              Row(
                children: [
                  const Icon(Icons.local_fire_department_rounded, color: Brand.accent, size: 20),
                  const SizedBox(width: 6),
                  Text(l.streakDays(d.streakDays), style: text.bodyMedium?.copyWith(color: scheme.onPrimary)),
                ],
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  _Stat(label: l.questionsAnswered, value: '${d.questionsAnsweredThisWeek}'),
                  const SizedBox(width: 24),
                  _Stat(label: l.lessonsCompleted, value: '${d.lessonsCompletedThisWeek}'),
                ],
              ),
              const SizedBox(height: 4),
              Text(l.thisWeek, style: text.labelSmall?.copyWith(color: scheme.onPrimary.withValues(alpha: 0.8))),
            ],
          ),
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final on = Theme.of(context).colorScheme.onPrimary;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          value,
          style: Theme.of(context).textTheme.headlineSmall?.copyWith(color: on, fontWeight: FontWeight.w700),
        ),
        Text(label, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: on)),
      ],
    );
  }
}

class _PlanTile extends ConsumerWidget {
  const _PlanTile({required this.task, required this.onChanged});
  final PlanTaskDto task;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final done = task.status == 'Completed';
    return ListTile(
      leading: Icon(planTaskIcon(task.type)),
      title: Text(task.title, style: done ? const TextStyle(decoration: TextDecoration.lineThrough) : null),
      subtitle: Text('${task.reason} · ${context.l10n.minutesShort(task.estimatedMinutes)}'),
      trailing: Checkbox(
        value: done,
        semanticLabel: task.title,
        onChanged: done
            ? null
            : (_) async {
                try {
                  await ref.read(apiProvider).completeTask(task.id);
                  onChanged();
                } catch (e) {
                  if (context.mounted) showSnack(context, errorText(context, e), error: true);
                }
              },
      ),
      onTap: () => openPlanTask(context, task),
    );
  }
}

class _ContinueCard extends StatelessWidget {
  const _ContinueCard({required this.item});
  final ContinueItemDto item;

  @override
  Widget build(BuildContext context) {
    final progress = item.durationSeconds == null || item.durationSeconds == 0 ? null : item.positionSeconds / item.durationSeconds!;
    return SizedBox(
      width: 220,
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => context.push(item.type == 'Video' ? '/video/${item.resourceId}' : '/topic/${item.topicId}'),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(item.type == 'Video' ? Icons.play_circle_fill_rounded : Icons.description_outlined, color: Theme.of(context).colorScheme.primary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(item.topic, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.labelMedium),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(item.title, maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleSmall),
                const Spacer(),
                if (progress != null) LinearProgressIndicator(value: progress.clamp(0, 1), borderRadius: BorderRadius.circular(3)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
