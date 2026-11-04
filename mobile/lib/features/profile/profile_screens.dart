import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/config.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../auth/auth_screens.dart';
import '../home/home_screen.dart';
import '../onboarding/onboarding_screen.dart';
import '../practice/practice_screens.dart';

final studyPlanProvider = FutureProvider.autoDispose<StudyPlanDto>((ref) => ref.watch(apiProvider).studyPlan());
final planHistoryProvider = FutureProvider.autoDispose<List<PlanDayDto>>((ref) => ref.watch(apiProvider).planHistory());
final notificationsProvider = FutureProvider.autoDispose<List<NotificationDto>>((ref) async => (await ref.watch(apiProvider).notifications()).items);
final bookmarksProvider = FutureProvider.autoDispose<List<BookmarkDto>>((ref) => ref.watch(apiProvider).bookmarks());
final ordersProvider = FutureProvider.autoDispose<List<OrderDto>>((ref) => ref.watch(apiProvider).orders());
final ticketsProvider = FutureProvider.autoDispose<List<TicketDto>>((ref) => ref.watch(apiProvider).tickets());

// ---------------- Study plan ----------------

class StudyPlanScreen extends ConsumerWidget {
  const StudyPlanScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final plan = ref.watch(studyPlanProvider);
    final history = ref.watch(planHistoryProvider);
    void refresh() {
      ref.invalidate(studyPlanProvider);
      ref.invalidate(planHistoryProvider);
      ref.invalidate(dashboardProvider);
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(l.planTitle),
        actions: [
          IconButton(
            tooltip: l.regeneratePlan,
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () async {
              try {
                await ref.read(apiProvider).regeneratePlan();
                refresh();
              } catch (e) {
                if (context.mounted) showSnack(context, errorText(context, e), error: true);
              }
            },
          ),
        ],
      ),
      body: AsyncView(
        value: plan,
        onRetry: refresh,
        data: (p) => RefreshableList(
          onRefresh: () async => refresh(),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(l.plannedMinutes(p.plannedMinutes, p.dailyStudyMinutes), style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 4),
                  Text(p.basis, style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
            const SizedBox(height: 8),
            if (p.tasks.isEmpty) Padding(padding: const EdgeInsets.all(16), child: Text(l.planEmpty)),
            for (final t in p.tasks)
              ListTile(
                leading: Icon(planTaskIcon(t.type)),
                title: Text(t.title, style: t.status == 'Pending' ? null : const TextStyle(decoration: TextDecoration.lineThrough)),
                subtitle: Text('${t.reason} · ${l.minutesShort(t.estimatedMinutes)}'),
                onTap: () => openPlanTask(context, t),
                trailing: t.status == 'Pending'
                    ? Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          TextButton(onPressed: () => ref.read(apiProvider).skipTask(t.id).then((_) => refresh()), child: Text(l.skip)),
                          IconButton(
                            tooltip: l.done,
                            icon: const Icon(Icons.check_circle_outline_rounded),
                            onPressed: () => ref.read(apiProvider).completeTask(t.id).then((_) => refresh()),
                          ),
                        ],
                      )
                    : Pill(t.status == 'Completed' ? l.completed : l.skip),
              ),
            if (history.value?.isNotEmpty ?? false) ...[
              SectionHeader(l.lastSevenDays),
              for (final d in history.value!)
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                  child: Row(
                    children: [
                      SizedBox(width: 110, child: Text(formatDate(DateTime.parse(d.date), context.lang))),
                      Expanded(
                        child: LinearProgressIndicator(
                          value: d.planned == 0 ? 0 : d.completed / d.planned,
                          minHeight: 8,
                          borderRadius: BorderRadius.circular(4),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(l.tasksDone(d.completed, d.planned)),
                    ],
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

// ---------------- Profile ----------------

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final p = ref.watch(profileProvider);
    final exams = ref.watch(examsProvider);
    final examName = exams.value?.where((e) => e.id == p.targetExamId).firstOrNull?.name;
    Widget link(IconData icon, String title, String route) =>
        ListTile(leading: Icon(icon), title: Text(title), trailing: const Icon(Icons.chevron_right_rounded), onTap: () => context.push(route));

    return Scaffold(
      appBar: AppBar(title: Text(l.profileTitle)),
      body: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          ListTile(
            leading: CircleAvatar(radius: 24, child: Text(p.fullName.isEmpty ? '?' : p.fullName.characters.first.toUpperCase())),
            title: Text(p.fullName, style: Theme.of(context).textTheme.titleMedium),
            subtitle: Text([?p.email, ?p.phone].join(' · ')),
          ),
          if (examName != null) Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: Text(l.preparingFor(examName, '${p.targetYear ?? ''}'))),
          const SizedBox(height: 8),
          ListTile(leading: const Icon(Icons.translate_rounded), title: Text(l.language), trailing: const LanguageToggle()),
          link(Icons.tune_rounded, l.editPreferences, '/preferences'),
          link(Icons.insights_rounded, l.myProgress, '/progress'),
          link(Icons.event_note_rounded, l.planTitle, '/plan'),
          link(Icons.bookmark_outline_rounded, l.bookmarksTitle, '/bookmarks'),
          link(Icons.notifications_none_rounded, l.notificationsTitle, '/notifications'),
          link(Icons.receipt_long_outlined, l.myPurchases, '/orders'),
          link(Icons.support_agent_rounded, l.helpSupport, '/support'),
          ListTile(
            leading: const Icon(Icons.privacy_tip_outlined),
            title: Text(l.privacyPolicy),
            trailing: const Icon(Icons.open_in_new_rounded, size: 18),
            onTap: () => launchUrl(Uri.parse(AppConfig.privacyPolicyUrl), mode: LaunchMode.externalApplication),
          ),
          const Divider(),
          ListTile(leading: const Icon(Icons.logout_rounded), title: Text(l.signOut), onTap: () => ref.read(sessionProvider.notifier).logout()),
          ListTile(
            leading: Icon(Icons.delete_outline_rounded, color: Theme.of(context).colorScheme.danger),
            title: Text(l.deleteAccount, style: TextStyle(color: Theme.of(context).colorScheme.danger)),
            onTap: () async {
              final ok = await showDialog<bool>(
                context: context,
                builder: (ctx) => AlertDialog(
                  title: Text(l.deleteAccount),
                  content: Text(l.deleteAccountBody),
                  actions: [
                    TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(l.cancel)),
                    FilledButton(
                      style: FilledButton.styleFrom(backgroundColor: Theme.of(ctx).colorScheme.danger),
                      onPressed: () => Navigator.pop(ctx, true),
                      child: Text(l.delete),
                    ),
                  ],
                ),
              );
              if (ok != true) return;
              try {
                await ref.read(apiProvider).requestAccountDeletion();
                await ref.read(sessionProvider.notifier).logout();
              } catch (e) {
                if (context.mounted) showSnack(context, errorText(context, e), error: true);
              }
            },
          ),
        ],
      ),
    );
  }
}

