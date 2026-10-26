using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public record OptionUpsert([Required] string TextEn, string? TextHi, bool IsCorrect);
public record QuestionUpsert(
    string? ExternalRef, [Required] Guid TopicId, [Required] QuestionType Type, Difficulty Difficulty,
    [Required] string StemEn, string? StemHi, string? ImageUrl, string? ExplanationEn, string? ExplanationHi, string? HintEn, string? VideoSolutionId,
    decimal? NumericAnswer, decimal NumericTolerance, [Range(0, 100)] decimal DefaultMarks, [Range(0, 100)] decimal DefaultNegativeMarks,
    bool AllowPartialMarking, int? PreviousYear, List<OptionUpsert>? Options);
public record ImportResultDto(Guid JobId, int TotalRows, int Imported, int Failed, List<ImportErrorDto> Errors);
public record ImportErrorDto(int Row, string Message);

[ApiController]
[Authorize(Policy = Policies.ContentEdit)]
[Route("api/v1/admin/questions")]
public class AdminQuestionsController(AppDbContext db, AuditService audit, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<object>> List([FromQuery] Guid? topicId, [FromQuery] Guid? chapterId, [FromQuery] PublishStatus? status,
        [FromQuery] Difficulty? difficulty, [FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var query = db.Questions.AsNoTracking();
        if (topicId is not null) query = query.Where(x => x.TopicId == topicId);
        if (chapterId is not null) query = query.Where(x => x.Topic!.ChapterId == chapterId);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (difficulty is not null) query = query.Where(x => x.Difficulty == difficulty);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.StemEn, $"%{q}%") || x.ExternalRef == q);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.UpdatedAt).Skip((p - 1) * size).Take(size)
            .Select(x => (object)new { x.Id, x.ExternalRef, x.TopicId, Type = x.Type.ToString(), Difficulty = x.Difficulty.ToString(), x.StemEn, Status = x.Status.ToString(), x.Version, x.UpdatedAt })
            .ToListAsync(ct);
        return new(items, p, size, total);
    }

    [HttpGet("{id:guid}")]
    public async Task<Question> Get(Guid id, CancellationToken ct) =>
        await db.Questions.AsNoTracking().Include(q => q.Options.OrderBy(o => o.SortOrder)).FirstOrDefaultAsync(q => q.Id == id, ct) ?? throw AppException.NotFound("Question");

    [HttpPost]
    public async Task<IdDto> Create(QuestionUpsert req, CancellationToken ct)
    {
        await ValidateAsync(req, null, ct);
        var now = clock.GetUtcNow();
        var q = new Question { CreatedAt = now };
        Apply(q, req, now);
        db.Questions.Add(q);
        audit.Record(User.UserId(), "question.create", "Question", q.Id, new { req.ExternalRef, req.TopicId });
        await db.SaveChangesAsync(ct);
        return new IdDto(q.Id);
    }

    /// <summary>Edits bump the version. Past attempts keep their own snapshot and are unaffected.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IdDto> Update(Guid id, QuestionUpsert req, CancellationToken ct)
    {
        var q = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Question");
        await ValidateAsync(req, id, ct);
        db.QuestionOptions.RemoveRange(q.Options);
        q.Options.Clear();
        Apply(q, req, clock.GetUtcNow());
        // Replacement options carry client-generated keys; EF would otherwise treat them as updates.
        foreach (var o in q.Options) db.Entry(o).State = EntityState.Added;
        q.Version++;
        audit.Record(User.UserId(), "question.update", "Question", id, new { q.Version });
        await db.SaveChangesAsync(ct);
        return new IdDto(id);
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<IdDto> Status(Guid id, StatusChange req, CancellationToken ct)
    {
        var q = await db.Questions.FindAsync([id], ct) ?? throw AppException.NotFound("Question");
        q.Status = req.Status;
        q.UpdatedAt = clock.GetUtcNow();
        audit.Record(User.UserId(), "question.status", "Question", id, req);
        await db.SaveChangesAsync(ct);
        return new IdDto(id);
    }

    /// <summary>
    /// CSV columns: external_ref,topic_id,type,difficulty,stem_en,stem_hi,option_a,option_b,option_c,option_d,correct,explanation_en,marks,negative_marks
    /// <c>correct</c> is a letter list ("A" or "A;C") for option types, or a number for Numerical/Integer.
    /// Valid rows are imported as Draft; invalid rows are reported with row numbers and nothing partial is written for them.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ImportResultDto> Import(IFormFile file, CancellationToken ct)
    {
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var rows = Csv.Parse(await reader.ReadToEndAsync(ct));
        var job = new ContentImportJob { UploadedBy = User.UserId(), FileName = file.FileName, CreatedAt = clock.GetUtcNow() };
        if (rows.Count == 0) throw AppException.BadRequest("empty_file", "The file is empty.");
        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        string[] required = ["external_ref", "topic_id", "type", "stem_en", "correct"];
        var missing = required.Where(r => !header.Contains(r)).ToList();
        if (missing.Count > 0) throw AppException.BadRequest("invalid_header", $"Missing columns: {string.Join(", ", missing)}");

        var topicIds = (await db.Topics.Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var existingRefs = (await db.Questions.Where(q => q.ExternalRef != null).Select(q => q.ExternalRef!).ToListAsync(ct)).ToHashSet();
        var seenRefs = new HashSet<string>();
        var now = clock.GetUtcNow();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            job.TotalRows++;
            string Col(string name) { var idx = header.IndexOf(name); return idx >= 0 && idx < row.Count ? row[idx].Trim() : ""; }
            var rowNo = i + 1;
            try
            {
                var extRef = Col("external_ref");
                if (extRef.Length == 0) throw new FormatException("external_ref is required.");
                if (existingRefs.Contains(extRef) || !seenRefs.Add(extRef)) throw new FormatException($"Duplicate external_ref '{extRef}'.");
                if (!Guid.TryParse(Col("topic_id"), out var topicId) || !topicIds.Contains(topicId)) throw new FormatException("topic_id does not match an existing topic.");
                if (!Enum.TryParse<QuestionType>(Col("type"), true, out var type)) throw new FormatException($"Unknown type '{Col("type")}'.");
                var difficulty = Enum.TryParse<Difficulty>(Col("difficulty"), true, out var d) ? d : Difficulty.Medium;
                var marks = decimal.TryParse(Col("marks"), NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : 4;
                var neg = decimal.TryParse(Col("negative_marks"), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 1;
                var correct = Col("correct");

                var options = new List<OptionUpsert>();
                decimal? numeric = null;
                if (type is QuestionType.Numerical or QuestionType.Integer)
                {
                    if (!decimal.TryParse(correct, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) throw new FormatException("correct must be a number for numeric questions.");
                    numeric = v;
                }
                else
                {
                    var letters = correct.ToUpperInvariant().Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                    var labels = new[] { "a", "b", "c", "d" };
                    foreach (var label in labels)
                    {
                        var text = Col("option_" + label);
                        if (text.Length > 0) options.Add(new OptionUpsert(text, null, letters.Contains(label.ToUpperInvariant())));
                    }
                    if (type == QuestionType.TrueFalse && options.Count == 0)
                        options = [new("True", "सत्य", letters.Contains("A")), new("False", "असत्य", letters.Contains("B"))];
                }
                var req = new QuestionUpsert(extRef, topicId, type, difficulty, Col("stem_en"), NullIfEmpty(Col("stem_hi")), null,
                    NullIfEmpty(Col("explanation_en")), null, null, null, numeric, 0, marks, neg, false, null, options);
                ValidateShape(req);

                var q = new Question { CreatedAt = now };
                Apply(q, req, now);
                db.Questions.Add(q);
                job.ImportedRows++;
            }
            catch (FormatException ex)
            {
                job.Errors.Add(new ContentImportError { RowNumber = rowNo, Message = ex.Message });
            }
            catch (AppException ex)
            {
                job.Errors.Add(new ContentImportError { RowNumber = rowNo, Message = ex.Message });
            }
        }
        job.FailedRows = job.Errors.Count;
        db.ContentImportJobs.Add(job);
        audit.Record(User.UserId(), "question.import", "ContentImportJob", job.Id, new { job.FileName, job.TotalRows, job.ImportedRows, job.FailedRows });
        await db.SaveChangesAsync(ct);
        return new ImportResultDto(job.Id, job.TotalRows, job.ImportedRows, job.FailedRows, job.Errors.Select(e => new ImportErrorDto(e.RowNumber, e.Message)).ToList());
    }

    private async Task ValidateAsync(QuestionUpsert req, Guid? id, CancellationToken ct)
    {
        if (!await db.Topics.AnyAsync(t => t.Id == req.TopicId, ct)) throw AppException.NotFound("Topic");
        if (req.ExternalRef is not null && await db.Questions.AnyAsync(q => q.ExternalRef == req.ExternalRef && q.Id != id, ct))
            throw AppException.Conflict("duplicate_external_ref", $"Question '{req.ExternalRef}' already exists.");
        ValidateShape(req);
    }

    private static void ValidateShape(QuestionUpsert r)
    {
        var opts = r.Options ?? [];
        var correct = opts.Count(o => o.IsCorrect);
        switch (r.Type)
        {
            case QuestionType.SingleCorrect when opts.Count < 2 || correct != 1:
                throw AppException.BadRequest("invalid_question", "Single-correct questions need at least 2 options and exactly 1 correct.");
            case QuestionType.TrueFalse when opts.Count != 2 || correct != 1:
                throw AppException.BadRequest("invalid_question", "True/false questions need exactly 2 options and 1 correct.");
            case QuestionType.MultipleCorrect when opts.Count < 2 || correct < 1:
                throw AppException.BadRequest("invalid_question", "Multiple-correct questions need at least 2 options and 1 or more correct.");
            case QuestionType.Numerical or QuestionType.Integer when r.NumericAnswer is null:
                throw AppException.BadRequest("invalid_question", "Numeric questions need a numeric answer.");
            case QuestionType.Integer when r.NumericAnswer is { } v && v != decimal.Truncate(v):
                throw AppException.BadRequest("invalid_question", "Integer questions need a whole-number answer.");
        }
        if (string.IsNullOrWhiteSpace(r.StemEn)) throw AppException.BadRequest("invalid_question", "Question text is required.");
    }

    private static void Apply(Question q, QuestionUpsert r, DateTimeOffset now)
    {
        (q.ExternalRef, q.TopicId, q.Type, q.Difficulty, q.StemEn, q.StemHi, q.ImageUrl) = (r.ExternalRef, r.TopicId, r.Type, r.Difficulty, r.StemEn, r.StemHi, r.ImageUrl);
        (q.ExplanationEn, q.ExplanationHi, q.HintEn, q.VideoSolutionId) = (r.ExplanationEn, r.ExplanationHi, r.HintEn, r.VideoSolutionId);
        (q.NumericAnswer, q.NumericTolerance, q.DefaultMarks, q.DefaultNegativeMarks, q.AllowPartialMarking, q.PreviousYear, q.UpdatedAt) =
            (r.NumericAnswer, r.NumericTolerance, r.DefaultMarks, r.DefaultNegativeMarks, r.AllowPartialMarking, r.PreviousYear, now);
        var i = 0;
        if (r.Type is not (QuestionType.Numerical or QuestionType.Integer))
            q.Options.AddRange((r.Options ?? []).Select(o => new QuestionOption { QuestionId = q.Id, TextEn = o.TextEn, TextHi = o.TextHi, IsCorrect = o.IsCorrect, SortOrder = i++ }));
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}

/// <summary>Minimal RFC 4180 CSV parser (quoted fields, escaped quotes, embedded newlines).</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        text = text.TrimStart('﻿');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = [];
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
