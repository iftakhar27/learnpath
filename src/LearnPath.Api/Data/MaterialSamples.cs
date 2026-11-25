using System.Text;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Data;

/// <summary>
/// Sample free material added with the sample content: three one-page PDFs (made here, original text, standard textbook
/// facts) and a link to NCERT's official textbook site, so downloads can be tried. Admins replace them in the portal.
/// </summary>
public static class MaterialSamples
{
    private static readonly (string Title, MaterialType Type, string Subject, string Class, string[] Lines)[] Pdfs =
    [
        ("Units and Measurement - formula sheet (Sample)", MaterialType.FormulaSheet, NcertSubjects.Physics, "11",
        [
            "SI base units: metre, kilogram, second, ampere, kelvin, mole, candela.",
            "Force [M L T^-2]    Work, energy [M L^2 T^-2]    Power [M L^2 T^-3]",
            "Pressure [M L^-1 T^-2]    Planck's constant h [M L^2 T^-1]",
            "Gravitational constant G [M^-1 L^3 T^-2]",
            "Z = A + B or A - B:  dZ = dA + dB (absolute errors add)",
            "Z = A x B or A / B:  dZ/Z = dA/A + dB/B (relative errors add)",
            "Z = A^n:  dZ/Z = n (dA/A)",
            "1 light year = 9.46 x 10^15 m    1 parsec = 3.08 x 10^16 m",
            "1 AU = 1.496 x 10^11 m    1 angstrom = 10^-10 m",
            "Significant figures: leading zeros never count;",
            "trailing zeros after a decimal point do (0.00340 has three).",
        ]),
        ("Laws of Motion - quick notes (Sample)", MaterialType.Notes, NcertSubjects.Physics, "11",
        [
            "First law: a body keeps its state of rest or uniform motion",
            "unless a net external force acts on it (inertia).",
            "Second law: F = dp/dt = ma for constant mass. 1 N = 1 kg m/s^2.",
            "Third law: action and reaction are equal, opposite, on different bodies.",
            "Impulse J = F x t = change in momentum (unit N s).",
            "Friction: static f <= mu_s N, kinetic f = mu_k N, and mu_k < mu_s.",
            "Angle of repose: tan(theta) = mu_s.",
            "Level circular road: v_max = sqrt(mu_s r g).",
            "Banked road without friction: v = sqrt(r g tan(theta)).",
            "Lift with acceleration a: up m(g + a), down m(g - a), free fall 0.",
            "No external force: total momentum stays constant.",
        ]),
        ("Cell: The Unit of Life - key points (Sample)", MaterialType.Notes, NcertSubjects.Biology, "11",
        [
            "Cell theory: Schleiden (1838) and Schwann (1839);",
            "Virchow (1855): omnis cellula e cellula.",
            "Ribosomes: prokaryotes 70S (50S + 30S); eukaryotic cytoplasm 80S (60S + 40S).",
            "Fluid mosaic model of the membrane: Singer and Nicolson (1972).",
            "Mitochondria: aerobic respiration and ATP; own circular DNA and 70S ribosomes.",
            "Golgi apparatus: packaging; forms glycoproteins and glycolipids.",
            "Lysosomes: hydrolytic enzymes, active at acidic pH.",
            "Cilia and flagella: 9 + 2 microtubules. Centriole: 9 triplets (cartwheel).",
            "Mesosome: infolding of the bacterial membrane (wall, DNA replication).",
            "Smallest cells: Mycoplasma, about 0.3 micrometre.",
            "Nucleolus: site of active ribosomal RNA synthesis.",
        ]),
    ];

    public static async Task<int> SeedAsync(AppDbContext db, IFileStore files, DateTimeOffset now)
    {
        if (await db.StudyMaterials.AnyAsync()) return 0;
        var sort = 0;
        foreach (var (title, type, subject, cls, lines) in Pdfs)
        {
            var data = OnePagePdf(title.Replace(" (Sample)", ""), lines);
            var name = UploadRules.SafeFileName(null, title, "application/pdf");
            var file = await files.SaveAsync(name, "application/pdf", data, default);
            db.StudyMaterials.Add(new StudyMaterial
            {
                TitleEn = title, Type = type, Subject = subject, ClassLevel = cls, FileId = file.Id, FileName = name,
                ContentType = "application/pdf", SizeBytes = data.LongLength, Status = PublishStatus.Published, SortOrder = sort++,
                CreatedAt = now, UpdatedAt = now,
            });
        }
        db.StudyMaterials.Add(new StudyMaterial
        {
            TitleEn = "NCERT textbooks (official site)", TitleHi = "एनसीईआरटी पाठ्यपुस्तकें (आधिकारिक साइट)",
            DescriptionEn = "Read or download every NCERT textbook, chapter by chapter, free from NCERT.",
            DescriptionHi = "हर एनसीईआरटी पाठ्यपुस्तक अध्याय-दर-अध्याय, एनसीईआरटी से मुफ़्त पढ़ें या डाउनलोड करें।",
            Type = MaterialType.Other, ExternalUrl = "https://ncert.nic.in/textbook.php", Status = PublishStatus.Published, SortOrder = sort,
            CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return Pdfs.Length + 1;
    }

    /// <summary>A minimal one-page A4 PDF: a bold title and lines of text in Helvetica (ASCII only).</summary>
    public static byte[] OnePagePdf(string title, IEnumerable<string> lines)
    {
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var content = new StringBuilder();
        content.Append("BT /F2 18 Tf 50 790 Td (").Append(Esc(title)).Append(") Tj ET\n");
        var y = 750;
        foreach (var line in lines)
        {
            content.Append("BT /F1 12 Tf 50 ").Append(y).Append(" Td (").Append(Esc(line)).Append(") Tj ET\n");
            y -= 22;
        }
        var stream = content.ToString();
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var o in offsets) pdf.Append(o.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
