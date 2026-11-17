using BookStore.Domain.Catalog;

namespace BookStore.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo catalog: real titles and authors, but prices, stock, page counts and ISBNs are SAMPLE values
/// (ISBNs are synthetic with valid check digits, and the publishers are placeholder demo imprints).
/// </summary>
internal static class CatalogSeedData
{
    private const string ArPublisher = "منشورات المتجر";
    private const string FrPublisher = "Éditions de la Librairie";
    private const string EnPublisher = "Bookstore Press";

    public static readonly (string Slug, string Ar, string Fr, string En)[] Categories =
    [
        ("novels", "روايات", "Romans", "Novels"),
        ("algerian-literature", "أدب جزائري", "Littérature algérienne", "Algerian Literature"),
        ("classics", "كلاسيكيات", "Classiques", "Classics"),
        ("history", "تاريخ", "Histoire", "History"),
        ("thought", "فكر وفلسفة", "Pensée et philosophie", "Thought & Philosophy"),
    ];

    public static readonly (string Slug, string Name)[] Authors =
    [
        ("ahlam-mosteghanemi", "أحلام مستغانمي"),
        ("tahar-ouettar", "الطاهر وطار"),
        ("abdelhamid-benhedouga", "عبد الحميد بن هدوقة"),
        ("malek-bennabi", "مالك بن نبي"),
        ("ibn-khaldun", "ابن خلدون"),
        ("tayeb-salih", "الطيب صالح"),
        ("taha-hussein", "طه حسين"),
        ("naguib-mahfouz", "نجيب محفوظ"),
        ("khalil-gibran", "جبران خليل جبران"),
        ("waciny-laredj", "واسيني الأعرج"),
        ("ghassan-kanafani", "غسان كنفاني"),
        ("abou-el-kacem-saadallah", "أبو القاسم سعد الله"),
        ("kateb-yacine", "Kateb Yacine"),
        ("mohammed-dib", "Mohammed Dib"),
        ("mouloud-feraoun", "Mouloud Feraoun"),
        ("mouloud-mammeri", "Mouloud Mammeri"),
        ("assia-djebar", "Assia Djebar"),
        ("yasmina-khadra", "Yasmina Khadra"),
        ("kamel-daoud", "Kamel Daoud"),
        ("albert-camus", "Albert Camus"),
        ("george-orwell", "George Orwell"),
        ("yuval-noah-harari", "Yuval Noah Harari"),
        ("ernest-hemingway", "Ernest Hemingway"),
        ("jane-austen", "Jane Austen"),
    ];

    public sealed record BookSeed(
        string Slug,
        string Title,
        string Description,
        string Language,
        int Year,
        int Price,
        int? SalePrice,
        int Stock,
        int PageCount,
        bool Featured,
        string[] Authors,
        string[] Categories);

