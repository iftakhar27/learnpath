import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../../widgets/question_view.dart';

final analyticsProvider = FutureProvider.autoDispose<StudentAnalyticsDto>((ref) => ref.watch(apiProvider).analytics());

class PracticeHomeScreen extends ConsumerWidget {
  const PracticeHomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final analytics = ref.watch(analyticsProvider);
    Widget mode(IconData icon, String title, String subtitle, String modeKey) => Card(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      child: ListTile(
        leading: Icon(icon, color: Theme.of(context).colorScheme.primary),
        title: Text(title),
        subtitle: Text(subtitle),
        trailing: const Icon(Icons.chevron_right_rounded),
        onTap: () => context.push('/practice/session', extra: CreatePracticeRequest(mode: modeKey, count: 10)),
      ),
    );
    return Scaffold(
      appBar: AppBar(title: Text(l.practiceTitle)),
      body: RefreshableList(
        onRefresh: () async => ref.invalidate(analyticsProvider),
        padding: const EdgeInsets.only(top: 8, bottom: 24),
        children: [
          mode(Icons.replay_rounded, l.practiceMistakes, l.practiceMistakesHint, 'incorrect'),
          mode(Icons.bookmark_outline_rounded, l.practiceBookmarked, l.practiceBookmarkedHint, 'bookmarked'),
          mode(Icons.history_edu_rounded, l.practicePyq, l.practicePyqHint, 'previous_year'),
          Card(
            margin: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            child: ListTile(
              leading: Icon(Icons.topic_outlined, color: Theme.of(context).colorScheme.primary),
              title: Text(l.practiceByTopic),
              subtitle: Text(l.practiceByTopicHint),
              trailing: const Icon(Icons.chevron_right_rounded),
              onTap: () => context.go('/learn'),
            ),
          ),
          if (analytics.value?.weakTopics.isNotEmpty ?? false) ...[
            SectionHeader(l.weakTopics),
            for (final w in analytics.value!.weakTopics)
              ListTile(
                leading: Icon(Icons.trending_down_rounded, color: Theme.of(context).colorScheme.warning),
                title: Text(w.topic),
                subtitle: Text('${w.subject} · ${l.accuracyOn(formatNumber(w.accuracy), w.attempted)}'),
                trailing: TextButton(
                  onPressed: () => context.push(
                    '/practice/session',
                    extra: CreatePracticeRequest(mode: 'topic', topicId: w.topicId),
                  ),
                  child: Text(l.practise),
                ),
              ),
          ],
        ],
      ),
    );
  }
}

/// One practice session: answer, see the solution immediately, move on. Retries never affect test results.
class PracticeSessionScreen extends ConsumerStatefulWidget {
  const PracticeSessionScreen({super.key, required this.request});
  final CreatePracticeRequest request;

  @override
  ConsumerState<PracticeSessionScreen> createState() => _PracticeSessionScreenState();
}

class _PracticeSessionScreenState extends ConsumerState<PracticeSessionScreen> {
  PracticeSessionDto? _session;
  Object? _error;
  int _index = 0;
  final _responses = <String, AnswerResponse?>{};
  final _feedback = <String, PracticeFeedbackDto>{};
  final _bookmarked = <String>{};
  final _watch = Stopwatch();
  bool _checking = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final s = await ref.read(apiProvider).startPractice(widget.request);
      setState(() => _session = s);
      _watch.start();
    } catch (e) {
      setState(() => _error = e);
    }
  }

  PracticeQuestionDto get _q => _session!.questions[_index];

  Future<void> _check() async {
    final response = _responses[_q.questionId];
    if (response == null) return;
    setState(() => _checking = true);
    try {
      final f = await ref
          .read(apiProvider)
          .answerPractice(
            _session!.sessionId,
            PracticeAnswerRequest(questionId: _q.questionId, response: response, timeSpentSeconds: _watch.elapsed.inSeconds),
          );
      setState(() => _feedback[_q.questionId] = f);
    } catch (e) {
      if (mounted) showSnack(context, errorText(context, e), error: true);
    } finally {
      if (mounted) setState(() => _checking = false);
    }
  }

  Future<void> _next() async {
    if (_index < _session!.questions.length - 1) {
      setState(() => _index++);
      _watch
        ..reset()
        ..start();
      return;
    }
    final summary = await ref.read(apiProvider).completePractice(_session!.sessionId);
    ref.invalidate(analyticsProvider);
    if (!mounted) return;
    await showModalBottomSheet<void>(
      context: context,
      builder: (_) => _SummarySheet(summary: summary),
    );
    if (mounted) context.pop();
  }

  Future<void> _bookmark() async {
    final id = _q.questionId;
    try {
      await ref.read(apiProvider).addBookmark(BookmarkType.question, id);
      setState(() => _bookmarked.add(id));
      if (mounted) showSnack(context, context.l10n.bookmarked);
    } catch (e) {
      if (mounted) showSnack(context, errorText(context, e), error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    if (_error != null) {
      final noQuestions = _error.toString().contains('no_questions');
      return Scaffold(
        appBar: AppBar(title: Text(l.practiceTitle)),
        body: noQuestions ? EmptyState(icon: Icons.inbox_outlined, message: l.noQuestionsForSelection) : ErrorView(error: _error!, onRetry: _load),
      );
    }
    if (_session == null) {
      return Scaffold(
        appBar: AppBar(title: Text(l.practiceTitle)),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    final q = _q;
    final feedback = _feedback[q.questionId];
    final total = _session!.questions.length;
    final marks = <String, OptionMark>{
      if (feedback != null) ...{
        for (final id in feedback.correctOptionIds) id: OptionMark.correct,
        for (final id in _responses[q.questionId]?.selectedOptionIds ?? const <String>[])
          if (!feedback.correctOptionIds.contains(id)) id: OptionMark.wrong,
      },
    };

    return Scaffold(
      appBar: AppBar(
        title: Text(l.questionOf(_index + 1, total)),
        actions: [
          IconButton(
            tooltip: _bookmarked.contains(q.questionId) ? l.bookmarked : l.bookmark,
            icon: Icon(_bookmarked.contains(q.questionId) ? Icons.bookmark_rounded : Icons.bookmark_outline_rounded),
            onPressed: _bookmarked.contains(q.questionId) ? null : _bookmark,
          ),
          IconButton(tooltip: l.reportQuestion, icon: const Icon(Icons.flag_outlined), onPressed: () => showReportSheet(context, ref, q.questionId)),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(4),
          child: LinearProgressIndicator(value: (_index + (feedback != null ? 1 : 0)) / total),
        ),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          QuestionView(
            key: ValueKey(q.questionId),
            type: q.type,
            stem: q.stem,
            imageUrl: q.imageUrl,
            options: q.options,
            response: _responses[q.questionId],
            enabled: feedback == null,
            marks: marks,
            onChanged: (r) => setState(() => _responses[q.questionId] = r),
            header: Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Wrap(spacing: 8, children: [Pill(q.difficulty), Pill('+${formatNumber(q.marks)} / −${formatNumber(q.negativeMarks)}')]),
            ),
          ),
          if (q.hint != null && feedback == null) ...[
            const SizedBox(height: 8),
            ExpansionTile(
              tilePadding: EdgeInsets.zero,
              title: Text(l.hint),
              children: [Align(alignment: Alignment.centerLeft, child: Text(q.hint!))],
            ),
          ],
          if (feedback != null) ...[const SizedBox(height: 8), FeedbackCard(feedback: feedback, type: q.type)],
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: feedback == null
              ? FilledButton(
                  onPressed: _responses[q.questionId] == null || _checking ? null : _check,
                  child: _checking ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)) : Text(l.checkAnswer),
                )
              : AsyncButton(onPressed: _next, child: Text(_index < total - 1 ? l.next : l.finish)),
        ),
      ),
    );
  }
}

