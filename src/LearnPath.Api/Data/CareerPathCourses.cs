using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Data;

/// <summary>
/// The course line-up from the institute's syllabus brochure: Foundation for Class 8, 9 and 10, NEET and JEE for Class
/// 11 and 12, and the NEET and JEE dropper batches. Each course is added once, with its syllabus (subjects and chapters),
/// monthly fee and description; nothing is touched once it exists, so admins edit, publish or archive them freely.
/// Lessons, notes and tests are added in the admin portal, and a course can't be bought until it has a lesson.
/// </summary>
public static class CareerPathCourses
{
    public const string FoundationExamCode = "FOUNDATION";

    private const long Foundation = 999_00, Senior = 1499_00; // paise, per month
    private const int Month = 30;

    private record Sub(string TitleEn, string TitleHi, Ch[] Chapters);
    private record Ch(string Title, string? Unit = null);
    private record Spec(string Slug, string ExamCode, string? ClassLevel, string TitleEn, string TitleHi, long PriceInPaise, string DescriptionEn, string DescriptionHi, Sub[] Subjects);

    // ---------- Chapter lists ----------

    private static Ch[] Plain(params string[] titles) => titles.Select(t => new Ch(t)).ToArray();
    private static Ch[] Unit(string unit, params string[] titles) => titles.Select(t => new Ch(t, unit)).ToArray();
    private static Ch[] InUnit(string unit, Ch[] chapters) => chapters.Select(c => c with { Unit = unit }).ToArray();

    private static readonly Ch[] Physics11 =
    [
        .. Unit("Physical World and Measurement", "Units and Measurement"),
        .. Unit("Kinematics", "Motion in a Straight Line", "Motion in a Plane"),
        .. Unit("Laws of Motion", "Laws of Motion"),
        .. Unit("Work, Energy and Power", "Work, Energy and Power"),
        .. Unit("Rotational Motion", "System of Particles and Rotational Motion"),
        .. Unit("Gravitation", "Gravitation"),
        .. Unit("Properties of Bulk Matter", "Mechanical Properties of Solids", "Mechanical Properties of Fluids", "Thermal Properties of Matter"),
        .. Unit("Thermodynamics and Kinetic Theory", "Thermodynamics", "Kinetic Theory"),
        .. Unit("Oscillations and Waves", "Oscillations", "Waves"),
    ];

    private static readonly Ch[] Physics12 =
    [
        .. Unit("Electrostatics", "Electric Charges and Fields", "Electrostatic Potential and Capacitance"),
        .. Unit("Current Electricity", "Current Electricity"),
        .. Unit("Magnetism", "Moving Charges and Magnetism", "Magnetism and Matter"),
        .. Unit("Electromagnetic Induction and Alternating Current", "Electromagnetic Induction", "Alternating Current"),
        .. Unit("Electromagnetic Waves", "Electromagnetic Waves"),
        .. Unit("Optics", "Ray Optics and Optical Instruments", "Wave Optics"),
        .. Unit("Dual Nature, Atoms and Nuclei", "Dual Nature of Radiation and Matter", "Atoms", "Nuclei"),
        .. Unit("Electronic Devices", "Semiconductor Electronics: Materials, Devices and Simple Circuits"),
    ];

    private static readonly Ch[] Chemistry11 =
    [
        .. Unit("Physical Chemistry", "Some Basic Concepts of Chemistry", "Structure of Atom"),
        .. Unit("Inorganic Chemistry", "Classification of Elements and Periodicity in Properties", "Chemical Bonding and Molecular Structure"),
        .. Unit("Physical Chemistry", "Thermodynamics", "Equilibrium", "Redox Reactions"),
        .. Unit("Organic Chemistry", "Organic Chemistry: Some Basic Principles and Techniques", "Hydrocarbons"),
    ];

    /// <summary>The NCERT Class 12 chapters, plus Solid State and the p-Block where a course's syllabus lists them.</summary>
    private static Ch[] Chemistry12(bool solidState, bool pBlock) =>
    [
        .. solidState ? Unit("Physical Chemistry", "The Solid State") : [],
        .. Unit("Physical Chemistry", "Solutions", "Electrochemistry", "Chemical Kinetics"),
        .. pBlock ? Unit("Inorganic Chemistry", "The p-Block Elements") : [],
        .. Unit("Inorganic Chemistry", "The d- and f-Block Elements", "Coordination Compounds"),
        .. Unit("Organic Chemistry", "Haloalkanes and Haloarenes", "Alcohols, Phenols and Ethers", "Aldehydes, Ketones and Carboxylic Acids", "Amines", "Biomolecules"),
    ];

