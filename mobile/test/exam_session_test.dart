import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/api_client.dart';
import 'package:learnpath/core/api/models.g.dart';
import 'package:learnpath/features/tests/exam_session.dart';

/// Builds an attempt the way the API returns it.
AttemptDto attemptJson({int remaining = 600, List<Map<String, dynamic>?>? responses}) => AttemptDto.fromJson({
      'attemptId': 'att-1',
      'testId': 't-1',
      'testTitle': 'Laws of Motion',
      'instructions': null,
      'status': 'InProgress',
      'startedAt': '2026-11-04T10:00:00Z',
      'expiresAt': '2026-11-04T10:10:00Z',
      'remainingSeconds': remaining,
      'serverTime': '2026-11-04T10:00:00Z',
      'questions': [
        for (var i = 0; i < 4; i++)
          {
            'number': i + 1,
            'testQuestionId': 'tq-$i',
            'section': null,
            'subject': 'Physics',
            'type': i == 3 ? 'Numerical' : 'SingleCorrect',
            'stem': 'Q$i',
            'imageUrl': null,
            'options': i == 3 ? <Map<String, dynamic>>[] : [{'id': 'o$i-a', 'text': 'A'}, {'id': 'o$i-b', 'text': 'B'}],
            'marks': 4,
            'negativeMarks': 1,
            'response': responses?[i],
            'markedForReview': false,
            'timeSpentSeconds': 0,
          },
      ],
    });

void main() {
  late DateTime now;
  late List<SaveAnswerRequest> saved;
  late Future<void> Function(String, SaveAnswerRequest) save;

  setUp(() {
    now = DateTime.utc(2026, 11, 4, 10);
    saved = [];
    save = (_, body) async => saved.add(body);
  });

  ExamSession session(AttemptDto a) => ExamSession(a, save: (id, b) => save(id, b), clock: () => now);

  test('deadline comes from the server remaining time, not the device clock', () {
    final s = session(attemptJson(remaining: 125));
    expect(s.remainingSeconds, 125);
    now = now.add(const Duration(seconds: 100));
    expect(s.remainingSeconds, 25);
    now = now.add(const Duration(seconds: 30));
    expect(s.remainingSeconds, 0);
    expect(s.timeUp, isTrue);
  });

  test('palette statuses follow visits, answers and review marks', () async {
    final s = session(attemptJson());
    expect(s.questions.map((q) => q.status), [PaletteStatus.notAnswered, PaletteStatus.notVisited, PaletteStatus.notVisited, PaletteStatus.notVisited]);

    s.setResponse(const AnswerResponse(selectedOptionIds: ['o0-a']));
    s.goTo(1);
    s.toggleMark();
    s.goTo(2);
    s.setResponse(const AnswerResponse(selectedOptionIds: ['o2-b']));
    s.toggleMark();
    await s.flush();

    expect(s.questions.map((q) => q.status), [
      PaletteStatus.answered,
      PaletteStatus.marked,
      PaletteStatus.answeredAndMarked,
      PaletteStatus.notVisited,
    ]);
    expect((s.answeredCount, s.markedCount, s.unansweredCount), (2, 2, 2));
  });

  test('changes are saved with the latest response, mark and time spent', () async {
    final s = session(attemptJson());
    now = now.add(const Duration(seconds: 42));
    s.setResponse(const AnswerResponse(selectedOptionIds: ['o0-b']));
    s.goTo(1); // leaving records time on question 1
    await s.flush();

    final last = saved.lastWhere((b) => b.testQuestionId == 'tq-0');
    expect(last.response!.selectedOptionIds, ['o0-b']);
    expect(last.timeSpentSeconds, 42);
  });

  test('clearing a response saves an empty answer', () async {
    final s = session(attemptJson());
    s.setResponse(const AnswerResponse(selectedOptionIds: ['o0-a']));
    s.clearResponse();
    await s.flush();
    expect(saved.last.response, isNull);
    expect(s.state.answered, isFalse);
  });

  test('resumed attempts restore saved answers as visited', () {
    final s = session(attemptJson(responses: [null, {'selectedOptionIds': ['o1-a']}, null, {'numericValue': 9.8}]));
    expect(s.questions[1].status, PaletteStatus.answered);
    expect(s.questions[3].response!.numericValue, 9.8);
    expect(s.answeredCount, 2);
  });

  test('offline saves keep the answer pending and report offline, then succeed later', () async {
    var online = false;
    save = (_, body) async {
      if (!online) throw const ApiException(0, 'network_error', 'offline');
      saved.add(body);
    };
    final s = session(attemptJson());
    s.setResponse(const AnswerResponse(selectedOptionIds: ['o0-a']));
    await s.flush();
    expect(s.sync, SyncState.offline);
    expect(s.hasUnsaved, isTrue);

    online = true;
    await s.flush();
    expect(s.sync, SyncState.idle);
    expect(s.hasUnsaved, isFalse);
    expect(saved.single.response!.selectedOptionIds, ['o0-a']);
    s.dispose();
  });

  test('a closed attempt (409) stops further saves instead of retrying forever', () async {
    save = (_, _) async => throw const ApiException(409, 'attempt_closed', 'closed');
    final s = session(attemptJson());
    s.setResponse(const AnswerResponse(selectedOptionIds: ['o0-a']));
    await s.flush();
    expect(s.hasUnsaved, isFalse);
    expect(s.sync, SyncState.idle);
  });
}
