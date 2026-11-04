import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';
import '../../core/api/api_client.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../../widgets/question_view.dart';
import '../home/home_screen.dart';
import '../practice/practice_screens.dart';
import 'exam_session.dart';

final testsProvider = FutureProvider.autoDispose<List<TestSummaryDto>>((ref) => ref.watch(apiProvider).tests());
final testDetailProvider = FutureProvider.autoDispose.family<TestDetailDto, String>((ref, id) => ref.watch(apiProvider).testDetail(id));
final resultProvider = FutureProvider.autoDispose.family<ResultDto, String>((ref, id) => ref.watch(apiProvider).result(id));

class TestsScreen extends ConsumerWidget {
  const TestsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final tests = ref.watch(testsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.testsTitle)),
      body: AsyncView(
        value: tests,
        onRetry: () => ref.invalidate(testsProvider),
        data: (list) => list.isEmpty
            ? EmptyState(icon: Icons.assignment_outlined, message: l.testsEmpty)
            : RefreshableList(
                onRefresh: () async => ref.invalidate(testsProvider),
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                children: [
                  for (final t in list)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _TestCard(test: t),
                    ),
                ],
              ),
      ),
    );
  }
}

class _TestCard extends StatelessWidget {
  const _TestCard({required this.test});
  final TestSummaryDto test;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final exhausted = test.maxAttempts != null && test.attemptsUsed >= test.maxAttempts! && test.inProgressAttemptId == null;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(test.title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600)),
                ),
                if (test.locked) Icon(Icons.lock_outline_rounded, color: scheme.outline, semanticLabel: l.locked),
              ],
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                Pill(l.questionsCount(test.questionCount)),
                Pill(l.marksCount(formatNumber(test.maxMarks))),
                Pill(l.minutesCount(test.durationMinutes)),
                if (test.isFree) Pill(l.free, color: scheme.primary, background: scheme.primaryContainer),
                if (test.maxAttempts != null) Pill(l.attemptsUsed(test.attemptsUsed, test.maxAttempts!)),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                if (test.lastAttemptId != null) TextButton(onPressed: () => context.push('/result/${test.lastAttemptId}'), child: Text(l.viewResult)),
                const Spacer(),
                if (test.locked)
                  Text(l.purchaseRequired, style: Theme.of(context).textTheme.bodySmall)
                else if (!exhausted)
                  FilledButton(
                    onPressed: () => context.push('/test/${test.id}'),
                    child: Text(test.inProgressAttemptId != null ? l.resumeTest : (test.attemptsUsed > 0 ? l.retakeTest : l.startTest)),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class TestIntroScreen extends ConsumerWidget {
  const TestIntroScreen({super.key, required this.testId});
  final String testId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final detail = ref.watch(testDetailProvider(testId));
    return Scaffold(
      appBar: AppBar(title: Text(l.instructions)),
      body: AsyncView(
        value: detail,
        onRetry: () => ref.invalidate(testDetailProvider(testId)),
        data: (d) {
          final s = d.summary;
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text(s.title, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  Pill(l.questionsCount(s.questionCount)),
                  Pill(l.marksCount(formatNumber(s.maxMarks))),
                  Pill(l.minutesCount(s.durationMinutes)),
                  Pill(
                    d.negativeMarksPerWrong == null || d.negativeMarksPerWrong == 0
                        ? l.noNegativeMarking
                        : l.negativeMarking(formatNumber(d.negativeMarksPerWrong!)),
                  ),
                ],
              ),
              if (d.sections.isNotEmpty) ...[
                SectionHeader(l.sections),
                for (final sec in d.sections) ListTile(dense: true, leading: const Icon(Icons.segment_rounded), title: Text(sec)),
              ],
              if (d.instructions != null) ...[
                SectionHeader(l.instructions),
                Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: Text(d.instructions!)),
              ],
              const SizedBox(height: 16),
              Card(
                color: Theme.of(context).colorScheme.warningContainer,
                child: Padding(padding: const EdgeInsets.all(12), child: Text(l.testRules)),
              ),
            ],
          );
        },
      ),
      bottomNavigationBar: detail.hasValue
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: AsyncButton(
                  onPressed: () async {
                    final attempt = await ref.read(apiProvider).startAttempt(testId);
                    if (context.mounted) context.pushReplacement('/exam/${attempt.attemptId}', extra: attempt);
                  },
                  child: Text(detail.value!.summary.inProgressAttemptId != null ? l.resumeTest : l.startTest),
                ),
              ),
            )
          : null,
    );
  }
}

