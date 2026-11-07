import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../../widgets/question_view.dart';
import '../doubts/doubt_screens.dart';
import '../practice/practice_screens.dart';
import 'learn_screens.dart';

/// The quiz card on a topic screen: highlighted once every lesson is done.
class TopicQuizCard extends StatelessWidget {
  const TopicQuizCard({super.key, required this.topicId, required this.quiz, required this.covered});
  final String topicId;
  final TopicQuizSummaryDto quiz;
  final bool covered;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final highlight = covered && !quiz.passed;
    return Card(
      color: highlight ? scheme.primaryContainer : null,
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.fact_check_outlined, color: scheme.primary),
                const SizedBox(width: 8),
                Expanded(child: Text(l.topicQuiz, style: Theme.of(context).textTheme.titleMedium)),
                if (quiz.attempts > 0)
                  quiz.passed
                      ? Pill(l.quizPassed, color: scheme.success, background: scheme.successContainer)
                      : Pill(l.quizNotPassed, color: scheme.warning, background: scheme.warningContainer),
              ],
            ),
            const SizedBox(height: 4),
            Text(l.topicQuizInfo(quiz.questionCount, quiz.passPercent)),
            if (quiz.bestPercent != null) Text(l.quizBest(quiz.bestPercent!)),
            if (quiz.attempts == 0) ...[
              const SizedBox(height: 8),
              Text(covered ? l.topicQuizCovered : l.topicQuizNotCovered),
            ],
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: () => context.push('/topic/$topicId/quiz'),
              icon: const Icon(Icons.play_arrow_rounded),
              label: Text(quiz.attempts > 0 ? l.retakeQuiz : l.startQuiz),
            ),
          ],
        ),
      ),
    );
  }
}

class TopicQuizScreen extends ConsumerStatefulWidget {
  const TopicQuizScreen({super.key, required this.topicId});
  final String topicId;

  @override
  ConsumerState<TopicQuizScreen> createState() => _TopicQuizScreenState();
}

class _TopicQuizScreenState extends ConsumerState<TopicQuizScreen> {
  QuizDto? _quiz;
  QuizResultDto? _result;
  Object? _error;
  final _responses = <String, AnswerResponse?>{};
  final _watch = Stopwatch();
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _start();
  }

  /// Starting picks the questions, so each visit (and each retake) is a new quiz.
  Future<void> _start() async {
    setState(() {
      _error = null;
      _quiz = null;
      _result = null;
      _responses.clear();
    });
    try {
      final q = await ref.read(apiProvider).startTopicQuiz(widget.topicId);
      if (!mounted) return;
      setState(() => _quiz = q);
      _watch
        ..reset()
        ..start();
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  int get _unanswered => _quiz!.questions.where((q) => _responses[q.questionId] == null).length;

  Future<void> _submit() async {
    final l = context.l10n;
    final quiz = _quiz!;
    if (_unanswered > 0) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(l.submitQuiz),
          content: Text(l.quizUnanswered(_unanswered)),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(l.keepWorking)),
            FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(l.submit)),
          ],
        ),
      );
      if (ok != true) return;
    }
    setState(() => _submitting = true);
    try {
      final each = (_watch.elapsed.inSeconds / quiz.questions.length).round().clamp(0, 3600);
      final result = await ref
          .read(apiProvider)
          .submitQuiz(
            quiz.sessionId,
            QuizSubmitRequest(
              answers: [
                for (final q in quiz.questions)
                  QuizAnswer(questionId: q.questionId, response: _responses[q.questionId], timeSpentSeconds: each),
              ],
            ),
          );
      ref.invalidate(topicProvider(widget.topicId));
      if (mounted) setState(() => _result = result);
    } catch (e) {
      if (mounted) showSnack(context, errorText(context, e), error: true);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: Text(l.topicQuiz)),
        body: ErrorView(error: _error!, onRetry: _start),
      );
    }
    final quiz = _quiz;
    if (quiz == null) {
      return Scaffold(
        appBar: AppBar(title: Text(l.topicQuiz)),
        body: const Center(child: CircularProgressIndicator()),
      );
    }
    if (_result case final result?) {
      return _ResultView(quiz: quiz, result: result, responses: _responses, onRetake: _start);
    }

    final total = quiz.questions.length;
    return Scaffold(
      appBar: AppBar(
        title: Text(quiz.topic),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(4),
          child: LinearProgressIndicator(value: (total - _unanswered) / total),
        ),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(l.topicQuiz, style: Theme.of(context).textTheme.titleLarge),
          Text(l.topicQuizInfo(total, quiz.passPercent)),
          for (final (i, q) in quiz.questions.indexed)
            Card(
              margin: const EdgeInsets.only(top: 16),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: QuestionView(
                  key: ValueKey(q.questionId),
                  type: q.type,
                  stem: q.stem,
                  imageUrl: q.imageUrl,
                  options: q.options,
                  response: _responses[q.questionId],
                  onChanged: (r) => setState(() => _responses[q.questionId] = r),
                  header: Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(l.questionOf(i + 1, total), style: Theme.of(context).textTheme.labelLarge),
                  ),
                ),
              ),
            ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: FilledButton(
            onPressed: _submitting ? null : _submit,
            child: _submitting
                ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : Text(l.submitQuiz),
          ),
        ),
      ),
    );
  }
}

