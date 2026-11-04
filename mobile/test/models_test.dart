import 'package:flutter_test/flutter_test.dart';
import 'package:learnpath/core/api/models.g.dart';

void main() {
  test('nested DTOs parse API JSON with nullable refs, dates, decimals and enums', () {
    final r = ResultDto.fromJson({
      'attemptId': 'a',
      'testId': 't',
      'testTitle': 'Quiz',
      'status': 'Submitted',
      'attemptNumber': 1,
      'score': 3, // decimals arrive as JSON integers when whole
      'maxScore': 12.0,
      'percentage': 25,
      'accuracy': 50.5,
      'correct': 1,
      'incorrect': 1,
      'partial': 0,
      'skipped': 1,
      'timeTakenSeconds': 90,
      'subjects': [
        {'id': 's', 'name': 'Physics', 'total': 3, 'correct': 1, 'incorrect': 1, 'partial': 0, 'skipped': 1, 'score': 3, 'maxScore': 12, 'accuracy': 50},
      ],
      'chapters': <Object>[],
      'difficulty': <Object>[],
      'questions': <Object>[],
      'previousAttempts': [
        {'attemptId': 'p', 'attemptNumber': 0, 'score': 8, 'maxScore': 12, 'submittedAt': '2026-11-01T09:30:00Z'},
      ],
      'percentile': null,
      'recommendations': ['Revise friction'],
    });
    expect(r.score, 3.0);
    expect(r.subjects.single.name, 'Physics');
    expect(r.previousAttempts.single.submittedAt, DateTime.utc(2026, 11, 1, 9, 30));
    expect(r.percentile, isNull);
  });

  test('requests omit null fields and serialize enums by wire name', () {
    expect(const CreatePracticeRequest(mode: 'topic', topicId: 'x').toJson(), {'mode': 'topic', 'topicId': 'x'});
    expect(const BookmarkRequest(entityType: BookmarkType.question, entityId: 'q').toJson(), {'entityType': 'Question', 'entityId': 'q'});
    expect(const UpdateProfileRequest(language: 'hi', completeOnboarding: true).toJson(), {'language': 'hi', 'completeOnboarding': true});
    expect(const AnswerResponse(selectedOptionIds: ['a']).toJson(), {'selectedOptionIds': ['a']});
  });

  test('unknown enum values fail loudly rather than silently mapping', () {
    expect(() => QuestionType.fromJson('Essay'), throwsFormatException);
    expect(QuestionType.fromJson('MultipleCorrect'), QuestionType.multipleCorrect);
  });
}