/// The timed test player. Answers save as they change; the server owns the clock and the score.
class ExamScreen extends ConsumerStatefulWidget {
  const ExamScreen({super.key, required this.attemptId, this.initial});
  final String attemptId;
  final AttemptDto? initial;

  @override
  ConsumerState<ExamScreen> createState() => _ExamScreenState();
}

class _ExamScreenState extends ConsumerState<ExamScreen> {
  ExamSession? _session;
  Object? _error;
  Timer? _ticker;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final api = ref.read(apiProvider);
      final attempt = widget.initial ?? await api.attempt(widget.attemptId);
      if (attempt.status != 'InProgress') {
        if (mounted) context.pushReplacement('/result/${attempt.attemptId}');
        return;
      }
      final session = ExamSession(attempt, save: api.saveAnswer)..addListener(_onChange);
      setState(() => _session = session);
      _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
        if (!mounted) return;
        setState(() {});
        if (session.timeUp && !_submitting) _submit(auto: true);
      });
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  void _onChange() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _ticker?.cancel();
    _session?.removeListener(_onChange);
    _session?.dispose();
    super.dispose();
  }

  Future<void> _submit({bool auto = false}) async {
    final s = _session!;
    final l = context.l10n;
    if (!auto) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(l.submitConfirmTitle),
          content: Text(l.submitConfirmBody(s.answeredCount, s.markedCount, s.unansweredCount)),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(l.keepWorking)),
            FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(l.submit)),
          ],
        ),
      );
      if (ok != true || !mounted) return;
    }
    setState(() => _submitting = true);
    if (auto) showSnack(context, l.timeUp);
    await s.prepareSubmit();
    try {
      await ref.read(apiProvider).submit(s.attempt.attemptId);
    } on ApiException catch (e) {
      // Already submitted elsewhere or auto-submitted by the server: the result is still available.
      if (e.status != 409 && e.status != 404) {
        if (mounted) {
          setState(() => _submitting = false);
          showSnack(context, errorText(context, e), error: true);
        }
        return;
      }
    }
    ref.invalidate(testsProvider);
    ref.invalidate(dashboardProvider);
    if (mounted) context.pushReplacement('/result/${s.attempt.attemptId}');
  }

  Future<bool> _confirmLeave() async {
    final l = context.l10n;
    return await showDialog<bool>(
          context: context,
          builder: (ctx) => AlertDialog(
            title: Text(l.leaveTestTitle),
            content: Text(l.leaveTestBody),
            actions: [
              TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(l.stay)),
              FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(l.leave)),
            ],
          ),
        ) ??
        false;
  }

  void _openPalette() {
    final s = _session!;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (ctx) => _Palette(
        session: s,
        onPick: (i) {
          Navigator.pop(ctx);
          s.goTo(i);
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(),
        body: ErrorView(error: _error!, onRetry: _load),
      );
    }
    final s = _session;
    if (s == null) return const Scaffold(body: Center(child: CircularProgressIndicator()));

    final scheme = Theme.of(context).colorScheme;
    final q = s.question;
    final lowTime = s.remainingSeconds < 300;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop || _submitting) return;
        if (await _confirmLeave() && context.mounted) {
          ref.invalidate(testsProvider);
          context.pop();
        }
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(s.attempt.testTitle, overflow: TextOverflow.ellipsis),
          actions: [
            Semantics(
              label: '${l.timeLeft} ${formatClock(s.remainingSeconds)}',
              liveRegion: s.remainingSeconds % 60 == 0,
              excludeSemantics: true,
              child: Container(
                margin: const EdgeInsets.only(right: 4),
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(color: lowTime ? scheme.dangerContainer : scheme.surfaceContainerHighest, borderRadius: BorderRadius.circular(8)),
                child: Row(
                  children: [
                    Icon(Icons.timer_outlined, size: 18, color: lowTime ? scheme.danger : null),
                    const SizedBox(width: 4),
                    Text(
                      formatClock(s.remainingSeconds),
                      style: TextStyle(fontWeight: FontWeight.w700, fontFeatures: const [FontFeature.tabularFigures()], color: lowTime ? scheme.danger : null),
                    ),
                  ],
                ),
              ),
            ),
            IconButton(tooltip: l.questionPalette, icon: const Icon(Icons.grid_view_rounded), onPressed: _openPalette),
          ],
        ),
        body: Column(
          children: [
            _SyncBanner(state: s.sync),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Row(
                    children: [
                      Text(l.questionOf(s.current + 1, s.questions.length), style: Theme.of(context).textTheme.labelLarge),
                      const SizedBox(width: 8),
                      if (q.section != null) Pill(q.section!),
                      const Spacer(),
                      Text('+${formatNumber(q.marks)} / −${formatNumber(q.negativeMarks)}', style: Theme.of(context).textTheme.labelMedium),
                    ],
                  ),
                  const SizedBox(height: 12),
                  QuestionView(
                    key: ValueKey(q.testQuestionId),
                    type: q.type,
                    stem: q.stem,
                    imageUrl: q.imageUrl,
                    options: q.options,
                    response: s.state.response,
                    enabled: !_submitting,
                    onChanged: (r) => s.setResponse(r, debounce: !isOptionType(q.type)),
                  ),
                ],
              ),
            ),
          ],
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: _submitting ? null : s.toggleMark,
                        icon: Icon(s.state.marked ? Icons.flag_rounded : Icons.outlined_flag_rounded, color: s.state.marked ? scheme.warning : null),
                        label: Text(s.state.marked ? l.unmarkReview : l.markForReview, overflow: TextOverflow.ellipsis),
                      ),
                    ),
                    const SizedBox(width: 8),
                    OutlinedButton(onPressed: _submitting || !s.state.answered ? null : s.clearResponse, child: Text(l.clearResponse)),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    IconButton.outlined(
                      tooltip: l.previous,
                      onPressed: s.current == 0 ? null : () => s.goTo(s.current - 1),
                      icon: const Icon(Icons.chevron_left_rounded),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: s.current < s.questions.length - 1
                          ? FilledButton(onPressed: () => s.goTo(s.current + 1), child: Text(l.saveAndNext))
                          : FilledButton(
                              onPressed: _submitting ? null : () => _submit(),
                              child: _submitting ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)) : Text(l.submitTest),
                            ),
                    ),
                    if (s.current < s.questions.length - 1) ...[
                      const SizedBox(width: 8),
                      TextButton(onPressed: _submitting ? null : () => _submit(), child: Text(l.submit)),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _SyncBanner extends StatelessWidget {
  const _SyncBanner({required this.state});
  final SyncState state;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    return AnimatedSize(
      duration: const Duration(milliseconds: 200),
      child: switch (state) {
        SyncState.offline => Container(
          width: double.infinity,
          color: scheme.warningContainer,
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Row(
            children: [
              Icon(Icons.cloud_off_rounded, size: 18, color: scheme.warning),
              const SizedBox(width: 8),
              Expanded(child: Text(l.offlineWillRetry)),
            ],
          ),
        ),
        SyncState.saving => Padding(
          padding: const EdgeInsets.only(top: 4, right: 16),
          child: Align(
            alignment: Alignment.centerRight,
            child: Text(l.saving, style: Theme.of(context).textTheme.labelSmall),
          ),
        ),
        SyncState.idle => const SizedBox(width: double.infinity),
      },
    );
  }
}