class PreferencesScreen extends StatelessWidget {
  const PreferencesScreen({super.key});
  @override
  Widget build(BuildContext context) => const OnboardingScreen(editing: true);
}

// ---------------- Progress ----------------

class ProgressScreen extends ConsumerWidget {
  const ProgressScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final analytics = ref.watch(analyticsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.progressTitle)),
      body: AsyncView(
        value: analytics,
        onRetry: () => ref.invalidate(analyticsProvider),
        data: (a) => RefreshableList(
          onRefresh: () async => ref.invalidate(analyticsProvider),
          padding: const EdgeInsets.all(16),
          children: [
            GridView.count(
              crossAxisCount: 2,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 1.7,
              children: [
                _StatCard(label: l.testsTaken, value: '${a.testsTaken}'),
                _StatCard(label: l.averageScore, value: a.averagePercentage == null ? '—' : '${formatNumber(a.averagePercentage!)}%'),
                _StatCard(label: l.practiceAccuracy, value: a.practiceAccuracy == null ? '—' : '${formatNumber(a.practiceAccuracy!)}%'),
                _StatCard(label: l.lessonsCompleted, value: '${a.lessonsCompleted}'),
              ],
            ),
            SectionHeader(l.scoreTrend),
            if (a.scoreTrend.isEmpty)
              Text(l.noTestsYet)
            else
              SizedBox(
                height: 140,
                child: Semantics(
                  label: a.scoreTrend.map((p) => '${p.test}: ${formatNumber(p.percentage)}%').join(', '),
                  excludeSemantics: true,
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      for (final pt in a.scoreTrend)
                        Expanded(
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 3),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.end,
                              children: [
                                Text('${pt.percentage.round()}', style: Theme.of(context).textTheme.labelSmall),
                                const SizedBox(height: 2),
                                Container(
                                  height: 100 * (pt.percentage.clamp(0, 100) / 100) + 2,
                                  decoration: BoxDecoration(color: Theme.of(context).colorScheme.primary, borderRadius: BorderRadius.circular(4)),
                                ),
                              ],
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            if (a.subjects.isNotEmpty) ...[
              SectionHeader(l.subjectWise),
              for (final s in a.subjects)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 6),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(child: Text(s.subject)),
                          Text(l.accuracyOn(formatNumber(s.accuracy), s.attempted)),
                        ],
                      ),
                      const SizedBox(height: 4),
                      LinearProgressIndicator(value: s.accuracy / 100, minHeight: 8, borderRadius: BorderRadius.circular(4)),
                    ],
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(value, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
          Text(label, style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    ),
  );
}

// ---------------- Notifications, bookmarks, orders, support ----------------

class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final items = ref.watch(notificationsProvider);
    return Scaffold(
      appBar: AppBar(
        title: Text(l.notificationsTitle),
        actions: [
          TextButton(
            onPressed: () async {
              await ref.read(apiProvider).markAllNotificationsRead();
              ref.invalidate(notificationsProvider);
              ref.invalidate(dashboardProvider);
            },
            child: Text(l.markAllRead),
          ),
        ],
      ),
      body: AsyncView(
        value: items,
        onRetry: () => ref.invalidate(notificationsProvider),
        data: (list) => list.isEmpty
            ? EmptyState(icon: Icons.notifications_none_rounded, message: l.notificationsEmpty)
            : RefreshableList(
                onRefresh: () async => ref.invalidate(notificationsProvider),
                children: [
                  for (final n in list)
                    ListTile(
                      leading: Icon(switch (n.type) {
                        'payment' => Icons.receipt_long_outlined,
                        'support' => Icons.support_agent_rounded,
                        _ => Icons.campaign_outlined,
                      }),
                      title: Text(n.title, style: n.read ? null : const TextStyle(fontWeight: FontWeight.w700)),
                      subtitle: Text('${n.body}\n${formatDateTime(n.createdAt, context.lang)}'),
                      isThreeLine: true,
                      onTap: () async {
                        if (!n.read) {
                          await ref.read(apiProvider).markNotificationRead(n.id);
                          ref.invalidate(notificationsProvider);
                          ref.invalidate(dashboardProvider);
                        }
                        if (n.link == '/support' && context.mounted) context.push('/support');
                      },
                    ),
                ],
              ),
      ),
    );
  }
}

