using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Admin;
using LearnPath.Api.Modules.Exams;

namespace LearnPath.Tests;

public class ScoringTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();

    private static QuestionSnapshot Q(QuestionType type, bool[] correct, decimal marks = 4, decimal neg = 1, bool partial = false, decimal? numeric = null, decimal tol = 0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, null, null, Guid.NewGuid(), "S", Guid.NewGuid(), "C", Guid.NewGuid(), "T", type, Difficulty.Medium, "stem", null, null,
            new[] { A, B, C, D }.Take(correct.Length).Select((id, i) => new OptionSnapshot(id, $"o{i}", null, correct[i])).ToList(),
            numeric, tol, marks, neg, partial, null, null);

    private static AnswerResponse Sel(params Guid[] ids) => new(ids.ToList(), null);
    private static AnswerResponse Num(decimal v) => new(null, v);

    [Fact]
    public void Single_correct_awards_full_marks() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Correct, 4), Scoring.Score(Q(QuestionType.SingleCorrect, [true, false, false, false]), Sel(A)));

    [Fact]
    public void Single_wrong_applies_negative_marking() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Incorrect, -1), Scoring.Score(Q(QuestionType.SingleCorrect, [true, false, false, false]), Sel(B)));

    [Fact]
    public void Unanswered_is_skipped_with_zero() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Skipped, 0), Scoring.Score(Q(QuestionType.SingleCorrect, [true, false]), null));

    [Fact]
    public void Empty_selection_is_skipped() =>
        Assert.Equal(AnswerOutcome.Skipped, Scoring.Score(Q(QuestionType.SingleCorrect, [true, false]), Sel()).Outcome);

    [Fact]
    public void Multi_exact_match_is_correct() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Correct, 4), Scoring.Score(Q(QuestionType.MultipleCorrect, [true, true, true, false], neg: 2, partial: true), Sel(C, A, B)));

    [Fact]
    public void Multi_partial_awards_one_per_correct_option_when_enabled() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.PartiallyCorrect, 2), Scoring.Score(Q(QuestionType.MultipleCorrect, [true, true, true, false], neg: 2, partial: true), Sel(A, B)));

    [Fact]
    public void Multi_partial_without_partial_marking_is_incorrect() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Incorrect, -2), Scoring.Score(Q(QuestionType.MultipleCorrect, [true, true, false, false], neg: 2), Sel(A)));

    [Fact]
    public void Multi_any_wrong_option_is_incorrect_even_with_partial_marking() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Incorrect, -2), Scoring.Score(Q(QuestionType.MultipleCorrect, [true, true, false, false], neg: 2, partial: true), Sel(A, C)));

    [Theory]
    [InlineData(9.81, true)]
    [InlineData(9.80, true)]
    [InlineData(9.83, false)]
    public void Numerical_respects_tolerance(decimal answer, bool correct) =>
        Assert.Equal(correct ? AnswerOutcome.Correct : AnswerOutcome.Incorrect,
            Scoring.Score(Q(QuestionType.Numerical, [], numeric: 9.81m, tol: 0.01m), Num(answer)).Outcome);

    [Fact]
    public void Integer_ignores_tolerance() =>
        Assert.Equal(AnswerOutcome.Incorrect, Scoring.Score(Q(QuestionType.Integer, [], numeric: 5, tol: 1), Num(6)).Outcome);

    [Fact]
    public void Numerical_with_zero_negative_marks_scores_zero_when_wrong() =>
        Assert.Equal(new ScoredQuestion(AnswerOutcome.Incorrect, 0), Scoring.Score(Q(QuestionType.Numerical, [], neg: 0, numeric: 3), Num(4)));

    [Fact]
    public void Summary_is_deterministic_and_totals_add_up()
    {
        var qs = new[]
        {
            Q(QuestionType.SingleCorrect, [true, false]),               // correct  +4
            Q(QuestionType.SingleCorrect, [true, false]),               // wrong    -1
            Q(QuestionType.Numerical, [], numeric: 2),                  // skipped   0
            Q(QuestionType.MultipleCorrect, [true, true, false], neg: 2, partial: true), // partial +1
        };
        var responses = new Dictionary<Guid, AnswerResponse?>
        {
            [qs[0].TestQuestionId] = Sel(A), [qs[1].TestQuestionId] = Sel(B), [qs[3].TestQuestionId] = Sel(A),
        };
        var s1 = Scoring.Summarize(qs, responses, out _);
        var s2 = Scoring.Summarize(qs, responses, out _);
        Assert.Equal(s1, s2);
        Assert.Equal(new ScoreSummary(4, 16, 1, 1, 1, 1), s1);
    }

    [Fact]
    public void Validate_rejects_foreign_options_and_multiple_selection_on_single()
    {
        var q = Q(QuestionType.SingleCorrect, [true, false]);
        Assert.Throws<AppException>(() => Scoring.Validate(q, Sel(Guid.NewGuid())));
        Assert.Throws<AppException>(() => Scoring.Validate(q, Sel(A, B)));
        Assert.Throws<AppException>(() => Scoring.Validate(q, Num(1)));
        Scoring.Validate(q, Sel(B));
    }

    [Fact]
    public void Validate_rejects_fraction_for_integer_question() =>
        Assert.Throws<AppException>(() => Scoring.Validate(Q(QuestionType.Integer, [], numeric: 3), Num(2.5m)));

    [Fact]
    public void Shuffle_is_stable_for_a_seed_and_is_a_permutation()
    {
        var items = Enumerable.Range(0, 30).ToList();
        var a = Scoring.Shuffle(items, 1234);
        Assert.Equal(a, Scoring.Shuffle(items, 1234));
        Assert.NotEqual(items, a);
        Assert.Equal(items, a.OrderBy(x => x));
    }

    [Fact]
    public void Accuracy_counts_partial_as_half_and_ignores_skipped()
    {
        Assert.Equal(62.5, ResultsService.Accuracy(2, 1, 1));
        Assert.Equal(0, ResultsService.Accuracy(0, 0, 0));
    }

    [Fact]
    public void Csv_parser_handles_quotes_commas_and_newlines()
    {
        var rows = Csv.Parse("a,b,c\r\n\"x, y\",\"he said \"\"hi\"\"\",\"line1\nline2\"\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["x, y", "he said \"hi\"", "line1\nline2"], rows[1]);
    }

    [Theory]
    [InlineData("9876543210", "+919876543210")]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("09876543210", "+919876543210")]
    public void Phone_numbers_normalise_to_e164(string input, string expected) =>
        Assert.Equal(expected, LearnPath.Api.Modules.Identity.AuthController.NormalizePhone(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]
    public void Invalid_phone_numbers_are_rejected(string input) =>
        Assert.Throws<AppException>(() => LearnPath.Api.Modules.Identity.AuthController.NormalizePhone(input));
}
