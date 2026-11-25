import 'dart:math';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/api/models.g.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../learn/subjects.dart' show subjectColor;

final customTestOptionsProvider = FutureProvider.autoDispose<CustomTestOptionsDto>(
  (ref) => ref.watch(apiProvider).customTestOptions(),
);
final myCustomTestsProvider = FutureProvider.autoDispose<List<TestSummaryDto>>(
  (ref) => ref.watch(apiProvider).myCustomTests(),
);

const _counts = [10, 20, 30, 45, 90, 180];

int _usable(CustomTestChapterDto c, Difficulty? d) => switch (d) {
  Difficulty.easy => c.easy,
  Difficulty.medium => c.medium,
  Difficulty.hard => c.hard,
  null => c.easy + c.medium + c.hard,
};

/// Pick NCERT chapters, how many questions and the difficulty; creating the test opens its instructions, ready to start.
class CustomTestScreen extends ConsumerStatefulWidget {
  const CustomTestScreen({super.key});

  @override
  ConsumerState<CustomTestScreen> createState() => _CustomTestScreenState();
}

class _CustomTestScreenState extends ConsumerState<CustomTestScreen> {
  String? _subject;
  final _selected = <String>{};
  int _count = 30;
  Difficulty? _difficulty;
  bool _skipCorrect = true;
  bool _creating = false;

