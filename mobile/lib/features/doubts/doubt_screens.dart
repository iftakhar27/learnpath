import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';
import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';

final doubtsProvider = FutureProvider.autoDispose<List<DoubtSummaryDto>>(
  (ref) async => (await ref.watch(apiProvider).doubts()).items,
);
final doubtProvider = FutureProvider.autoDispose.family<DoubtDto, String>(
  (ref, id) => ref.watch(apiProvider).doubt(id),
);

const _minLength = 5;
const _maxLength = 2000;

/// Opens the "Ask a doubt" sheet. Pass what the doubt is about (a topic, lesson or question) and its title.
Future<void> showAskDoubtSheet(
  BuildContext context,
  WidgetRef ref, {
  String? topicId,
  String? resourceId,
  String? questionId,
  String? about,
}) async {
  final sent = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (_) => _AskDoubtSheet(topicId: topicId, resourceId: resourceId, questionId: questionId, about: about),
  );
  if (sent == true) {
    ref.invalidate(doubtsProvider);
    if (context.mounted) showSnack(context, context.l10n.doubtSent);
  }
}

/// Owns its text field, so the field outlives the sheet's closing animation.
class _AskDoubtSheet extends ConsumerStatefulWidget {
  const _AskDoubtSheet({this.topicId, this.resourceId, this.questionId, this.about});
  final String? topicId;
  final String? resourceId;
  final String? questionId;
  final String? about;

  @override
  ConsumerState<_AskDoubtSheet> createState() => _AskDoubtSheetState();
}

class _AskDoubtSheetState extends ConsumerState<_AskDoubtSheet> {
  final _form = GlobalKey<FormState>();
  final _text = TextEditingController();

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.viewInsetsOf(context).bottom + 16),
      child: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(l.askDoubt, style: Theme.of(context).textTheme.titleLarge),
            if (widget.about case final about?) ...[
              const SizedBox(height: 4),
              Text(
                l.doubtAbout(about),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 12),
            TextFormField(
              controller: _text,
              autofocus: true,
              minLines: 4,
              maxLines: 8,
              maxLength: _maxLength,
              decoration: InputDecoration(labelText: l.yourDoubt, hintText: l.doubtHint, alignLabelWithHint: true),
              validator: (v) => (v ?? '').trim().length < _minLength ? l.doubtTooShort : null,
            ),
            const SizedBox(height: 8),
            AsyncButton(
              onPressed: () async {
                if (!_form.currentState!.validate()) return;
                await ref
                    .read(apiProvider)
                    .askDoubt(
                      AskDoubtRequest(
                        text: _text.text.trim(),
                        topicId: widget.topicId,
                        resourceId: widget.resourceId,
                        questionId: widget.questionId,
                      ),
                    );
                if (context.mounted) Navigator.pop(context, true);
              },
              child: Text(l.send),
            ),
          ],
        ),
      ),
    );
  }
}

/// "Ask a doubt" for app bars on lesson and question screens.
class AskDoubtAction extends ConsumerWidget {
  const AskDoubtAction({super.key, this.topicId, this.resourceId, this.questionId, this.about});
  final String? topicId;
  final String? resourceId;
  final String? questionId;
  final String? about;

  @override
  Widget build(BuildContext context, WidgetRef ref) => IconButton(
    tooltip: context.l10n.askDoubt,
    icon: const Icon(Icons.forum_outlined),
    onPressed: () =>
        showAskDoubtSheet(context, ref, topicId: topicId, resourceId: resourceId, questionId: questionId, about: about),
  );
}

class DoubtStatusPill extends StatelessWidget {
  const DoubtStatusPill(this.status, {super.key});
  final String status;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    return switch (status) {
      'Answered' => Pill(l.doubtStatusAnswered, color: scheme.success, background: scheme.successContainer),
      'Resolved' => Pill(l.doubtStatusResolved),
      _ => Pill(l.doubtStatusOpen, color: scheme.warning, background: scheme.warningContainer),
    };
  }
}

