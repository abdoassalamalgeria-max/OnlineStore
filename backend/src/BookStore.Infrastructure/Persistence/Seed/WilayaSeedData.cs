using BookStore.Domain.Shipping;

namespace BookStore.Infrastructure.Persistence.Seed;

/// <summary>
/// The 58 Algerian wilayas (administrative division in force since 2019), with their official codes.
/// Seeded through the migration (HasData). To adopt a new division, edit this list and add a migration.
/// </summary>
internal static class WilayaSeedData
{
    public static readonly Wilaya[] All =
    [
        new(1, "أدرار", "Adrar"),
        new(2, "الشلف", "Chlef"),
        new(3, "الأغواط", "Laghouat"),
        new(4, "أم البواقي", "Oum El Bouaghi"),
        new(5, "باتنة", "Batna"),
        new(6, "بجاية", "Béjaïa"),
        new(7, "بسكرة", "Biskra"),
        new(8, "بشار", "Béchar"),
        new(9, "البليدة", "Blida"),
        new(10, "البويرة", "Bouira"),
        new(11, "تمنراست", "Tamanrasset"),
        new(12, "تبسة", "Tébessa"),
        new(13, "تلمسان", "Tlemcen"),
        new(14, "تيارت", "Tiaret"),
        new(15, "تيزي وزو", "Tizi Ouzou"),
        new(16, "الجزائر", "Alger"),
        new(17, "الجلفة", "Djelfa"),
        new(18, "جيجل", "Jijel"),
        new(19, "سطيف", "Sétif"),
        new(20, "سعيدة", "Saïda"),
        new(21, "سكيكدة", "Skikda"),
        new(22, "سيدي بلعباس", "Sidi Bel Abbès"),
        new(23, "عنابة", "Annaba"),
        new(24, "قالمة", "Guelma"),
        new(25, "قسنطينة", "Constantine"),
        new(26, "المدية", "Médéa"),
        new(27, "مستغانم", "Mostaganem"),
        new(28, "المسيلة", "M'Sila"),
        new(29, "معسكر", "Mascara"),
        new(30, "ورقلة", "Ouargla"),
        new(31, "وهران", "Oran"),
        new(32, "البيض", "El Bayadh"),
        new(33, "إليزي", "Illizi"),
        new(34, "برج بوعريريج", "Bordj Bou Arréridj"),
        new(35, "بومرداس", "Boumerdès"),
        new(36, "الطارف", "El Tarf"),
        new(37, "تندوف", "Tindouf"),
        new(38, "تيسمسيلت", "Tissemsilt"),
        new(39, "الوادي", "El Oued"),
        new(40, "خنشلة", "Khenchela"),
        new(41, "سوق أهراس", "Souk Ahras"),
        new(42, "تيبازة", "Tipaza"),
        new(43, "ميلة", "Mila"),
        new(44, "عين الدفلى", "Aïn Defla"),
        new(45, "النعامة", "Naâma"),
        new(46, "عين تموشنت", "Aïn Témouchent"),
        new(47, "غرداية", "Ghardaïa"),
        new(48, "غليزان", "Relizane"),
        new(49, "تيميمون", "Timimoun"),
        new(50, "برج باجي مختار", "Bordj Badji Mokhtar"),
        new(51, "أولاد جلال", "Ouled Djellal"),
        new(52, "بني عباس", "Béni Abbès"),
        new(53, "عين صالح", "In Salah"),
        new(54, "عين قزام", "In Guezzam"),
        new(55, "تقرت", "Touggourt"),
        new(56, "جانت", "Djanet"),
        new(57, "المغير", "El M'Ghair"),
        new(58, "المنيعة", "El Meniaa"),
    ];
}
