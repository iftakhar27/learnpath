using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Data;

/// <summary>
/// Starter flash card decks for four NCERT chapters (original wording, standard textbook facts), added with the sample
/// content so the feature can be tried. Admins edit or replace them in the portal; a subject expert should review them
/// before real students rely on them.
/// </summary>
public static class FlashcardSamples
{
    private static readonly (string ClassLevel, string Subject, string Chapter, (string Front, string Back)[] Cards)[] Decks =
    [
        ("11", NcertSubjects.Physics, "Units and Measurement",
        [
            ("How many SI base units are there?", "Seven: metre, kilogram, second, ampere, kelvin, mole and candela."),
            ("Dimensional formula of force", "[M L T⁻²]"),
            ("Dimensional formula of work or energy", "[M L² T⁻²]"),
            ("Dimensional formula of Planck's constant", "[M L² T⁻¹] (energy × time)"),
            ("Principle of homogeneity of dimensions", "Every term in a correct physical equation has the same dimensions."),
            ("Significant figures in 0.00340", "Three: 3, 4 and the final 0. Leading zeros don't count."),
            ("Relative error in Z = A × B (or A / B)", "ΔZ/Z = ΔA/A + ΔB/B. Relative errors add."),
            ("Relative error in Z = Aⁿ", "ΔZ/Z = n · ΔA/A"),
            ("1 light year in metres", "About 9.46 × 10¹⁵ m"),
            ("1 parsec in metres", "About 3.08 × 10¹⁶ m (about 3.26 light years)"),
            ("What dimensional analysis can't do", "Find dimensionless constants, check equations with trigonometric, exponential or log terms, or tell scalars from vectors."),
        ]),
        ("11", NcertSubjects.Physics, "Laws of Motion",
        [
            ("Newton's first law", "A body stays at rest or in uniform motion in a straight line unless a net external force acts on it (law of inertia)."),
            ("Newton's second law", "Net force equals the rate of change of momentum: F = dp/dt, which is ma for constant mass."),
            ("Newton's third law", "Forces come in equal and opposite pairs that act on different bodies."),
            ("Impulse", "Force × time = change in momentum. SI unit: N s."),
            ("Law of conservation of linear momentum", "With no net external force, a system's total momentum stays constant."),
            ("Static vs kinetic friction", "Limiting static friction is larger (μs > μk): starting a body moving takes more force than keeping it moving."),
            ("Angle of repose θ and friction", "tan θ = μs"),
            ("Maximum safe speed on a level curved road", "v = √(μs r g)"),
            ("Ideal speed on a banked road (no friction)", "v = √(r g tan θ)"),
            ("Apparent weight in a lift with acceleration a", "Going up: m(g + a). Going down: m(g − a). In free fall: zero."),
        ]),
        ("11", NcertSubjects.Chemistry, "Some Basic Concepts of Chemistry",
        [
            ("Avogadro constant", "6.022 × 10²³ mol⁻¹"),
            ("Molarity (M)", "Moles of solute per litre of solution. It changes with temperature."),
            ("Molality (m)", "Moles of solute per kilogram of solvent. It doesn't change with temperature."),
            ("Mole fraction", "Moles of one component ÷ total moles. All mole fractions add up to 1."),
            ("Law of definite proportions", "A compound always has the same elements in the same proportion by mass (Proust)."),
            ("Law of multiple proportions", "When two elements form more than one compound, the masses of one that combine with a fixed mass of the other are in small whole-number ratios (Dalton)."),
            ("Limiting reagent", "The reactant used up first. It decides how much product forms."),
            ("Empirical vs molecular formula", "Empirical: simplest whole-number ratio of atoms. Molecular: the actual numbers, a whole-number multiple of the empirical formula."),
            ("1 atomic mass unit (u)", "1/12 of the mass of one carbon-12 atom, about 1.66 × 10⁻²⁴ g."),
            ("Molar volume of an ideal gas at STP (273.15 K, 1 bar)", "22.7 L mol⁻¹ (22.4 L at 1 atm)"),
        ]),
        ("11", NcertSubjects.Biology, "Cell: The Unit of Life",
        [
            ("Who proposed the cell theory?", "Schleiden (1838) and Schwann (1839). Virchow (1855) added that new cells come from pre-existing cells (omnis cellula e cellula)."),
            ("Fluid mosaic model of the membrane", "Singer and Nicolson (1972): proteins sit in a quasi-fluid lipid bilayer."),
            ("Ribosomes: prokaryotes vs eukaryotes", "Prokaryotes: 70S (50S + 30S). Eukaryotic cytoplasm: 80S (60S + 40S)."),
            ("Which organelle makes most of the cell's ATP?", "The mitochondrion, the site of aerobic respiration."),
            ("Golgi apparatus", "Packages materials for delivery inside or outside the cell; forms glycoproteins and glycolipids."),
            ("Lysosomes", "Membrane-bound vesicles of hydrolytic enzymes that work best at acidic pH. Formed by the Golgi apparatus."),
            ("Arrangement of microtubules in cilia and flagella", "9 + 2: nine peripheral doublets around two central singlets."),
            ("Arrangement of microtubules in a centriole", "Nine evenly spaced peripheral triplets in a cartwheel, with no central microtubules."),
            ("Mesosome", "An infolding of the plasma membrane in bacteria; helps form the cell wall, replicate DNA and in respiration and secretion."),
            ("Smallest cells known", "Mycoplasma (PPLO), about 0.3 µm."),
            ("Function of the nucleolus", "Site of active ribosomal RNA synthesis."),
            ("Leucoplasts and what they store", "Amyloplasts: starch. Elaioplasts: oils and fats. Aleuroplasts: proteins."),
        ]),
    ];

    /// <summary>Adds the starter decks once, while there are no flash cards at all. Returns how many cards were added.</summary>
    public static async Task<int> SeedAsync(AppDbContext db, DateTimeOffset now)
    {
        if (await db.Flashcards.AnyAsync()) return 0;
        var chapters = await db.NcertChapters.AsNoTracking().ToListAsync();
        var added = 0;
        foreach (var (cls, subject, title, cards) in Decks)
        {
            var chapter = chapters.FirstOrDefault(n => n.ClassLevel == cls && n.Subject == subject && n.TitleEn == title);
            if (chapter is null) continue;
            for (var i = 0; i < cards.Length; i++)
            {
                db.Flashcards.Add(new Flashcard
                {
                    NcertChapterId = chapter.Id, FrontEn = cards[i].Front, BackEn = cards[i].Back, SortOrder = i,
                    Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
                });
                added++;
            }
        }
        if (added > 0) await db.SaveChangesAsync();
        return added;
    }
}