    private static readonly Ch[] Biology11 =
    [
        .. Unit("Diversity in the Living World", "The Living World", "Biological Classification", "Plant Kingdom", "Animal Kingdom"),
        .. Unit("Structural Organisation in Plants and Animals", "Morphology of Flowering Plants", "Anatomy of Flowering Plants", "Structural Organisation in Animals"),
        .. Unit("Cell: Structure and Function", "Cell: The Unit of Life", "Biomolecules", "Cell Cycle and Cell Division"),
        .. Unit("Plant Physiology", "Photosynthesis in Higher Plants", "Respiration in Plants", "Plant Growth and Development"),
        .. Unit("Human Physiology", "Breathing and Exchange of Gases", "Body Fluids and Circulation", "Excretory Products and their Elimination",
            "Locomotion and Movement", "Neural Control and Coordination", "Chemical Coordination and Integration"),
    ];

    private static readonly Ch[] Biology12 =
    [
        .. Unit("Reproduction", "Sexual Reproduction in Flowering Plants", "Human Reproduction", "Reproductive Health"),
        .. Unit("Genetics and Evolution", "Principles of Inheritance and Variation", "Molecular Basis of Inheritance", "Evolution"),
        .. Unit("Biology and Human Welfare", "Human Health and Disease", "Microbes in Human Welfare"),
        .. Unit("Biotechnology", "Biotechnology: Principles and Processes", "Biotechnology and its Applications"),
        .. Unit("Ecology and Environment", "Organisms and Populations", "Ecosystem", "Biodiversity and Conservation"),
    ];

    private static readonly Ch[] Maths11 =
    [
        .. Unit("Sets and Functions", "Sets", "Relations and Functions", "Trigonometric Functions"),
        .. Unit("Algebra", "Complex Numbers and Quadratic Equations", "Linear Inequalities", "Permutations and Combinations", "Binomial Theorem", "Sequences and Series"),
        .. Unit("Coordinate Geometry", "Straight Lines", "Conic Sections", "Introduction to Three Dimensional Geometry"),
        .. Unit("Calculus", "Limits and Derivatives"),
        .. Unit("Statistics and Probability", "Statistics", "Probability"),
    ];

    private static readonly Ch[] Maths12 =
    [
        .. Unit("Relations and Functions", "Relations and Functions", "Inverse Trigonometric Functions"),
        .. Unit("Algebra", "Matrices", "Determinants"),
        .. Unit("Calculus", "Continuity and Differentiability", "Application of Derivatives", "Integrals", "Application of Integrals", "Differential Equations"),
        .. Unit("Vectors and Three Dimensional Geometry", "Vector Algebra", "Three Dimensional Geometry"),
        .. Unit("Linear Programming", "Linear Programming"),
        .. Unit("Probability", "Probability"),
    ];

    private const string Phase1 = "Phase 1 · Class 11 revision (months 1–3)", Phase2 = "Phase 2 · Class 12 revision (months 4–6)";
    private static Ch[] Dropper(Ch[] class11, Ch[] class12) => [.. InUnit(Phase1, class11), .. InUnit(Phase2, class12)];
    private static Sub Phase3(string exam) => new("Phase 3 · Full mocks + PYQ marathon (months 7–10)", "चरण 3 · फुल मॉक + PYQ मैराथन (महीने 7–10)",
        Plain("Full Syllabus Mock Tests (daily)", $"{exam} PYQ Marathon (previous year questions)", "Mock Analysis and Weak-Chapter Revision"));

    // ---------- Descriptions ----------

    private const string CommonEn = "In every course: a 15-minute mini test every morning, Formula Wall (every formula in one place), Doubt Room " +
        "(send a photo, get the solution within 2 hours), a monthly parent report (PDF), recorded classes (watch a missed class later) and the Topper Wall.";
    private const string CommonHi = "हर कोर्स में: रोज़ सुबह 15 मिनट का मिनी टेस्ट, फ़ॉर्मूला वॉल (सारे फ़ॉर्मूले एक जगह), डाउट रूम (फ़ोटो भेजें, " +
        "2 घंटे में हल), हर महीने पेरेंट रिपोर्ट (PDF), रिकॉर्डेड क्लासेज़ (छूटी क्लास बाद में देखें) और टॉपर वॉल।";

