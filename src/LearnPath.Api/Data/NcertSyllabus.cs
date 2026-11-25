using System.Text.RegularExpressions;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Data;

/// <summary>The rationalised NCERT chapter list (2023-24 onward) for Class 11 and 12 science: 79 chapters.</summary>
public static partial class NcertSyllabus
{
    public static readonly (string ClassLevel, string Subject, string[] Chapters)[] Books =
    [
        ("11", NcertSubjects.Physics, [
            "Units and Measurement", "Motion in a Straight Line", "Motion in a Plane", "Laws of Motion", "Work, Energy and Power",
            "System of Particles and Rotational Motion", "Gravitation", "Mechanical Properties of Solids", "Mechanical Properties of Fluids",
            "Thermal Properties of Matter", "Thermodynamics", "Kinetic Theory", "Oscillations", "Waves"]),
        ("12", NcertSubjects.Physics, [
            "Electric Charges and Fields", "Electrostatic Potential and Capacitance", "Current Electricity", "Moving Charges and Magnetism",
            "Magnetism and Matter", "Electromagnetic Induction", "Alternating Current", "Electromagnetic Waves", "Ray Optics and Optical Instruments",
            "Wave Optics", "Dual Nature of Radiation and Matter", "Atoms", "Nuclei", "Semiconductor Electronics: Materials, Devices and Simple Circuits"]),
        ("11", NcertSubjects.Chemistry, [
            "Some Basic Concepts of Chemistry", "Structure of Atom", "Classification of Elements and Periodicity in Properties",
            "Chemical Bonding and Molecular Structure", "Thermodynamics", "Equilibrium", "Redox Reactions",
            "Organic Chemistry: Some Basic Principles and Techniques", "Hydrocarbons"]),
        ("12", NcertSubjects.Chemistry, [
            "Solutions", "Electrochemistry", "Chemical Kinetics", "The d- and f-Block Elements", "Coordination Compounds",
            "Haloalkanes and Haloarenes", "Alcohols, Phenols and Ethers", "Aldehydes, Ketones and Carboxylic Acids", "Amines", "Biomolecules"]),
        ("11", NcertSubjects.Biology, [
            "The Living World", "Biological Classification", "Plant Kingdom", "Animal Kingdom", "Morphology of Flowering Plants",
            "Anatomy of Flowering Plants", "Structural Organisation in Animals", "Cell: The Unit of Life", "Biomolecules", "Cell Cycle and Cell Division",
            "Photosynthesis in Higher Plants", "Respiration in Plants", "Plant Growth and Development", "Breathing and Exchange of Gases",
            "Body Fluids and Circulation", "Excretory Products and their Elimination", "Locomotion and Movement", "Neural Control and Coordination",
            "Chemical Coordination and Integration"]),
        ("12", NcertSubjects.Biology, [
            "Sexual Reproduction in Flowering Plants", "Human Reproduction", "Reproductive Health", "Principles of Inheritance and Variation",
            "Molecular Basis of Inheritance", "Evolution", "Human Health and Disease", "Microbes in Human Welfare",
            "Biotechnology: Principles and Processes", "Biotechnology and its Applications", "Organisms and Populations", "Ecosystem",
            "Biodiversity and Conservation"]),
    ];

    /// <summary>Adds the list once, then links existing course chapters whose subject and title match (e.g. "Units and Measurements").</summary>
    public static async Task<int> SeedAsync(AppDbContext db)
    {
        if (await db.NcertChapters.AnyAsync()) return 0;
        foreach (var (cls, subject, chapters) in Books)
            db.NcertChapters.AddRange(chapters.Select((title, i) => new NcertChapter { ClassLevel = cls, Subject = subject, Number = i + 1, TitleEn = title }));
        await db.SaveChangesAsync();
        return await LinkUnmappedAsync(db);
    }

    /// <summary>Links course chapters without an NCERT chapter to the one with the same subject and (normalised) title.</summary>
    public static async Task<int> LinkUnmappedAsync(AppDbContext db)
    {
        var ncert = await db.NcertChapters.ToListAsync();
        var byKey = ncert.GroupBy(n => (n.Subject, Normalise(n.TitleEn))).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Id);
        var chapters = await db.Chapters.Include(c => c.Subject).Where(c => c.NcertChapterId == null).ToListAsync();
        var linked = 0;
        foreach (var c in chapters)
        {
            if (SubjectOf(c.Subject?.TitleEn) is not { } subject) continue;
            if (byKey.TryGetValue((subject, Normalise(c.TitleEn)), out var id)) { c.NcertChapterId = id; linked++; }
        }
        if (linked > 0) await db.SaveChangesAsync();
        return linked;
    }

    /// <summary>The NCERT chapter a new course chapter matches by subject and title, if exactly one does.</summary>
    public static async Task<Guid?> MatchAsync(AppDbContext db, string? courseSubject, string chapterTitle, CancellationToken ct)
    {
        if (SubjectOf(courseSubject) is not { } subject) return null;
        var key = Normalise(chapterTitle);
        var matches = (await db.NcertChapters.AsNoTracking().Where(n => n.Subject == subject).ToListAsync(ct)).Where(n => Normalise(n.TitleEn) == key).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    /// <summary>The NCERT subject a course subject belongs to (Botany and Zoology count as Biology).</summary>
    public static string? SubjectOf(string? courseSubject)
    {
        var s = (courseSubject ?? "").ToLowerInvariant();
        return s.Contains("physic") ? NcertSubjects.Physics
            : s.Contains("chemi") ? NcertSubjects.Chemistry
            : s.Contains("bio") || s.Contains("botany") || s.Contains("zoology") ? NcertSubjects.Biology
            : null;
    }

    /// <summary>Lower case, "&amp;" as "and", punctuation dropped, plural "s" ignored: "Laws of Motion" ~ "law of motion".</summary>
    public static string Normalise(string title)
    {
        var words = NonWord().Split(title.ToLowerInvariant().Replace("&", " and ")).Where(w => w.Length > 0)
            .Select(w => w.Length > 3 && w.EndsWith('s') ? w[..^1] : w);
        return string.Join(' ', words);
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonWord();
}
