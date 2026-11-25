namespace LearnPath.Api.Domain;

public enum MaterialType { Notes, FormulaSheet, MindMap, PreviousYearPaper, SamplePaper, Other }

/// <summary>
/// A free download: notes, a formula sheet, a mind map, a past or sample paper. Either an uploaded file
/// (<see cref="FileId"/>) or a link to one kept elsewhere (<see cref="ExternalUrl"/>, e.g. Google Drive).
/// </summary>
public class StudyMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionHi { get; set; }
    public MaterialType Type { get; set; }
    /// <summary>Physics, Chemistry, Biology, or null for general material (exam guides, mixed papers).</summary>
    public string? Subject { get; set; }
    /// <summary>"11", "12" or null for both.</summary>
    public string? ClassLevel { get; set; }
    public Guid? FileId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public int SortOrder { get; set; }
    public int Downloads { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>An uploaded file's bytes (kept apart from its listing so lists never load them).</summary>
public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}