Color paletteColor(ColorScheme scheme, PaletteStatus s) => switch (s) {
  PaletteStatus.answered => scheme.success,
  PaletteStatus.notAnswered => scheme.danger,
  PaletteStatus.marked || PaletteStatus.answeredAndMarked => scheme.warning,
  PaletteStatus.notVisited => scheme.surfaceContainerHighest,
};

class _Palette extends StatelessWidget {
  const _Palette({required this.session, required this.onPick});
  final ExamSession session;
  final ValueChanged<int> onPick;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    Widget legend(PaletteStatus s, String label) => Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 14,
          height: 14,
          decoration: BoxDecoration(color: paletteColor(scheme, s), borderRadius: BorderRadius.circular(4)),
        ),
        const SizedBox(width: 6),
        Text(label, style: Theme.of(context).textTheme.labelSmall),
      ],
    );
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(l.questionPalette, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Wrap(
              spacing: 12,
              runSpacing: 6,
              children: [
                legend(PaletteStatus.answered, l.paletteAnswered),
                legend(PaletteStatus.notAnswered, l.paletteNotAnswered),
                legend(PaletteStatus.marked, l.paletteMarked),
                legend(PaletteStatus.notVisited, l.paletteNotVisited),
              ],
            ),
            const SizedBox(height: 16),
            Flexible(
              child: GridView.count(
                crossAxisCount: 6,
                shrinkWrap: true,
                mainAxisSpacing: 8,
                crossAxisSpacing: 8,
                children: [
                  for (final (i, q) in session.questions.indexed)
                    Semantics(
                      button: true,
                      selected: i == session.current,
                      label:
                          '${l.questionOf(i + 1, session.questions.length)}, ${switch (q.status) {
                            PaletteStatus.answered => l.paletteAnswered,
                            PaletteStatus.notAnswered => l.paletteNotAnswered,
                            PaletteStatus.marked || PaletteStatus.answeredAndMarked => l.paletteMarked,
                            PaletteStatus.notVisited => l.paletteNotVisited,
                          }}',
                      excludeSemantics: true,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => onPick(i),
                        child: Container(
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: paletteColor(scheme, q.status),
                            borderRadius: BorderRadius.circular(8),
                            border: i == session.current ? Border.all(color: scheme.primary, width: 3) : null,
                          ),
                          child: Text(
                            '${i + 1}',
                            style: TextStyle(fontWeight: FontWeight.w700, color: q.status == PaletteStatus.notVisited ? scheme.onSurfaceVariant : Colors.white),
                          ),
                        ),
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

class ResultScreen extends ConsumerWidget {
  const ResultScreen({super.key, required this.attemptId});
  final String attemptId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final result = ref.watch(resultProvider(attemptId));
    return Scaffold(
      appBar: AppBar(title: Text(l.resultTitle)),
      body: AsyncView(
        value: result,
        onRetry: () => ref.invalidate(resultProvider(attemptId)),
        data: (r) {
          final scheme = Theme.of(context).colorScheme;
          final text = Theme.of(context).textTheme;
          return ListView(
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              Padding(
                padding: const EdgeInsets.all(16),
                child: Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(r.testTitle, style: text.titleMedium),
                        Text(l.attemptNumber(r.attemptNumber), style: text.bodySmall),
                        if (r.status == 'AutoSubmitted') Text(l.autoSubmitted, style: text.bodySmall?.copyWith(color: scheme.warning)),
                        const SizedBox(height: 12),
                        Text(l.scoreOf(formatNumber(r.score), formatNumber(r.maxScore)), style: text.displaySmall?.copyWith(fontWeight: FontWeight.w700)),
                        Text('${formatNumber(r.percentage)}% · ${l.accuracy} ${formatNumber(r.accuracy)}%'),
                        const SizedBox(height: 12),
                        Wrap(
                          spacing: 8,
                          runSpacing: 4,
                          children: [
                            Pill('${l.correct}: ${r.correct}', color: scheme.success, background: scheme.successContainer),
                            Pill('${l.incorrect}: ${r.incorrect}', color: scheme.danger, background: scheme.dangerContainer),
                            if (r.partial > 0) Pill('${l.partiallyCorrect}: ${r.partial}'),
                            Pill('${l.skipped}: ${r.skipped}'),
                            Pill('${l.timeTaken}: ${formatClock(r.timeTakenSeconds)}'),
                          ],
                        ),
                        if (r.percentile != null) ...[
                          const SizedBox(height: 12),
                          Text(l.percentileLine(formatNumber(r.percentile!.percentile))),
                          Text(l.notOfficialRank, style: text.bodySmall),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
              if (r.subjects.length > 1 || r.chapters.isNotEmpty) ...[
                SectionHeader(r.subjects.length > 1 ? l.subjectWise : l.chapterWise),
                for (final b in r.subjects.length > 1 ? r.subjects : r.chapters) _BreakdownRow(b: b),
              ],
              if (r.recommendations.isNotEmpty) ...[
                SectionHeader(l.recommendations),
                for (final tip in r.recommendations) ListTile(leading: const Icon(Icons.lightbulb_outline_rounded), title: Text(tip)),
              ],
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    FilledButton(onPressed: () => context.push('/review/$attemptId'), child: Text(l.reviewAnswers)),
                    const SizedBox(height: 8),
                    OutlinedButton(onPressed: () => context.go('/tests'), child: Text(l.backToTests)),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _BreakdownRow extends StatelessWidget {
  const _BreakdownRow({required this.b});
  final BreakdownDto b;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(b.name)),
              Text('${formatNumber(b.score)} / ${formatNumber(b.maxScore)} · ${formatNumber(b.accuracy)}%'),
            ],
          ),
          const SizedBox(height: 4),
          LinearProgressIndicator(
            value: b.maxScore == 0 ? 0 : (b.score / b.maxScore).clamp(0, 1),
            minHeight: 8,
            borderRadius: BorderRadius.circular(4),
            color: b.accuracy >= 60 ? scheme.success : scheme.warning,
          ),
        ],
      ),
    );
  }
}

enum _ReviewFilter { all, incorrect, skipped }

class ReviewScreen extends ConsumerStatefulWidget {
  const ReviewScreen({super.key, required this.attemptId});
  final String attemptId;

  @override
  ConsumerState<ReviewScreen> createState() => _ReviewScreenState();
}

class _ReviewScreenState extends ConsumerState<ReviewScreen> {
  _ReviewFilter _filter = _ReviewFilter.all;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final result = ref.watch(resultProvider(widget.attemptId));
    return Scaffold(
      appBar: AppBar(title: Text(l.reviewAnswers)),
      body: AsyncView(
        value: result,
        onRetry: () => ref.invalidate(resultProvider(widget.attemptId)),
        data: (r) {
          final qs = r.questions.where(
            (q) => switch (_filter) {
              _ReviewFilter.all => true,
              _ReviewFilter.incorrect => q.outcome == 'Incorrect' || q.outcome == 'PartiallyCorrect',
              _ReviewFilter.skipped => q.outcome == 'Skipped',
            },
          );
          return ListView(
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              Padding(
                padding: const EdgeInsets.all(16),
                child: SegmentedButton<_ReviewFilter>(
                  showSelectedIcon: false,
                  segments: [
                    ButtonSegment(value: _ReviewFilter.all, label: Text(l.filterAll)),
                    ButtonSegment(value: _ReviewFilter.incorrect, label: Text(l.filterIncorrect)),
                    ButtonSegment(value: _ReviewFilter.skipped, label: Text(l.filterSkipped)),
                  ],
                  selected: {_filter},
                  onSelectionChanged: (s) => setState(() => _filter = s.first),
                ),
              ),
              for (final q in qs) _ReviewCard(q: q, total: r.questions.length),
              if (qs.isEmpty) EmptyState(icon: Icons.check_rounded, message: l.emptyGeneric),
            ],
          );
        },
      ),
    );
  }
}

