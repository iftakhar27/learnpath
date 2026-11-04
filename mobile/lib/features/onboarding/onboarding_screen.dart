import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/api/models.g.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../auth/auth_screens.dart';

final examsProvider = FutureProvider.autoDispose<List<ExamDto>>((ref) => ref.watch(apiProvider).exams());

const _classes = ['9', '10', '11', '12', 'Dropper'];
const _boards = ['CBSE', 'ICSE', 'State'];

/// First-run setup, also reused from Profile → Study preferences (`editing: true`).
class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key, this.editing = false});
  final bool editing;

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  int _step = 0;
  String? _examId;
  int _year = DateTime.now().year + 1;
  String? _class;
  String? _board;
  double _minutes = 120;
  bool _minor = false;
  bool _consent = false;
  final _guardian = TextEditingController();
  String? _error;

  @override
  void initState() {
    super.initState();
    final p = ref.read(profileProvider);
    _examId = p.targetExamId;
    _year = p.targetYear ?? _year;
    _class = p.classLevel;
    _board = p.board;
    _minutes = p.dailyStudyMinutes.clamp(30, 480).toDouble();
    _minor = p.isMinor;
    _guardian.text = p.guardianName ?? '';
  }

  @override
  void dispose() {
    _guardian.dispose();
    super.dispose();
  }

  Future<void> _finish() async {
    final l = context.l10n;
    if (_examId == null) return setState(() => _error = l.selectExamError);
    if (_minor && (_guardian.text.trim().isEmpty || !_consent)) return setState(() => _error = l.guardianConsent);
    setState(() => _error = null);
    final profile = await ref
        .read(apiProvider)
        .updateProfile(
          UpdateProfileRequest(
            targetExamId: _examId,
            targetYear: _year,
            classLevel: _class,
            board: _board,
            dailyStudyMinutes: _minutes.round(),
            language: ref.read(localeProvider).languageCode,
            isMinor: _minor,
            guardianName: _minor ? _guardian.text.trim() : null,
            guardianConsent: _minor && _consent ? true : null,
            completeOnboarding: true,
          ),
        );
    ref.read(sessionProvider.notifier).profileUpdated(profile);
    if (widget.editing && mounted) {
      showSnack(context, l.savedToast);
      context.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final exams = ref.watch(examsProvider);
    final years = List.generate(4, (i) => DateTime.now().year + i);

    final steps = <Widget>[
      // 1. Exam and target year
      AsyncView(
        value: exams,
        onRetry: () => ref.invalidate(examsProvider),
        data: (list) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(l.chooseExam, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            RadioGroup<String>(
              groupValue: _examId,
              onChanged: (v) => setState(() => _examId = v),
              child: Column(
                children: [
                  for (final e in list)
                    Card(
                      margin: const EdgeInsets.only(bottom: 8),
                      child: RadioListTile<String>(value: e.id, title: Text(e.name)),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            Text(l.targetYear, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              children: [for (final y in years) ChoiceChip(label: Text('$y'), selected: _year == y, onSelected: (_) => setState(() => _year = y))],
            ),
          ],
        ),
      ),
      // 2. Class and board
      ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(l.yourClass, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final c in _classes)
                ChoiceChip(
                  label: Text(c == 'Dropper' ? l.classDropper : l.classLabel(c)),
                  selected: _class == c,
                  onSelected: (_) => setState(() => _class = c),
                ),
            ],
          ),
          const SizedBox(height: 24),
          Text(l.board, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            children: [
              for (final b in _boards)
                ChoiceChip(label: Text(b == 'State' ? l.stateBoard : b), selected: _board == b, onSelected: (_) => setState(() => _board = b)),
            ],
          ),
        ],
      ),
      // 3. Study time, language, guardian consent
      ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(l.dailyStudyTime, style: Theme.of(context).textTheme.titleLarge),
          Slider(value: _minutes, min: 30, max: 480, divisions: 15, label: l.minutesPerDay(_minutes.round()), onChanged: (v) => setState(() => _minutes = v)),
          Text(l.minutesPerDay(_minutes.round()), textAlign: TextAlign.center),
          const SizedBox(height: 24),
          Text(l.language, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          const Align(alignment: Alignment.centerLeft, child: LanguageToggle()),
          const SizedBox(height: 24),
          SwitchListTile(contentPadding: EdgeInsets.zero, value: _minor, title: Text(l.under18), onChanged: (v) => setState(() => _minor = v)),
          if (_minor) ...[
            TextField(
              controller: _guardian,
              textCapitalization: TextCapitalization.words,
              decoration: InputDecoration(labelText: l.guardianName),
            ),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              value: _consent,
              title: Text(l.guardianConsent),
              onChanged: (v) => setState(() => _consent = v ?? false),
            ),
          ],
        ],
      ),
    ];

    final last = _step == steps.length - 1;
    return Scaffold(
      appBar: AppBar(title: Text(widget.editing ? l.editPreferences : l.onboardingTitle)),
      body: Column(
        children: [
          LinearProgressIndicator(value: (_step + 1) / steps.length),
          Expanded(child: steps[_step]),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  if (_step > 0)
                    Expanded(
                      child: OutlinedButton(onPressed: () => setState(() => _step--), child: Text(l.back)),
                    ),
                  if (_step > 0) const SizedBox(width: 12),
                  Expanded(
                    child: last
                        ? AsyncButton(onPressed: _finish, child: Text(widget.editing ? l.save : l.getStarted))
                        : FilledButton(
                            onPressed: () {
                              if (_step == 0 && _examId == null) return setState(() => _error = l.selectExamError);
                              setState(() {
                                _error = null;
                                _step++;
                              });
                            },
                            child: Text(l.next),
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