class _ResultView extends StatelessWidget {
  const _ResultView({required this.quiz, required this.result, required this.responses, required this.onRetake});
  final QuizDto quiz;
  final QuizResultDto result;
  final Map<String, AnswerResponse?> responses;
  final VoidCallback onRetake;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final items = {for (final i in result.items) i.questionId: i};
    return Scaffold(
      appBar: AppBar(title: Text(l.quizResult)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            color: result.passed ? scheme.successContainer : scheme.warningContainer,
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                children: [
                  Text(quiz.topic, style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
                  const SizedBox(height: 8),
                  Text(
                    '${result.scorePercent}%',
                    style: Theme.of(context).textTheme.displayMedium?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    result.passed ? l.quizPassedBody : l.quizFailedBody(result.passPercent),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    alignment: WrapAlignment.center,
                    children: [
                      Pill('${l.correct}: ${result.correct}'),
                      if (result.partial > 0) Pill('${l.partiallyCorrect}: ${result.partial}'),
                      Pill('${l.incorrect}: ${result.incorrect}'),
                      Pill('${l.skipped}: ${result.skipped}'),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    alignment: WrapAlignment.center,
                    children: [
                      if (result.passed && result.nextTopicId != null)
                        FilledButton(
                          onPressed: () => context.pushReplacement('/topic/${result.nextTopicId}'),
                          child: Text(l.nextTopic),
                        ),
                      OutlinedButton(onPressed: onRetake, child: Text(l.retakeQuiz)),
                      TextButton(onPressed: () => context.pop(), child: Text(l.backToTopic)),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(l.reviewAnswers, style: Theme.of(context).textTheme.titleLarge),
          for (final (i, q) in quiz.questions.indexed)
            if (items[q.questionId] case final item?)
              Card(
                margin: const EdgeInsets.only(top: 12),
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      QuestionView(
                        type: q.type,
                        stem: q.stem,
                        imageUrl: q.imageUrl,
                        options: q.options,
                        response: responses[q.questionId],
                        enabled: false,
                        onChanged: (_) {},
                        marks: {
                          for (final id in item.correctOptionIds) id: OptionMark.correct,
                          for (final id in responses[q.questionId]?.selectedOptionIds ?? const <String>[])
                            if (!item.correctOptionIds.contains(id)) id: OptionMark.wrong,
                        },
                        header: Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: Text(
                            l.questionOf(i + 1, quiz.questions.length),
                            style: Theme.of(context).textTheme.labelLarge,
                          ),
                        ),
                      ),
                      const SizedBox(height: 8),
                      FeedbackCard(feedback: item, type: q.type),
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Consumer(
                          builder: (context, ref, _) => TextButton.icon(
                            onPressed: () => showAskDoubtSheet(context, ref, questionId: q.questionId, about: q.stem),
                            icon: const Icon(Icons.forum_outlined),
                            label: Text(l.askDoubt),
                          ),
                        ),
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