class _ReviewCard extends ConsumerWidget {
  const _ReviewCard({required this.q, required this.total});
  final ReviewQuestionDto q;
  final int total;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final (label, color) = switch (q.outcome) {
      'Correct' => (l.correct, scheme.success),
      'PartiallyCorrect' => (l.partiallyCorrect, scheme.warning),
      'Incorrect' => (l.incorrect, scheme.danger),
      _ => (l.skipped, scheme.outline),
    };
    return Card(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Separate semantics nodes so screen readers announce the header, question and explanation in turn.
            Semantics(
              container: true,
              child: Row(
                children: [
                  Text(l.questionOf(q.number, total), style: Theme.of(context).textTheme.labelLarge),
                  const SizedBox(width: 8),
                  Pill(label, color: color),
                  const Spacer(),
                  Text(l.marksAwarded(formatNumber(q.marksAwarded))),
                  IconButton(
                    tooltip: l.reportQuestion,
                    icon: const Icon(Icons.flag_outlined, size: 20),
                    onPressed: () => showReportSheet(context, ref, q.questionId),
                  ),
                ],
              ),
            ),
            Semantics(container: true, child: Text('${q.subject} · ${q.topic}', style: Theme.of(context).textTheme.bodySmall)),
            const SizedBox(height: 8),
            QuestionView(
              type: q.type,
              stem: q.stem,
              imageUrl: q.imageUrl,
              options: [for (final o in q.options) PlayerOptionDto(id: o.id, text: o.text)],
              response: AnswerResponse(
                selectedOptionIds: [
                  for (final o in q.options)
                    if (o.selected) o.id,
                ],
                numericValue: q.yourNumericAnswer,
              ),
              enabled: false,
              marks: {
                for (final o in q.options)
                  if (o.isCorrect) o.id: OptionMark.correct else if (o.selected) o.id: OptionMark.wrong,
              },
              onChanged: (_) {},
            ),
            if (!isOptionType(q.type)) ...[
              Text(l.yourAnswer(q.yourNumericAnswer == null ? l.notAnswered : formatNumber(q.yourNumericAnswer!))),
              if (q.correctNumericAnswer != null) Text(l.correctAnswer(formatNumber(q.correctNumericAnswer!)), style: TextStyle(color: scheme.success)),
            ],
            if (q.explanation != null) ...[
              const SizedBox(height: 8),
              Semantics(
                container: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [Text(l.explanation, style: Theme.of(context).textTheme.titleSmall), Text(q.explanation!)],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