class FeedbackCard extends StatelessWidget {
  const FeedbackCard({super.key, required this.feedback, required this.type});
  final PracticeFeedbackDto feedback;
  final String type;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final (label, color, bg, icon) = switch (feedback.outcome) {
      'Correct' => (l.correct, scheme.success, scheme.successContainer, Icons.check_circle_rounded),
      'PartiallyCorrect' => (l.partiallyCorrect, scheme.warning, scheme.warningContainer, Icons.adjust_rounded),
      _ => (l.incorrect, scheme.danger, scheme.dangerContainer, Icons.cancel_rounded),
    };
    return Card(
      color: bg,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: color),
                const SizedBox(width: 8),
                Text(
                  label,
                  style: TextStyle(color: color, fontWeight: FontWeight.w700),
                ),
              ],
            ),
            if (!isOptionType(type) && feedback.correctNumericAnswer != null) ...[
              const SizedBox(height: 8),
              Text(l.correctAnswer(formatNumber(feedback.correctNumericAnswer!))),
            ],
            if (feedback.explanation != null) ...[
              const SizedBox(height: 12),
              Text(l.explanation, style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: 4),
              Text(feedback.explanation!),
            ],
          ],
        ),
      ),
    );
  }
}

class _SummarySheet extends StatelessWidget {
  const _SummarySheet({required this.summary});
  final PracticeSummaryDto summary;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(l.practiceSummary, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 16),
            Text('${formatNumber(summary.accuracy)}%', style: Theme.of(context).textTheme.displaySmall?.copyWith(fontWeight: FontWeight.w700)),
            Text(l.accuracy),
            const SizedBox(height: 16),
            Wrap(
              spacing: 8,
              children: [
                Pill('${l.correct}: ${summary.correct}'),
                Pill('${l.incorrect}: ${summary.incorrect}'),
                if (summary.partial > 0) Pill('${l.partiallyCorrect}: ${summary.partial}'),
              ],
            ),
            const SizedBox(height: 24),
            FilledButton(onPressed: () => Navigator.of(context).pop(), child: Text(l.done)),
          ],
        ),
      ),
    );
  }
}

Future<void> showReportSheet(BuildContext context, WidgetRef ref, String questionId) async {
  final l = context.l10n;
  final details = TextEditingController();
  var reason = 'wrong_answer';
  final send = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setState) => Padding(
        padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.viewInsetsOf(ctx).bottom + 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(l.reportQuestion, style: Theme.of(ctx).textTheme.titleLarge),
            RadioGroup<String>(
              groupValue: reason,
              onChanged: (v) => setState(() => reason = v!),
              child: Column(
                children: [
                  for (final (key, label) in [
                    ('wrong_answer', l.reportReasonWrong),
                    ('typo', l.reportReasonTypo),
                    ('unclear', l.reportReasonUnclear),
                    ('other', l.reportReasonOther),
                  ])
                    RadioListTile<String>(value: key, title: Text(label), contentPadding: EdgeInsets.zero),
                ],
              ),
            ),
            TextField(
              controller: details,
              maxLines: 3,
              maxLength: 2000,
              decoration: InputDecoration(labelText: l.reportDetails),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(l.cancel)),
                FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(l.send)),
              ],
            ),
          ],
        ),
      ),
    ),
  );
  if (send == true) {
    try {
      await ref.read(apiProvider).reportQuestion(questionId, reason, details.text.trim().isEmpty ? null : details.text.trim());
      if (context.mounted) showSnack(context, l.reportThanks);
    } catch (e) {
      if (context.mounted) showSnack(context, errorText(context, e), error: true);
    }
  }
  details.dispose();
}
