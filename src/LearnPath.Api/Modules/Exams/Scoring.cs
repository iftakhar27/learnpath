using System.Text.Json;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;

namespace LearnPath.Api.Modules.Exams;

public record OptionSnapshot(Guid Id, string TextEn, string? TextHi, bool IsCorrect);

/// <summary>
/// Everything needed to display and re-score one question exactly as it was when the attempt began.
/// Later edits to the question bank never change a stored snapshot.
/// </summary>
public record QuestionSnapshot(
    Guid TestQuestionId, Guid QuestionId, int QuestionVersion,
    Guid? SectionId, string? SectionTitle,
    Guid SubjectId, string SubjectTitle, Guid ChapterId, string ChapterTitle, Guid TopicId, string TopicTitle,
    QuestionType Type, Difficulty Difficulty, string StemEn, string? StemHi, string? ImageUrl,
    List<OptionSnapshot> Options, decimal? NumericAnswer, decimal NumericTolerance,
    decimal Marks, decimal NegativeMarks, bool AllowPartialMarking,
    string? ExplanationEn, string? ExplanationHi);

/// <summary>Student response. Option-based questions use <see cref="SelectedOptionIds"/>; numeric/integer use <see cref="NumericValue"/>.</summary>
public record AnswerResponse(List<Guid>? SelectedOptionIds, decimal? NumericValue)
{
    public bool IsEmpty => (SelectedOptionIds is null || SelectedOptionIds.Count == 0) && NumericValue is null;
}

public record ScoredQuestion(AnswerOutcome Outcome, decimal Marks);

public record ScoreSummary(decimal Score, decimal MaxScore, int Correct, int Incorrect, int Partial, int Skipped);

public static class Scoring
{
    /// <summary>Marks for each correct option chosen under JEE-Advanced-style partial marking.</summary>
    public const decimal PartialMarkPerOption = 1m;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ScoredQuestion Score(QuestionSnapshot q, AnswerResponse? response)
    {
        if (response is null || response.IsEmpty) return new(AnswerOutcome.Skipped, 0);

        switch (q.Type)
        {
            case QuestionType.SingleCorrect:
            case QuestionType.TrueFalse:
            {
                var sel = response.SelectedOptionIds ?? [];
                var correct = sel.Count == 1 && q.Options.Any(o => o.Id == sel[0] && o.IsCorrect);
                return correct ? new(AnswerOutcome.Correct, q.Marks) : new(AnswerOutcome.Incorrect, -q.NegativeMarks);
            }
            case QuestionType.MultipleCorrect:
            {
                var sel = (response.SelectedOptionIds ?? []).ToHashSet();
                var correctSet = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
                if (sel.SetEquals(correctSet)) return new(AnswerOutcome.Correct, q.Marks);
                if (q.AllowPartialMarking && sel.IsSubsetOf(correctSet))
                    return new(AnswerOutcome.PartiallyCorrect, Math.Min(q.Marks, sel.Count * PartialMarkPerOption));
                return new(AnswerOutcome.Incorrect, -q.NegativeMarks);
            }
            case QuestionType.Numerical:
            case QuestionType.Integer:
            {
                if (response.NumericValue is not { } v || q.NumericAnswer is not { } ans) return new(AnswerOutcome.Skipped, 0);
                var tol = q.Type == QuestionType.Integer ? 0 : q.NumericTolerance;
                return Math.Abs(v - ans) <= tol ? new(AnswerOutcome.Correct, q.Marks) : new(AnswerOutcome.Incorrect, -q.NegativeMarks);
            }
            default:
                throw new NotSupportedException($"Question type {q.Type} is not auto-scored.");
        }
    }

    public static ScoreSummary Summarize(IEnumerable<QuestionSnapshot> questions, IReadOnlyDictionary<Guid, AnswerResponse?> responses,
        out List<(QuestionSnapshot Q, ScoredQuestion R)> perQuestion)
    {
        perQuestion = [];
        decimal score = 0, max = 0;
        int c = 0, i = 0, p = 0, s = 0;
        foreach (var q in questions)
        {
            responses.TryGetValue(q.TestQuestionId, out var resp);
            var r = Score(q, resp);
            perQuestion.Add((q, r));
            score += r.Marks;
            max += q.Marks;
            switch (r.Outcome)
            {
                case AnswerOutcome.Correct: c++; break;
                case AnswerOutcome.Incorrect: i++; break;
                case AnswerOutcome.PartiallyCorrect: p++; break;
                default: s++; break;
            }
        }
        return new(score, max, c, i, p, s);
    }

    /// <summary>Rejects responses that do not fit the question shape, so garbage is never stored as an answer.</summary>
    public static void Validate(QuestionSnapshot q, AnswerResponse response)
    {
        if (response.IsEmpty) return;
        switch (q.Type)
        {
            case QuestionType.SingleCorrect or QuestionType.TrueFalse or QuestionType.MultipleCorrect:
                var sel = response.SelectedOptionIds ?? [];
                if (response.NumericValue is not null) throw AppException.BadRequest("invalid_answer", "This question expects option selection.");
                if (sel.Distinct().Count() != sel.Count || sel.Any(id => q.Options.All(o => o.Id != id)))
                    throw AppException.BadRequest("invalid_answer", "Selected option does not belong to this question.");
                if (q.Type != QuestionType.MultipleCorrect && sel.Count > 1)
                    throw AppException.BadRequest("invalid_answer", "Select only one option.");
                break;
            case QuestionType.Numerical or QuestionType.Integer:
                if (response.SelectedOptionIds is { Count: > 0 }) throw AppException.BadRequest("invalid_answer", "This question expects a numeric answer.");
                if (q.Type == QuestionType.Integer && response.NumericValue is { } v && v != decimal.Truncate(v))
                    throw AppException.BadRequest("invalid_answer", "Enter a whole number.");
                break;
        }
    }

    public static AnswerResponse? Parse(string? json) => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AnswerResponse>(json, Json);
    public static string Serialize(AnswerResponse r) => JsonSerializer.Serialize(r, Json);

    /// <summary>Deterministic Fisher–Yates shuffle; the same seed always yields the same order.</summary>
    public static List<T> Shuffle<T>(IEnumerable<T> items, int seed)
    {
        var list = items.ToList();
        var rng = new Random(seed);
        for (var n = list.Count - 1; n > 0; n--)
        {
            var k = rng.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
        return list;
    }
}
