import 'dart:math';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/api/models.g.dart';
import '../../core/format.dart';
import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../learn/subjects.dart' show subjectColor, subjectIcon;

final flashcardsProvider = FutureProvider.autoDispose<FlashcardsHomeDto>((ref) => ref.watch(apiProvider).flashcards());
final flashcardDeckProvider = FutureProvider.autoDispose.family<FlashcardDeckDetailDto, String>(
  (ref, chapterId) => ref.watch(apiProvider).flashcardDeck(chapterId),
);

/// Every chapter with flash cards, grouped by NCERT book, with what's due and what's mastered.
class FlashcardDecksScreen extends ConsumerWidget {
  const FlashcardDecksScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l = context.l10n;
    final home = ref.watch(flashcardsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l.flashcardsTitle)),
      body: AsyncView(
        value: home,
        onRetry: () => ref.invalidate(flashcardsProvider),
        data: (h) {
          if (h.decks.isEmpty) return EmptyState(icon: Icons.style_outlined, message: l.flashcardsEmpty);
          final books = <String, List<FlashcardDeckDto>>{};
          for (final d in h.decks) {
            books.putIfAbsent('${d.subject}-${d.classLevel}', () => []).add(d);
          }
          return RefreshableList(
            onRefresh: () async => ref.invalidate(flashcardsProvider),
            children: [
              Padding(padding: const EdgeInsets.fromLTRB(16, 4, 16, 0), child: Text(l.flashcardsIntro)),
              if (h.dueTotal > 0)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: Pill(
                      l.flashcardsDue(h.dueTotal),
                      color: Theme.of(context).colorScheme.primary,
                      background: Theme.of(context).colorScheme.primaryContainer,
                    ),
                  ),
                ),
              for (final decks in books.values) ...[
                SectionHeader('${decks.first.subjectTitle} · ${l.classLevel(decks.first.classLevel)}'),
                for (final d in decks)
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                    child: _DeckTile(deck: d),
                  ),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _DeckTile extends StatelessWidget {
  const _DeckTile({required this.deck});
  final FlashcardDeckDto deck;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final color = subjectColor(context, deck.subject);
    final dark = Theme.of(context).brightness == Brightness.dark;
    // One button for screen readers: "Chapter 1, Units and Measurement, 11 cards · 3 cards due".
    return Card(
      clipBehavior: Clip.antiAlias,
      child: MergeSemantics(
        child: Semantics(
          button: true,
          child: InkWell(
            onTap: () => context.push('/flashcards/${deck.chapterId}'),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: dark ? 0.22 : 0.12),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Icon(subjectIcon(deck.subject, deck.classLevel), color: color),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(l.chapterNumber(deck.number), style: Theme.of(context).textTheme.bodySmall),
                        Text(deck.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                        const SizedBox(height: 2),
                        Text(
                          [
                            l.flashcardsCount(deck.cards),
                            l.flashcardsDue(deck.due),
                            if (deck.mastered > 0) l.flashcardsMastered(deck.mastered),
                          ].join(' · '),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                        const SizedBox(height: 6),
                        ExcludeSemantics(
                          // The text above already says how many are mastered.
                          child: LinearProgressIndicator(
                            value: deck.mastered / max(1, deck.cards),
                            minHeight: 5,
                            borderRadius: BorderRadius.circular(3),
                            color: color,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right_rounded),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A study session: the due cards (new ones included), or every card on request. Tap the card to flip it; answer with
/// the buttons or by swiping it (right: got it, left: still learning).
class FlashcardStudyScreen extends ConsumerStatefulWidget {
  const FlashcardStudyScreen({super.key, required this.chapterId});
  final String chapterId;

  @override
  ConsumerState<FlashcardStudyScreen> createState() => _FlashcardStudyScreenState();
}

class _FlashcardStudyScreenState extends ConsumerState<FlashcardStudyScreen> {
  /// null until a deck has loaded; empty when nothing is due (caught up).
  List<FlashcardDto>? _session;
  int _index = 0;
  int _known = 0;
  bool _flipped = false;
  bool _saving = false;

  void _start(List<FlashcardDto> cards) => setState(() {
    _session = cards;
    _index = 0;
    _known = 0;
    _flipped = false;
  });

  Future<void> _answer(bool known) async {
    final session = _session;
    if (session == null || _index >= session.length || !_flipped || _saving) return;
    setState(() => _saving = true);
    try {
      await ref.read(apiProvider).reviewFlashcard(session[_index].id, known: known);
      ref.invalidate(flashcardsProvider);
      if (!mounted) return;
      setState(() {
        if (known) _known++;
        _index++;
        _flipped = false;
      });
    } catch (e) {
      if (mounted) showSnack(context, errorText(context, e), error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _restart() async {
    ref.invalidate(flashcardDeckProvider(widget.chapterId));
    final deck = await ref.read(flashcardDeckProvider(widget.chapterId).future);
    _start(deck.cards.where((c) => c.isDue).toList());
  }

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final deck = ref.watch(flashcardDeckProvider(widget.chapterId));
    // The first load starts with what's due; later refreshes don't interrupt a session.
    if (_session == null && deck.value != null) {
      _session = deck.value!.cards.where((c) => c.isDue).toList();
    }
    return Scaffold(
      appBar: AppBar(title: Text(deck.value?.deck.title ?? l.flashcardsTitle)),
      body: AsyncView(
        value: deck,
        onRetry: () => ref.invalidate(flashcardDeckProvider(widget.chapterId)),
        data: (d) {
          final session = _session!;
          final color = subjectColor(context, d.deck.subject);
          final header = Text(
            '${d.deck.subjectTitle} · ${l.classLevel(d.deck.classLevel)} · ${l.chapterNumber(d.deck.number)}',
            style: TextStyle(color: color, fontWeight: FontWeight.w600),
          );
          if (session.isEmpty) {
            return _Centered(
              children: [
                header,
                const SizedBox(height: 24),
                Icon(Icons.celebration_outlined, size: 48, color: color),
                const SizedBox(height: 12),
                Text(l.allCaughtUp, style: Theme.of(context).textTheme.titleLarge),
                if (d.nextDueAt case final next?) Text(l.nextCardsDue(formatDate(next, context.lang))),
                const SizedBox(height: 16),
                FilledButton(onPressed: () => _start(d.cards), child: Text(l.reviseAll)),
              ],
            );
          }
          if (_index >= session.length) {
            final again = session.length - _known;
            return _Centered(
              children: [
                header,
                const SizedBox(height: 24),
                Icon(Icons.task_alt_rounded, size: 48, color: color),
                const SizedBox(height: 12),
                Semantics(liveRegion: true, child: Text(l.sessionDone, style: Theme.of(context).textTheme.titleLarge)),
                Text(l.sessionKnew(_known, session.length)),
                if (again > 0) Text(l.sessionAgainSoon(again)),
                const SizedBox(height: 16),
                FilledButton(onPressed: _restart, child: Text(l.studyAgain)),
                TextButton(onPressed: () => context.pop(), child: Text(l.allDecks)),
              ],
            );
          }
          final card = session[_index];
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              header,
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(child: Text(l.cardProgress(_index + 1, session.length))),
                  Text(l.flashcardsMastered(d.deck.mastered), style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
              const SizedBox(height: 6),
              LinearProgressIndicator(
                value: _index / session.length,
                minHeight: 6,
                borderRadius: BorderRadius.circular(3),
                semanticsLabel: l.cardProgress(_index + 1, session.length),
              ),
              const SizedBox(height: 16),
              GestureDetector(
                onHorizontalDragEnd: (e) {
                  final v = e.primaryVelocity ?? 0;
                  if (v.abs() > 300) _answer(v > 0);
                },
                child: _FlipCard(
                  key: ValueKey(card.id),
                  front: card.front,
                  back: card.back,
                  flipped: _flipped,
                  color: color,
                  onTap: () => setState(() => _flipped = !_flipped),
                ),
              ),
              const SizedBox(height: 16),
              if (_flipped)
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: _saving ? null : () => _answer(false),
                        child: Text(l.stillLearning),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: FilledButton(onPressed: _saving ? null : () => _answer(true), child: Text(l.gotIt)),
                    ),
                  ],
                )
              else
                OutlinedButton(onPressed: () => setState(() => _flipped = true), child: Text(l.showAnswer)),
            ],
          );
        },
      ),
    );
  }
}

class _Centered extends StatelessWidget {
  const _Centered({required this.children});
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Center(
    child: SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(mainAxisSize: MainAxisSize.min, children: children),
    ),
  );
}

/// The card: front (prompt) and back (answer), turning over in 3D when tapped.
class _FlipCard extends StatelessWidget {
  const _FlipCard({
    super.key,
    required this.front,
    required this.back,
    required this.flipped,
    required this.color,
    required this.onTap,
  });
  final String front;
  final String back;
  final bool flipped;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final dark = Theme.of(context).brightness == Brightness.dark;
    Widget face(String label, String text, {required bool isBack}) => Container(
      constraints: const BoxConstraints(minHeight: 240),
      width: double.infinity,
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: isBack ? color.withValues(alpha: dark ? 0.2 : 0.1) : scheme.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: scheme.outlineVariant),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            label.toUpperCase(),
            style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 12, letterSpacing: 1),
          ),
          const SizedBox(height: 12),
          Text(
            text,
            textAlign: TextAlign.center,
            style: isBack
                ? Theme.of(context).textTheme.titleMedium
                : Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
          ),
          if (!isBack) ...[const SizedBox(height: 12), Text(l.tapToFlip, style: Theme.of(context).textTheme.bodySmall)],
        ],
      ),
    );
    return Semantics(
      button: true,
      label: flipped ? '${l.cardBack}: $back' : '${l.cardFront}: $front. ${l.tapToFlip}',
      excludeSemantics: true,
      child: GestureDetector(
        onTap: onTap,
        child: TweenAnimationBuilder<double>(
          tween: Tween(end: flipped ? pi : 0),
          duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 400),
          curve: Curves.easeInOut,
          builder: (context, angle, _) {
            final showBack = angle > pi / 2;
            return Transform(
              alignment: Alignment.center,
              transform: Matrix4.identity()
                ..setEntry(3, 2, 0.001)
                ..rotateY(angle),
              child: showBack
                  ? Transform(
                      alignment: Alignment.center,
                      transform: Matrix4.rotationY(pi),
                      child: face(l.cardBack, back, isBack: true),
                    )
                  : face(l.cardFront, front, isBack: false),
            );
          },
        ),
      ),
    );
  }
}