    private static string DescribeEn(string goal, int months, int tests, string fee, string yearly, string[] lines) =>
        $"Goal: {goal}\n{months} months · {tests}+ tests · {fee}/month (yearly offer: {yearly})\n\nTests and notes\n" +
        string.Join('\n', lines.Select(l => "• " + l)) + "\n\n" + CommonEn;

    private static string DescribeHi(string goal, int months, int tests, string fee, string yearly, string[] lines) =>
        $"लक्ष्य: {goal}\n{months} महीने · {tests}+ टेस्ट · {fee}/महीना (सालाना ऑफ़र: {yearly})\n\nटेस्ट और नोट्स\n" +
        string.Join('\n', lines.Select(l => "• " + l)) + "\n\n" + CommonHi;

    private static readonly Spec[] Courses =
    [
        new("class-8-foundation", FoundationExamCode, "8", "Class 8 Foundation", "कक्षा 8 फाउंडेशन", Foundation,
            DescribeEn("strong basics + Olympiad / ITSE preparation.", 10, 40, "₹999", "₹9,999 — 2 months free",
            [
                "Science: weekly 15-question MCQ test + NCERT notes",
                "Maths: chapter-wise test + formula sheet",
                "English + Reasoning: monthly reasoning test",
                "Daily 10-question habit test",
                "Monthly Olympiad mock test",
                "Parent report every month",
            ]),
            DescribeHi("मज़बूत बुनियाद + ओलंपियाड / ITSE की तैयारी।", 10, 40, "₹999", "₹9,999 — 2 महीने मुफ़्त",
            [
                "विज्ञान: हर हफ़्ते 15 प्रश्नों का MCQ टेस्ट + NCERT नोट्स",
                "गणित: हर अध्याय का टेस्ट + फ़ॉर्मूला शीट",
                "अंग्रेज़ी + रीज़निंग: मासिक रीज़निंग टेस्ट",
                "रोज़ 10 प्रश्नों का हैबिट टेस्ट",
                "हर महीने ओलंपियाड मॉक टेस्ट",
                "हर महीने पेरेंट रिपोर्ट",
            ]),
            [
                new("Science", "विज्ञान", Plain("Crop Production and Management", "Microorganisms: Friend and Foe", "Force and Pressure", "Light", "Sound",
                    "Chemical Effects of Electric Current", "Pollution of Air and Water")),
                new("Maths", "गणित", Plain("Rational Numbers", "Linear Equations in One Variable", "Squares and Square Roots", "Cubes and Cube Roots",
                    "Mensuration", "Data Handling")),
                new("English + Reasoning", "अंग्रेज़ी + रीज़निंग", Plain("Grammar", "Reading Comprehension", "Mental Ability", "Olympiad Reasoning")),
            ]),

        new("class-9-foundation", FoundationExamCode, "9", "Class 9 Foundation", "कक्षा 9 फाउंडेशन", Foundation,
            DescribeEn("NTSE / ITSE base + strong Class 9 results.", 10, 50, "₹999", "₹9,999 — 2 months free",
            [
                "Science: chapter test + diagram practice",
                "Maths: chapter test + previous-year-style questions",
                "Foundation for NEET/JEE: monthly foundation test",
            ]),
            DescribeHi("NTSE / ITSE की बुनियाद + कक्षा 9 में मज़बूत नतीजे।", 10, 50, "₹999", "₹9,999 — 2 महीने मुफ़्त",
            [
                "विज्ञान: अध्याय टेस्ट + डायग्राम अभ्यास",
                "गणित: अध्याय टेस्ट + पिछले वर्षों जैसे प्रश्न",
                "नीट/जेईई फाउंडेशन: मासिक फाउंडेशन टेस्ट",
            ]),
            [
                new("Science", "विज्ञान", Plain("Matter in Our Surroundings", "Atoms and Molecules", "Tissues", "Motion", "Force and Laws of Motion",
                    "Gravitation", "Sound", "Improvement in Food Resources")),
                new("Maths", "गणित", Plain("Number Systems", "Polynomials", "Coordinate Geometry", "Lines and Angles", "Triangles",
                    "Surface Areas and Volumes", "Statistics")),
                new("Foundation for NEET/JEE", "नीट/जेईई फाउंडेशन", Plain("Basic Physics Numericals", "Mole Concept (Introduction)", "Biology Diagrams")),
            ]),

        new("class-10-foundation-board", "CBSE_10", "10", "Class 10 Foundation + Board", "कक्षा 10 फाउंडेशन + बोर्ड", Foundation,
            DescribeEn("90%+ in the boards + NEET/JEE foundation.", 10, 60, "₹999", "₹9,999 — 2 months free",
            [
                "Science: board-pattern + MCQ tests",
                "Maths: mixed board + MCQ tests",
                "Foundation Advance: Sunday foundation mega test",
                "Board special: last 5 years' board papers, answer-writing practice for 5-mark answers, 3 full pre-board mocks",
            ]),
            DescribeHi("बोर्ड में 90%+ और नीट/जेईई की बुनियाद।", 10, 60, "₹999", "₹9,999 — 2 महीने मुफ़्त",
            [
                "विज्ञान: बोर्ड पैटर्न + MCQ टेस्ट",
                "गणित: बोर्ड + MCQ मिक्स्ड टेस्ट",
                "फाउंडेशन एडवांस: रविवार फाउंडेशन मेगा टेस्ट",
                "बोर्ड स्पेशल: पिछले 5 साल के बोर्ड पेपर, 5 अंकों के उत्तर लिखने का अभ्यास, 3 फुल प्री-बोर्ड मॉक",
            ]),
            [
                new("Science", "विज्ञान", Plain("Chemical Reactions and Equations", "Acids, Bases and Salts", "Metals and Non-metals", "Electricity",
                    "Magnetic Effects of Electric Current", "Light: Reflection and Refraction", "Heredity", "Our Environment")),
                new("Maths", "गणित", Plain("Real Numbers", "Polynomials", "Introduction to Trigonometry", "Circles", "Surface Areas and Volumes",
                    "Probability", "Statistics")),
                new("Foundation Advance", "फाउंडेशन एडवांस", Plain("Light Numericals", "Electricity Numericals", "Heredity (Advanced)")),
                new("Board Special", "बोर्ड स्पेशल", Plain("Board Papers: Last 5 Years", "Answer Writing Practice (5-mark answers)", "Pre-Board Full Mocks")),
            ]),

        new("class-11-neet", "NEET", "11", "Class 11 NEET", "कक्षा 11 नीट", Senior,
            DescribeEn("100% NCERT + NEET level.", 12, 80, "₹1,499", "₹14,999",
            [
                "Physics: chapter test + numerical practice",
                "Chemistry: chapter test + NCERT line test",
                "Biology: NCERT line test + diagram test",
                "NEET special: an NCERT line-by-line test after every chapter, 100+ biology diagrams, a 720-mark NEET mock every month",
            ]),
            DescribeHi("100% NCERT + नीट स्तर।", 12, 80, "₹1,499", "₹14,999",
            [
                "भौतिकी: अध्याय टेस्ट + न्यूमेरिकल अभ्यास",
                "रसायन विज्ञान: अध्याय टेस्ट + NCERT लाइन टेस्ट",
                "जीव विज्ञान: NCERT लाइन टेस्ट + डायग्राम टेस्ट",
                "नीट स्पेशल: हर अध्याय के बाद NCERT लाइन-बाय-लाइन टेस्ट, जीव विज्ञान के 100+ डायग्राम, हर महीने 720 अंकों का नीट मॉक",
            ]),
            [new("Physics", "भौतिकी", Physics11), new("Chemistry", "रसायन विज्ञान", Chemistry11), new("Biology", "जीव विज्ञान", Biology11)]),

        new("class-11-jee", "JEE_MAIN", "11", "Class 11 JEE", "कक्षा 11 जेईई", Senior,
            DescribeEn("strong concepts + JEE Main / Advanced level.", 12, 80, "₹1,499", "₹14,999",
            [
                "Physics: JEE-pattern test + numericals",
                "Chemistry: chapter test + PYQ practice",
                "Maths: chapter test + JEE PYQs",
            ]),
            DescribeHi("मज़बूत कॉन्सेप्ट + जेईई मेन / एडवांस्ड स्तर।", 12, 80, "₹1,499", "₹14,999",
            [
                "भौतिकी: जेईई पैटर्न टेस्ट + न्यूमेरिकल",
                "रसायन विज्ञान: अध्याय टेस्ट + PYQ अभ्यास",
                "गणित: अध्याय टेस्ट + जेईई PYQ",
            ]),
            [new("Physics", "भौतिकी", Physics11), new("Chemistry", "रसायन विज्ञान", Chemistry11), new("Maths", "गणित", Maths11)]),

        new("class-12-neet", "NEET", "12", "Class 12 NEET", "कक्षा 12 नीट", Senior,
            DescribeEn("boards + NEET 650+.", 12, 90, "₹1,499", "₹14,999",
            [
                "Physics: chapter tests + full mocks",
                "Chemistry: NCERT + PYQ tests",
                "Biology: NCERT line test + diagrams + PYQs",
                "12th NEET special: full revision of Class 11 + 12 in January–February, a 720-mark full mock every Sunday, a weak-chapter report + personal plan",
            ]),
            DescribeHi("बोर्ड + नीट में 650+।", 12, 90, "₹1,499", "₹14,999",
            [
                "भौतिकी: अध्याय टेस्ट + फुल मॉक",
                "रसायन विज्ञान: NCERT + PYQ टेस्ट",
                "जीव विज्ञान: NCERT लाइन टेस्ट + डायग्राम + PYQ",
                "12वीं नीट स्पेशल: जनवरी–फ़रवरी में कक्षा 11 + 12 का पूरा रिवीज़न, हर रविवार 720 अंकों का फुल मॉक, कमज़ोर अध्यायों की रिपोर्ट + पर्सनल प्लान",
            ]),
            [new("Physics", "भौतिकी", Physics12), new("Chemistry", "रसायन विज्ञान", Chemistry12(solidState: true, pBlock: true)), new("Biology", "जीव विज्ञान", Biology12)]),

        new("class-12-jee", "JEE_MAIN", "12", "Class 12 JEE", "कक्षा 12 जेईई", Senior,
            DescribeEn("boards + 99 percentile in JEE Main.", 12, 90, "₹1,499", "₹14,999",
            [
                "Physics: JEE Main-pattern tests",
                "Chemistry: PYQ + chapter tests (all of Class 12, with Class 11 revision)",
                "Maths: JEE-pattern + PYQ tests",
            ]),
            DescribeHi("बोर्ड + जेईई मेन में 99 पर्सेंटाइल।", 12, 90, "₹1,499", "₹14,999",
            [
                "भौतिकी: जेईई मेन पैटर्न टेस्ट",
                "रसायन विज्ञान: PYQ + अध्याय टेस्ट (पूरी कक्षा 12, साथ में कक्षा 11 का रिवीज़न)",
                "गणित: जेईई पैटर्न + PYQ टेस्ट",
            ]),
            [
                new("Physics", "भौतिकी", Physics12),
                new("Chemistry", "रसायन विज्ञान", [.. Chemistry12(solidState: false, pBlock: true), new("Class 11 Chemistry: Full Revision", "Revision")]),
                new("Maths", "गणित", Maths12),
            ]),

        new("neet-dropper", "NEET", null, "NEET Dropper Batch", "नीट ड्रॉपर बैच", Senior,
            DescribeEn("selection in one year.", 10, 100, "₹1,499", "₹12,999",
            [
                "Phase 1 (3 months): fast revision of all of Class 11, with a daily test + weekly mock",
                "Phase 2 (3 months): fast revision of all of Class 12, with a daily test + weekly mock",
                "Phase 3 (4 months): full-syllabus mocks + PYQ marathon, a full mock every day with analysis",
            ]),
            DescribeHi("एक साल में सिलेक्शन।", 10, 100, "₹1,499", "₹12,999",
            [
                "चरण 1 (3 महीने): पूरी कक्षा 11 का तेज़ रिवीज़न, रोज़ टेस्ट + हर हफ़्ते मॉक",
                "चरण 2 (3 महीने): पूरी कक्षा 12 का तेज़ रिवीज़न, रोज़ टेस्ट + हर हफ़्ते मॉक",
                "चरण 3 (4 महीने): पूरे सिलेबस के मॉक + PYQ मैराथन, रोज़ फुल मॉक और उसका विश्लेषण",
            ]),
            [
                new("Physics", "भौतिकी", Dropper(Physics11, Physics12)),
                new("Chemistry", "रसायन विज्ञान", Dropper(Chemistry11, Chemistry12(solidState: true, pBlock: true))),
                new("Biology", "जीव विज्ञान", Dropper(Biology11, Biology12)),
                Phase3("NEET"),
            ]),

        new("jee-dropper", "JEE_MAIN", null, "JEE Dropper Batch", "जेईई ड्रॉपर बैच", Senior,
            DescribeEn("selection in one year.", 10, 100, "₹1,499", "₹12,999",
            [
                "Phase 1 (3 months): fast revision of all of Class 11, with a daily test + weekly mock",
                "Phase 2 (3 months): fast revision of all of Class 12, with a daily test + weekly mock",
                "Phase 3 (4 months): full-syllabus mocks + PYQ marathon, a full mock every day with analysis",
            ]),
            DescribeHi("एक साल में सिलेक्शन।", 10, 100, "₹1,499", "₹12,999",
            [
                "चरण 1 (3 महीने): पूरी कक्षा 11 का तेज़ रिवीज़न, रोज़ टेस्ट + हर हफ़्ते मॉक",
                "चरण 2 (3 महीने): पूरी कक्षा 12 का तेज़ रिवीज़न, रोज़ टेस्ट + हर हफ़्ते मॉक",
                "चरण 3 (4 महीने): पूरे सिलेबस के मॉक + PYQ मैराथन, रोज़ फुल मॉक और उसका विश्लेषण",
            ]),
            [
                new("Physics", "भौतिकी", Dropper(Physics11, Physics12)),
                new("Chemistry", "रसायन विज्ञान", Dropper(Chemistry11, Chemistry12(solidState: false, pBlock: true))),
                new("Maths", "गणित", Dropper(Maths11, Maths12)),
                Phase3("JEE"),
            ]),
    ];