class DoubtsScreen extends ConsumerWidget {
  const DoubtsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final doubts = ref.watch(doubtsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.doubtsTitle)),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => showAskDoubtSheet(context, ref),
        icon: const Icon(Icons.forum_outlined),
        label: Text(l.askDoubt),
      ),
      body: AsyncView(
        value: doubts,
        onRetry: () => ref.invalidate(doubtsProvider),
        data: (list) => RefreshableList(
          onRefresh: () async => ref.invalidate(doubtsProvider),
          padding: const EdgeInsets.only(bottom: 96),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
              child: Text(l.doubtsIntro, style: Theme.of(context).textTheme.bodyMedium),
            ),
            if (list.isEmpty)
              EmptyState(icon: Icons.forum_outlined, message: l.doubtsEmpty)
            else
              for (final d in list)
                ListTile(
                  title: Text(d.text, maxLines: 2, overflow: TextOverflow.ellipsis),
                  subtitle: Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      '${d.context.topic != null ? l.doubtAbout(d.context.topic!) : l.doubtGeneral} · ${l.doubtReplies(d.replies)} · '
                      '${formatDateTime(d.updatedAt, context.lang)}',
                    ),
                  ),
                  trailing: DoubtStatusPill(d.status),
                  onTap: () async {
                    await context.push('/doubt/${d.id}');
                    ref.invalidate(doubtsProvider);
                  },
                ),
          ],
        ),
      ),
    );
  }
}

class DoubtScreen extends ConsumerStatefulWidget {
  const DoubtScreen({super.key, required this.doubtId});
  final String doubtId;

  @override
  ConsumerState<DoubtScreen> createState() => _DoubtScreenState();
}

class _DoubtScreenState extends ConsumerState<DoubtScreen> {
  final _reply = TextEditingController();

  @override
  void initState() {
    super.initState();
    _reply.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _reply.dispose();
    super.dispose();
  }

  Future<void> _act(Future<DoubtDto> Function() call) async {
    await call();
    ref.invalidate(doubtProvider(widget.doubtId));
    ref.invalidate(doubtsProvider);
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final doubt = ref.watch(doubtProvider(widget.doubtId));
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: Text(l.doubtsTitle)),
      body: AsyncView(
        value: doubt,
        onRetry: () => ref.invalidate(doubtProvider(widget.doubtId)),
        data: (d) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: d.context.topicId != null
                              ? InkWell(
                                  onTap: () => context.push('/topic/${d.context.topicId}'),
                                  child: Text(
                                    d.context.topic ?? '',
                                    style: TextStyle(color: scheme.primary, fontWeight: FontWeight.w600),
                                  ),
                                )
                              : Text(l.doubtGeneral, style: Theme.of(context).textTheme.labelLarge),
                        ),
                        DoubtStatusPill(d.status),
                      ],
                    ),
                    if (d.context.question ?? d.context.resource case final about?) ...[
                      const SizedBox(height: 8),
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(8),
                        decoration: BoxDecoration(
                          color: scheme.surfaceContainerHighest,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          about,
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ),
                    ],
                    const SizedBox(height: 12),
                    Text(d.text, style: Theme.of(context).textTheme.bodyLarge),
                    const SizedBox(height: 4),
                    Text(formatDateTime(d.createdAt, context.lang), style: Theme.of(context).textTheme.bodySmall),
                  ],
                ),
              ),
            ),
            for (final m in d.messages)
              Align(
                alignment: m.fromStaff ? Alignment.centerLeft : Alignment.centerRight,
                child: Container(
                  margin: const EdgeInsets.only(top: 12),
                  constraints: const BoxConstraints(maxWidth: 520),
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: m.fromStaff ? scheme.surfaceContainerHighest : scheme.primaryContainer,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${m.fromStaff ? '${l.doubtTeacher} · ${m.author}' : l.doubtYou} · ${formatDateTime(m.createdAt, context.lang)}',
                        style: Theme.of(context).textTheme.labelSmall,
                      ),
                      const SizedBox(height: 4),
                      Text(m.text),
                    ],
                  ),
                ),
              ),
            const SizedBox(height: 24),
            if (d.status == 'Resolved') ...[Text(l.doubtResolvedHint), const SizedBox(height: 8)],
            TextField(
              controller: _reply,
              minLines: 2,
              maxLines: 6,
              maxLength: _maxLength,
              decoration: InputDecoration(labelText: l.doubtFollowUp, alignLabelWithHint: true),
            ),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                AsyncButton(
                  onPressed: _reply.text.trim().isEmpty
                      ? null
                      : () async {
                          await _act(() => ref.read(apiProvider).followUpDoubt(widget.doubtId, _reply.text.trim()));
                          _reply.clear();
                        },
                  child: Text(l.send),
                ),
                if (d.status != 'Resolved')
                  AsyncButton(
                    outlined: true,
                    onPressed: () => _act(() => ref.read(apiProvider).resolveDoubt(widget.doubtId)),
                    child: Text(l.doubtMarkResolved),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