  Future<void> _create(int questions) async {
    setState(() => _creating = true);
    try {
      final test = await ref
          .read(apiProvider)
          .createCustomTest(
            CreateCustomTestRequest(
              chapterIds: _selected.toList(),
              count: questions,
              difficulty: _difficulty,
              skipCorrect: _skipCorrect,
            ),
          );
      ref.invalidate(myCustomTestsProvider);
      if (mounted) context.pushReplacement('/test/${test.id}');
    } catch (e) {
      if (mounted) showSnack(context, errorText(context, e), error: true);
    } finally {
      if (mounted) setState(() => _creating = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final options = ref.watch(customTestOptionsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.customTestTitle)),
      body: AsyncView(
        value: options,
        onRetry: () => ref.invalidate(customTestOptionsProvider),
        data: (o) {
          if (o.subjects.isEmpty) return EmptyState(icon: Icons.assignment_outlined, message: l.customTestNoChapters);
          final current = o.subjects.firstWhere((s) => s.subject == _subject, orElse: () => o.subjects.first);
          final all = [for (final s in o.subjects) ...s.chapters];
          final available = all.where((c) => _selected.contains(c.id)).fold(0, (n, c) => n + _usable(c, _difficulty));
          final maxQuestions = min(available, o.maxQuestions);
          final questions = min(_count, maxQuestions);
          final minutes = max(5, questions * o.minutesPerQuestion);
          final color = subjectColor(context, current.subject);

          return Column(
            children: [
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.only(bottom: 16),
                  children: [
                    Padding(padding: const EdgeInsets.fromLTRB(16, 4, 16, 0), child: Text(l.customTestIntro)),
                    SectionHeader(
                      l.chooseChapters,
                      action: Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: Text(l.chaptersSelected(_selected.length), style: Theme.of(context).textTheme.bodySmall),
                      ),
                    ),
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: Row(
                        children: [
                          for (final s in o.subjects)
                            Padding(
                              padding: const EdgeInsets.only(right: 8),
                              child: ChoiceChip(
                                label: Text(() {
                                  final picked = s.chapters.where((c) => _selected.contains(c.id)).length;
                                  return picked > 0 ? '${s.title} ($picked)' : s.title;
                                }()),
                                selected: s.subject == current.subject,
                                onSelected: (_) => setState(() => _subject = s.subject),
                              ),
                            ),
                        ],
                      ),
                    ),
                    for (final cls in ['11', '12'])
                      if (current.chapters.where((c) => c.classLevel == cls).toList() case final chapters
                          when chapters.isNotEmpty) ...[
                        () {
                          final open = [
                            for (final c in chapters)
                              if (_usable(c, null) > 0) c.id,
                          ];
                          final allOn = open.isNotEmpty && open.every(_selected.contains);
                          return Padding(
                            padding: const EdgeInsets.fromLTRB(16, 12, 8, 0),
                            child: Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    '${current.title} · ${l.classLevel(cls)}',
                                    style: TextStyle(color: color, fontWeight: FontWeight.w700),
                                  ),
                                ),
                                if (open.length > 1)
                                  TextButton(
                                    onPressed: () =>
                                        setState(() => allOn ? _selected.removeAll(open) : _selected.addAll(open)),
                                    child: Text(allOn ? l.clearSelection : l.selectAll),
                                  ),
                              ],
                            ),
                          );
                        }(),
                        for (final c in chapters)
                          CheckboxListTile(
                            value: _selected.contains(c.id),
                            onChanged: _usable(c, null) == 0
                                ? null
                                : (on) => setState(() => on == true ? _selected.add(c.id) : _selected.remove(c.id)),
                            controlAffinity: ListTileControlAffinity.leading,
                            title: Text('${c.number}. ${c.title}'),
                            subtitle: Text(
                              _usable(c, null) == 0
                                  ? l.onlyInPaidCourses
                                  : [
                                      l.questionsAvailable(_usable(c, _difficulty)),
                                      if (c.locked > 0) l.moreInPaidCourses(c.locked),
                                    ].join(' · '),
                            ),
                          ),
                      ],
                    SectionHeader(l.numberOfQuestions),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(l.questionsAvailable(available), style: Theme.of(context).textTheme.bodySmall),
                          const SizedBox(height: 8),
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              for (final n in [
                                ..._counts.where((n) => n < maxQuestions),
                                if (maxQuestions > 0) maxQuestions,
                              ])
                                ChoiceChip(
                                  label: Text(
                                    n == maxQuestions && !_counts.contains(n) ? l.allQuestionsCount(n) : '$n',
                                  ),
                                  selected: questions == n,
                                  onSelected: (_) => setState(() => _count = n),
                                ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    SectionHeader(l.difficulty),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: SegmentedButton<String>(
                        segments: [
                          ButtonSegment(value: 'any', label: Text(l.difficultyAny)),
                          ButtonSegment(value: Difficulty.easy.wire, label: Text(l.difficultyEasy)),
                          ButtonSegment(value: Difficulty.medium.wire, label: Text(l.difficultyMedium)),
                          ButtonSegment(value: Difficulty.hard.wire, label: Text(l.difficultyHard)),
                        ],
                        selected: {_difficulty?.wire ?? 'any'},
                        showSelectedIcon: false,
                        onSelectionChanged: (s) =>
                            setState(() => _difficulty = s.first == 'any' ? null : Difficulty.fromJson(s.first)),
                      ),
                    ),
                    const SizedBox(height: 8),
                    SwitchListTile(
                      value: _skipCorrect,
                      onChanged: (v) => setState(() => _skipCorrect = v),
                      title: Text(l.skipCorrect),
                      subtitle: Text(l.skipCorrectHint),
                    ),
                  ],
                ),
              ),
              Material(
                elevation: 8,
                color: Theme.of(context).colorScheme.surface,
                child: SafeArea(
                  top: false,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
                    child: Row(
                      children: [
                        Expanded(
                          child: Semantics(
                            liveRegion: true,
                            child: Text(
                              _selected.isEmpty
                                  ? l.customTestPickChapter
                                  : '${l.questionsCount(questions)} · ${l.minutesCount(minutes)}',
                              style: const TextStyle(fontWeight: FontWeight.w600),
                            ),
                          ),
                        ),
                        FilledButton(
                          onPressed: _selected.isEmpty || questions == 0 || _creating ? null : () => _create(questions),
                          child: _creating
                              ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2))
                              : Text(l.createTest),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