    public static IReadOnlyList<string> Slugs { get; } = Courses.Select(c => c.Slug).ToList();

    /// <summary>Adds the Foundation exam and every course not already there (matched by slug); returns how many courses were added.</summary>
    public static async Task<int> SeedAsync(AppDbContext db, DateTimeOffset now)
    {
        if (!await db.Exams.AnyAsync(e => e.Code == FoundationExamCode))
        {
            var last = await db.Exams.MaxAsync(e => (int?)e.SortOrder) ?? 0;
            db.Exams.Add(new Exam { Code = FoundationExamCode, NameEn = "Foundation (Olympiad / NTSE)", NameHi = "फाउंडेशन (ओलंपियाड / एनटीएसई)", SortOrder = last + 1 });
            await db.SaveChangesAsync();
        }

        var exams = await db.Exams.ToDictionaryAsync(e => e.Code, e => e.Id);
        // Class 11–12 science chapters link to the NCERT chapter of the same subject and title (only the ones added here).
        var ncert = (await db.NcertChapters.AsNoTracking().ToListAsync())
            .GroupBy(n => (n.Subject, NcertSyllabus.Normalise(n.TitleEn))).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Id);
        Guid? NcertId(string subject, string title) =>
            NcertSyllabus.SubjectOf(subject) is { } book && ncert.TryGetValue((book, NcertSyllabus.Normalise(title)), out var id) ? id : null;
        var existing = (await db.Courses.Where(c => Slugs.Contains(c.Slug)).Select(c => c.Slug).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var spec in Courses.Where(s => !existing.Contains(s.Slug) && exams.ContainsKey(s.ExamCode)))
        {
            var course = new Course
            {
                ExamId = exams[spec.ExamCode], Slug = spec.Slug, TitleEn = spec.TitleEn, TitleHi = spec.TitleHi,
                DescriptionEn = spec.DescriptionEn, DescriptionHi = spec.DescriptionHi, ClassLevel = spec.ClassLevel,
                PriceInPaise = spec.PriceInPaise, AccessDays = Month, Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
            };
            course.Subjects.AddRange(spec.Subjects.Select((s, i) =>
            {
                var subject = new Subject { CourseId = course.Id, TitleEn = s.TitleEn, TitleHi = s.TitleHi, SortOrder = i };
                subject.Chapters.AddRange(s.Chapters.Select((c, j) =>
                    new Chapter { SubjectId = subject.Id, UnitTitleEn = c.Unit, TitleEn = c.Title, SortOrder = j, NcertChapterId = NcertId(s.TitleEn, c.Title) }));
                return subject;
            }));
            db.Courses.Add(course);
            added++;
        }
        if (added > 0) await db.SaveChangesAsync();
        return added;
    }
}