    public static readonly BookSeed[] Books =
    [
        // ---- Arabic ----
        new("dhakirat-al-jasad", "ذاكرة الجسد", "رواية عن الذاكرة والحب والوطن، تتنقل بين قسنطينة وباريس.", "ar", 1993, 1800, 1500, 25, 404, true, ["ahlam-mosteghanemi"], ["novels", "algerian-literature"]),
        new("fawda-al-hawas", "فوضى الحواس", "الجزء الثاني من الثلاثية، رواية عن الكتابة والحب في جزائر التسعينيات.", "ar", 1997, 1700, null, 18, 380, false, ["ahlam-mosteghanemi"], ["novels", "algerian-literature"]),
        new("al-laz", "اللاز", "رواية عن الثورة التحريرية الجزائرية وأبطالها البسطاء.", "ar", 1974, 1200, null, 12, 290, false, ["tahar-ouettar"], ["novels", "algerian-literature"]),
        new("rih-al-janoub", "ريح الجنوب", "من أوائل الروايات الجزائرية المكتوبة بالعربية، عن الريف الجزائري بعد الاستقلال.", "ar", 1971, 1100, null, 3, 260, false, ["abdelhamid-benhedouga"], ["novels", "algerian-literature", "classics"]),
        new("shurut-al-nahda", "شروط النهضة", "تحليل لشروط النهضة الحضارية ومشكلات المجتمعات الإسلامية.", "ar", 1949, 1300, null, 30, 160, true, ["malek-bennabi"], ["thought", "algerian-literature"]),
        new("mushkilat-al-thaqafa", "مشكلة الثقافة", "دراسة في مفهوم الثقافة ودورها في بناء المجتمع.", "ar", 1959, 1200, 1000, 20, 140, false, ["malek-bennabi"], ["thought", "algerian-literature"]),
        new("al-muqaddima", "المقدمة", "مقدمة كتاب العبر، من أهم مؤلفات علم الاجتماع وفلسفة التاريخ.", "ar", 1377, 3500, 2900, 15, 1100, true, ["ibn-khaldun"], ["history", "thought", "classics"]),
        new("mawsim-al-hijra", "موسم الهجرة إلى الشمال", "رواية عن لقاء الشرق بالغرب وأسئلة الهوية.", "ar", 1966, 1400, null, 22, 170, false, ["tayeb-salih"], ["novels", "classics"]),
        new("al-ayyam", "الأيام", "سيرة ذاتية يروي فيها الكاتب طفولته وسنوات تعلّمه.", "ar", 1929, 1300, null, 0, 300, false, ["taha-hussein"], ["classics"]),
        new("zuqaq-al-midaq", "زقاق المدق", "رواية عن حياة سكان حارة شعبية في القاهرة خلال الحرب العالمية الثانية.", "ar", 1947, 1500, null, 14, 290, false, ["naguib-mahfouz"], ["novels", "classics"]),
        new("bayn-al-qasrayn", "بين القصرين", "الجزء الأول من الثلاثية، عن عائلة قاهرية في مطلع القرن العشرين.", "ar", 1956, 1900, 1600, 9, 520, false, ["naguib-mahfouz"], ["novels", "classics"]),
        new("al-nabi", "النبي", "تأملات شعرية في الحب والعمل والحرية والموت.", "ar", 1923, 900, null, 40, 120, false, ["khalil-gibran"], ["thought", "classics"]),
        new("kitab-al-amir", "كتاب الأمير", "رواية تاريخية عن الأمير عبد القادر ومسالك أبواب الحديد.", "ar", 2005, 2200, null, 16, 560, true, ["waciny-laredj"], ["novels", "history", "algerian-literature"]),
        new("rijal-fi-al-shams", "رجال في الشمس", "رواية قصيرة عن رحلة ثلاثة لاجئين بحثاً عن حياة أفضل.", "ar", 1963, 800, null, 2, 110, false, ["ghassan-kanafani"], ["novels"]),
        new("tarikh-al-jazair-al-thaqafi", "تاريخ الجزائر الثقافي", "موسوعة في التاريخ الثقافي للجزائر عبر القرون.", "ar", 1981, 4500, 3900, 6, 900, false, ["abou-el-kacem-saadallah"], ["history", "algerian-literature"]),

        // ---- French ----
        new("nedjma", "Nedjma", "Roman fondateur de la littérature algérienne autour de quatre jeunes hommes et d'une femme insaisissable.", "fr", 1956, 1600, null, 20, 256, true, ["kateb-yacine"], ["novels", "algerian-literature", "classics"]),
        new("la-grande-maison", "La Grande Maison", "Premier volet de la trilogie Algérie : l'enfance d'Omar à Tlemcen.", "fr", 1952, 1400, null, 11, 190, false, ["mohammed-dib"], ["novels", "algerian-literature"]),
        new("le-fils-du-pauvre", "Le Fils du pauvre", "Récit d'une enfance en Kabylie et du chemin vers l'école.", "fr", 1950, 1300, 1100, 17, 160, false, ["mouloud-feraoun"], ["novels", "algerian-literature"]),
        new("la-colline-oubliee", "La Colline oubliée", "La vie d'un village kabyle à la veille de la Seconde Guerre mondiale.", "fr", 1952, 1400, null, 8, 250, false, ["mouloud-mammeri"], ["novels", "algerian-literature"]),
        new("l-amour-la-fantasia", "L'Amour, la fantasia", "Entre autobiographie et histoire de la conquête de l'Algérie.", "fr", 1985, 1700, null, 10, 300, false, ["assia-djebar"], ["novels", "history", "algerian-literature"]),
        new("ce-que-le-jour-doit-a-la-nuit", "Ce que le jour doit à la nuit", "Une fresque de l'Algérie des années 1930 à l'indépendance.", "fr", 2008, 2100, 1800, 24, 420, true, ["yasmina-khadra"], ["novels", "algerian-literature"]),
        new("meursault-contre-enquete", "Meursault, contre-enquête", "Le frère de « l'Arabe » de L'Étranger prend enfin la parole.", "fr", 2013, 1800, null, 13, 160, false, ["kamel-daoud"], ["novels", "algerian-literature"]),
        new("l-etranger", "L'Étranger", "Meursault, un homme indifférent, à Alger.", "fr", 1942, 1100, null, 35, 180, false, ["albert-camus"], ["novels", "classics"]),
        new("la-peste", "La Peste", "Une épidémie frappe Oran et révèle les hommes.", "fr", 1947, 1300, null, 0, 340, false, ["albert-camus"], ["novels", "classics"]),
        new("les-hirondelles-de-kaboul", "Les Hirondelles de Kaboul", "Deux couples dans Kaboul sous le régime des talibans.", "fr", 2002, 1500, null, 4, 190, false, ["yasmina-khadra"], ["novels"]),

        // ---- English ----
        new("nineteen-eighty-four", "1984", "A dystopian novel about surveillance, truth and totalitarian power.", "en", 1949, 1600, 1350, 28, 328, true, ["george-orwell"], ["novels", "classics"]),
        new("animal-farm", "Animal Farm", "A fable about a revolution on a farm and how power corrupts.", "en", 1945, 1000, null, 21, 112, false, ["george-orwell"], ["novels", "classics"]),
        new("sapiens", "Sapiens: A Brief History of Humankind", "How Homo sapiens came to dominate the planet.", "en", 2014, 3200, null, 7, 464, false, ["yuval-noah-harari"], ["history", "thought"]),
        new("the-old-man-and-the-sea", "The Old Man and the Sea", "An ageing fisherman's long struggle with a giant marlin.", "en", 1952, 1100, null, 19, 128, false, ["ernest-hemingway"], ["novels", "classics"]),
        new("pride-and-prejudice", "Pride and Prejudice", "Elizabeth Bennet and Mr Darcy, manners and marriage in Regency England.", "en", 1813, 1200, 990, 1, 432, false, ["jane-austen"], ["novels", "classics"]),
    ];

    public static string PublisherFor(string language) => language switch
    {
        BookLanguages.Arabic => ArPublisher,
        BookLanguages.French => FrPublisher,
        _ => EnPublisher,
    };

    /// <summary>Deterministic synthetic ISBN-13 (978-0-00000-NNN-C) with a valid check digit.</summary>
    public static string SyntheticIsbn(int index)
    {
        var body = $"978000000{index:D3}";
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        var check = (10 - (sum % 10)) % 10;
        return body + check;
    }
}
