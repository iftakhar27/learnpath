import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../core/api/api_client.dart';
import '../../core/api/models.g.dart';

enum PaletteStatus { notVisited, notAnswered, answered, marked, answeredAndMarked }

enum SyncState { idle, saving, offline }

class QuestionState {
  QuestionState({this.response, this.marked = false, this.visited = false, this.timeSpentSeconds = 0});
  AnswerResponse? response;
  bool marked;
  bool visited;
  int timeSpentSeconds;

  bool get answered => response != null && ((response!.selectedOptionIds?.isNotEmpty ?? false) || response!.numericValue != null);

  PaletteStatus get status => switch ((visited, answered, marked)) {
    (_, true, true) => PaletteStatus.answeredAndMarked,
    (_, false, true) => PaletteStatus.marked,
    (_, true, false) => PaletteStatus.answered,
    (true, false, false) => PaletteStatus.notAnswered,
    _ => PaletteStatus.notVisited,
  };
}

typedef SaveAnswer = Future<void> Function(String attemptId, SaveAnswerRequest body);
typedef Clock = DateTime Function();

/// Client-side state of a test attempt.
///
/// The server is authoritative for timing and scoring: the deadline here is derived from the server's
/// `remainingSeconds`, and every change is saved to the server. Saves that fail because the device is offline are
/// retried with back-off; the attempt can always be resumed, because the server keeps the saved answers.
class ExamSession extends ChangeNotifier {
  ExamSession(this.attempt, {required this.save, Clock? clock})
    : _clock = clock ?? DateTime.now,
      questions = [
        for (final q in attempt.questions)
          QuestionState(
            response: q.response,
            marked: q.markedForReview,
            visited: q.response != null || q.markedForReview,
            timeSpentSeconds: q.timeSpentSeconds,
          ),
      ] {
    deadline = _clock().add(Duration(seconds: attempt.remainingSeconds));
    _enteredAt = _clock();
    questions.first.visited = true;
  }

  final AttemptDto attempt;
  final List<QuestionState> questions;

  /// Persists one answer; throws [ApiException] on failure.
  final SaveAnswer save;
  final Clock _clock;
  late final DateTime deadline;
  late DateTime _enteredAt;

  int current = 0;
  SyncState sync = SyncState.idle;
  final Set<int> _dirty = {};
  Timer? _retry;
  Timer? _debounce;
  int _backoffSeconds = 2;
  bool _disposed = false;

  PlayerQuestionDto get question => attempt.questions[current];
  QuestionState get state => questions[current];
  int get remainingSeconds => deadline.difference(_clock()).inSeconds.clamp(0, 1 << 31);
  bool get timeUp => remainingSeconds <= 0;
  bool get hasUnsaved => _dirty.isNotEmpty;

  int count(bool Function(QuestionState q) test) => questions.where(test).length;
  int get answeredCount => count((q) => q.answered);
  int get markedCount => count((q) => q.marked);
  int get unansweredCount => questions.length - answeredCount;

  void _accrueTime() {
    final now = _clock();
    state.timeSpentSeconds += now.difference(_enteredAt).inSeconds;
    _enteredAt = now;
  }

  void goTo(int index) {
    if (index < 0 || index >= questions.length || index == current) return;
    _accrueTime();
    // Time spent is part of the saved answer, so record it for the question we're leaving.
    if (questions[current].visited) _markDirty(current, immediate: true);
    current = index;
    state.visited = true;
    notifyListeners();
  }

  void setResponse(AnswerResponse? response, {bool debounce = false}) {
    state.response = response;
    _markDirty(current, immediate: !debounce);
    notifyListeners();
  }

  void clearResponse() => setResponse(null);

  void toggleMark() {
    state.marked = !state.marked;
    _markDirty(current, immediate: true);
    notifyListeners();
  }

  void _markDirty(int index, {required bool immediate}) {
    _dirty.add(index);
    _debounce?.cancel();
    if (immediate) {
      unawaited(flush());
    } else {
      _debounce = Timer(const Duration(milliseconds: 700), () => unawaited(flush()));
    }
  }

  Future<void>? _flushing;

  /// Saves every changed question. Concurrent calls share one run; a failed run schedules a retry.
  Future<void> flush() => _flushing ??= _doFlush().whenComplete(() => _flushing = null);

  Future<void> _doFlush() async {
    while (_dirty.isNotEmpty && !_disposed) {
      final index = _dirty.first;
      _dirty.remove(index);
      final q = questions[index];
      final body = SaveAnswerRequest(
        testQuestionId: attempt.questions[index].testQuestionId,
        response: q.answered ? q.response : null,
        markedForReview: q.marked,
        timeSpentSeconds: q.timeSpentSeconds,
      );
      _setSync(SyncState.saving);
      try {
        await save(attempt.attemptId, body);
        _backoffSeconds = 2;
      } on ApiException catch (e) {
        if (e.isNetwork) {
          _dirty.add(index);
          _setSync(SyncState.offline);
          _retry?.cancel();
          _retry = Timer(Duration(seconds: _backoffSeconds), () => unawaited(flush()));
          _backoffSeconds = (_backoffSeconds * 2).clamp(2, 30);
          return;
        }
        // Closed or expired attempts are handled by the submit/result flow; nothing more to save.
        if (e.status == 409) {
          _dirty.clear();
          break;
        }
        rethrow;
      }
    }
    _setSync(SyncState.idle);
  }

  void _setSync(SyncState s) {
    if (sync == s || _disposed) return;
    sync = s;
    notifyListeners();
  }

  /// Records time on the current question and makes a best-effort final save before submitting.
  Future<void> prepareSubmit() async {
    _accrueTime();
    _markDirty(current, immediate: false);
    _debounce?.cancel();
    try {
      await flush().timeout(const Duration(seconds: 8));
    } catch (_) {
      // Submit anyway: the server scores what it already has.
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _retry?.cancel();
    _debounce?.cancel();
    super.dispose();
  }
}