class BookmarksScreen extends ConsumerWidget {
  const BookmarksScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final items = ref.watch(bookmarksProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.bookmarksTitle)),
      body: AsyncView(
        value: items,
        onRetry: () => ref.invalidate(bookmarksProvider),
        data: (list) => list.isEmpty
            ? EmptyState(icon: Icons.bookmark_outline_rounded, message: l.bookmarksEmpty)
            : ListView(
                children: [
                  if (list.any((b) => b.entityType == 'Question'))
                    Padding(
                      padding: const EdgeInsets.all(16),
                      child: FilledButton.icon(
                        icon: const Icon(Icons.play_arrow_rounded),
                        label: Text(l.practiceBookmarked),
                        onPressed: () => context.push('/practice/session', extra: CreatePracticeRequest(mode: 'bookmarked', count: 20)),
                      ),
                    ),
                  for (final b in list)
                    ListTile(
                      leading: Icon(b.entityType == 'Question' ? Icons.help_outline_rounded : Icons.play_circle_outline_rounded),
                      title: Text(b.title, maxLines: 2, overflow: TextOverflow.ellipsis),
                      subtitle: b.note == null ? null : Text(b.note!),
                      trailing: IconButton(
                        tooltip: l.remove,
                        icon: const Icon(Icons.delete_outline_rounded),
                        onPressed: () async {
                          await ref.read(apiProvider).removeBookmark(b.id);
                          ref.invalidate(bookmarksProvider);
                        },
                      ),
                      onTap: switch (b.entityType) {
                        'Resource' => () => context.push('/video/${b.entityId}'),
                        'Topic' => () => context.push('/topic/${b.entityId}'),
                        _ => null,
                      },
                    ),
                ],
              ),
      ),
    );
  }
}

class OrdersScreen extends ConsumerWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final orders = ref.watch(ordersProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.myPurchases)),
      body: AsyncView(
        value: orders,
        onRetry: () => ref.invalidate(ordersProvider),
        data: (list) => list.isEmpty
            ? EmptyState(icon: Icons.receipt_long_outlined, message: l.ordersEmpty)
            : ListView(
                children: [
                  for (final o in list)
                    ListTile(
                      title: Text(o.courseTitle),
                      subtitle: Text(formatDateTime(o.createdAt, context.lang)),
                      trailing: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(formatPaise(o.amountInPaise), style: Theme.of(context).textTheme.titleSmall),
                          Text(switch (o.status) {
                            'Paid' => l.orderPaid,
                            'Failed' => l.orderFailed,
                            'Refunded' => l.orderRefunded,
                            _ => l.orderPending,
                          }),
                        ],
                      ),
                    ),
                ],
              ),
      ),
    );
  }
}

class SupportScreen extends ConsumerWidget {
  const SupportScreen({super.key});

