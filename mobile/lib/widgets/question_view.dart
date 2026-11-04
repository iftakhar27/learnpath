import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import '../app/theme.dart';
import '../core/api/models.g.dart';
import 'common.dart';

/// Option-based question types; everything else takes a numeric answer.
bool isOptionType(String type) => type == 'SingleCorrect' || type == 'MultipleCorrect' || type == 'TrueFalse';

/// How an option should be drawn once the answer is known (practice feedback and result review).
enum OptionMark { none, correct, wrong }

/// Renders a question stem with its options or a numeric input. Stateless: the parent owns the response.
class QuestionView extends StatefulWidget {
  const QuestionView({
    super.key,
    required this.type,
    required this.stem,
    required this.options,
    required this.response,
    required this.onChanged,
    this.imageUrl,
    this.enabled = true,
    this.marks = const {},
    this.header,
  });

  final String type;
  final String stem;
  final String? imageUrl;
  final List<PlayerOptionDto> options;
  final AnswerResponse? response;
  final ValueChanged<AnswerResponse?> onChanged;
  final bool enabled;
  final Map<String, OptionMark> marks;
  final Widget? header;

  @override
  State<QuestionView> createState() => _QuestionViewState();
}

class _QuestionViewState extends State<QuestionView> {
  late final TextEditingController _numeric = TextEditingController(text: _numericText(widget.response));

  static String _numericText(AnswerResponse? r) {
    final v = r?.numericValue;
    if (v == null) return '';
    return v == v.roundToDouble() ? v.toInt().toString() : v.toString();
  }

  @override
  void didUpdateWidget(QuestionView old) {
    super.didUpdateWidget(old);
    // Keep the field in sync when the parent clears or replaces the response (e.g. "Clear", next question).
    final text = _numericText(widget.response);
    if (double.tryParse(_numeric.text) != widget.response?.numericValue && _numeric.text != text) _numeric.text = text;
  }

  @override
  void dispose() {
    _numeric.dispose();
    super.dispose();
  }

  void _toggle(String optionId) {
    final current = {...?widget.response?.selectedOptionIds};
    if (widget.type == 'MultipleCorrect') {
      current.contains(optionId) ? current.remove(optionId) : current.add(optionId);
    } else {
      current
        ..clear()
        ..add(optionId);
    }
    widget.onChanged(current.isEmpty ? null : AnswerResponse(selectedOptionIds: current.toList()));
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final selected = widget.response?.selectedOptionIds?.toSet() ?? const <String>{};
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ?widget.header,
        Text(widget.stem, style: Theme.of(context).textTheme.titleMedium?.copyWith(height: 1.4)),
        if (widget.imageUrl != null)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.network(widget.imageUrl!, errorBuilder: (_, _, _) => const SizedBox.shrink()),
            ),
          ),
        const SizedBox(height: 16),
        if (widget.type == 'MultipleCorrect')
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Text(l.selectAllThatApply, style: Theme.of(context).textTheme.labelLarge),
          ),
        if (isOptionType(widget.type))
          for (final (i, o) in widget.options.indexed)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: _OptionTile(
                letter: String.fromCharCode(65 + i),
                text: o.text,
                selected: selected.contains(o.id),
                multi: widget.type == 'MultipleCorrect',
                mark: widget.marks[o.id] ?? OptionMark.none,
                onTap: widget.enabled ? () => _toggle(o.id) : null,
              ),
            )
        else
          TextField(
            controller: _numeric,
            enabled: widget.enabled,
            keyboardType: TextInputType.numberWithOptions(decimal: widget.type == 'Numerical', signed: true),
            inputFormatters: [FilteringTextInputFormatter.allow(RegExp(widget.type == 'Integer' ? r'^-?\d*' : r'^-?\d*\.?\d*'))],
            decoration: InputDecoration(labelText: l.enterNumber),
            onChanged: (text) {
              final v = double.tryParse(text);
              widget.onChanged(v == null ? null : AnswerResponse(numericValue: v));
            },
          ),
        if (widget.marks.isNotEmpty && !isOptionType(widget.type)) const SizedBox.shrink(),
        if (!widget.enabled && selected.isEmpty && widget.response?.numericValue == null && widget.marks.isEmpty)
          Text(l.notAnswered, style: TextStyle(color: scheme.outline)),
      ],
    );
  }
}

class _OptionTile extends StatelessWidget {
  const _OptionTile({required this.letter, required this.text, required this.selected, required this.multi, required this.mark, this.onTap});
  final String letter;
  final String text;
  final bool selected;
  final bool multi;
  final OptionMark mark;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final (border, fill) = switch (mark) {
      OptionMark.correct => (scheme.success, scheme.successContainer),
      OptionMark.wrong => (scheme.danger, scheme.dangerContainer),
      OptionMark.none => selected ? (scheme.primary, scheme.primaryContainer.withValues(alpha: 0.5)) : (scheme.outlineVariant, scheme.surface),
    };
    return Semantics(
      button: true,
      selected: selected,
      inMutuallyExclusiveGroup: !multi,
      label: '$letter. $text',
      excludeSemantics: true,
      child: Material(
        color: fill,
        shape: RoundedRectangleBorder(
          side: BorderSide(color: border, width: selected || mark != OptionMark.none ? 2 : 1),
          borderRadius: BorderRadius.circular(10),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 14,
                  backgroundColor: selected ? border : scheme.surfaceContainerHighest,
                  child: Text(
                    letter,
                    style: TextStyle(fontWeight: FontWeight.w700, color: selected ? scheme.onPrimary : scheme.onSurfaceVariant),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(child: Text(text, style: Theme.of(context).textTheme.bodyLarge)),
                if (mark == OptionMark.correct) Icon(Icons.check_circle_rounded, color: scheme.success),
                if (mark == OptionMark.wrong) Icon(Icons.cancel_rounded, color: scheme.danger),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