  Future<void> _newTicket(BuildContext context, WidgetRef ref) async {
    final l = context.l10n;
    final form = GlobalKey<FormState>();
    final subject = TextEditingController();
    final message = TextEditingController();
    var category = 'technical';
    final sent = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setState) => Padding(
          padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.viewInsetsOf(ctx).bottom + 16),
          child: Form(
            key: form,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(l.newTicket, style: Theme.of(ctx).textTheme.titleLarge),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: category,
                  decoration: InputDecoration(labelText: l.ticketCategory),
                  items: [
                    DropdownMenuItem(value: 'payment', child: Text(l.categoryPayment)),
                    DropdownMenuItem(value: 'content', child: Text(l.categoryContent)),
                    DropdownMenuItem(value: 'technical', child: Text(l.categoryTechnical)),
                    DropdownMenuItem(value: 'account', child: Text(l.categoryAccount)),
                    DropdownMenuItem(value: 'other', child: Text(l.categoryOther)),
                  ],
                  onChanged: (v) => setState(() => category = v!),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: subject,
                  maxLength: 200,
                  decoration: InputDecoration(labelText: l.ticketSubject),
                  validator: (v) => (v ?? '').trim().isEmpty ? l.fieldRequired : null,
                ),
                TextFormField(
                  controller: message,
                  maxLines: 4,
                  maxLength: 4000,
                  decoration: InputDecoration(labelText: l.ticketMessage),
                  validator: (v) => (v ?? '').trim().isEmpty ? l.fieldRequired : null,
                ),
                const SizedBox(height: 8),
                AsyncButton(
                  onPressed: () async {
                    if (!form.currentState!.validate()) return;
                    await ref.read(apiProvider).createTicket(TicketRequest(category: category, subject: subject.text.trim(), message: message.text.trim()));
                    if (ctx.mounted) Navigator.pop(ctx, true);
                  },
                  child: Text(l.send),
                ),
              ],
            ),
          ),
        ),
      ),
    );
    subject.dispose();
    message.dispose();
    if (sent == true) {
      ref.invalidate(ticketsProvider);
      if (context.mounted) showSnack(context, l.ticketSent);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final tickets = ref.watch(ticketsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.helpSupport)),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _newTicket(context, ref),
        icon: const Icon(Icons.edit_rounded),
        label: Text(l.newTicket),
      ),
      body: AsyncView(
        value: tickets,
        onRetry: () => ref.invalidate(ticketsProvider),
        data: (list) => list.isEmpty
            ? EmptyState(icon: Icons.support_agent_rounded, message: l.ticketsEmpty)
            : ListView(
                padding: const EdgeInsets.only(bottom: 96),
                children: [
                  for (final t in list)
                    Card(
                      margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(child: Text(t.subject, style: Theme.of(context).textTheme.titleSmall)),
                                Pill(t.status),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(t.message),
                            Text(formatDateTime(t.createdAt, context.lang), style: Theme.of(context).textTheme.bodySmall),
                            if (t.staffResponse != null) ...[
                              const Divider(),
                              Text(l.supportReply, style: Theme.of(context).textTheme.labelLarge),
                              Text(t.staffResponse!),
                            ],
                          ],
                        ),
                      ),
                    ),
                ],
              ),
      ),
    );
  }
}

// ---------------- Search ----------------

final _searchProvider = FutureProvider.autoDispose.family<List<SearchHitDto>, String>((ref, q) => ref.watch(apiProvider).search(q));

class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  String _q = '';

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Scaffold(
      appBar: AppBar(
        title: TextField(
          autofocus: true,
          textInputAction: TextInputAction.search,
          decoration: InputDecoration(hintText: l.searchHint, border: InputBorder.none, filled: false),
          onSubmitted: (v) => setState(() => _q = v.trim()),
        ),
      ),
      body: _q.length < 2
          ? const SizedBox.shrink()
          : AsyncView(
              value: ref.watch(_searchProvider(_q)),
              onRetry: () => ref.invalidate(_searchProvider(_q)),
              data: (hits) => hits.isEmpty
                  ? EmptyState(icon: Icons.search_off_rounded, message: l.noResults)
                  : ListView(
                      children: [
                        for (final h in hits)
                          ListTile(
                            leading: Icon(switch (h.kind) {
                              'course' => Icons.menu_book_outlined,
                              'video' => Icons.play_circle_outline_rounded,
                              'notes' => Icons.description_outlined,
                              'test' => Icons.timer_outlined,
                              _ => Icons.topic_outlined,
                            }),
                            title: Text(h.title),
                            subtitle: h.context == null ? null : Text(h.context!),
                            onTap: () => switch (h.kind) {
                              'course' => context.push('/course/${h.id}'),
                              'video' => context.push('/video/${h.id}'),
                              'test' => context.push('/test/${h.id}'),
                              'topic' => context.push('/topic/${h.id}'),
                              _ => null,
                            },
                          ),
                      ],
                    ),
            ),
    );
  }
}
