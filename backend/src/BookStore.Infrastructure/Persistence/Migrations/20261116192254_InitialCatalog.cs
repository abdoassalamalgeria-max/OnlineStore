using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "authors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    isbn = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    sale_price = table.Column<int>(type: "integer", nullable: true),
                    effective_price = table.Column<int>(type: "integer", nullable: false, computedColumnSql: "COALESCE(sale_price, price)", stored: true),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    published_on = table.Column<DateOnly>(type: "date", nullable: true),
                    publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_books", x => x.id);
                    table.CheckConstraint("ck_books_page_count", "page_count > 0");
                    table.CheckConstraint("ck_books_price_positive", "price > 0");
                    table.CheckConstraint("ck_books_sale_price", "sale_price IS NULL OR (sale_price > 0 AND sale_price < price)");
                    table.CheckConstraint("ck_books_stock_non_negative", "stock >= 0");
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wilayas",
                columns: table => new
                {
                    code = table.Column<int>(type: "integer", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipping_fee = table.Column<int>(type: "integer", nullable: false),
                    is_delivery_available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wilayas", x => x.code);
                    table.CheckConstraint("ck_wilayas_shipping_fee", "shipping_fee >= 0");
                });

            migrationBuilder.CreateTable(
                name: "book_authors",
                columns: table => new
                {
                    authors_id = table.Column<Guid>(type: "uuid", nullable: false),
                    books_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_authors", x => new { x.authors_id, x.books_id });
                    table.ForeignKey(
                        name: "fk_book_authors_authors_authors_id",
                        column: x => x.authors_id,
                        principalTable: "authors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_authors_books_books_id",
                        column: x => x.books_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_categories",
                columns: table => new
                {
                    books_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categories_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_categories", x => new { x.books_id, x.categories_id });
                    table.ForeignKey(
                        name: "fk_book_categories_books_books_id",
                        column: x => x.books_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_categories_categories_categories_id",
                        column: x => x.categories_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "wilayas",
                columns: new[] { "code", "is_delivery_available", "name_ar", "name_fr", "shipping_fee" },
                values: new object[,]
                {
                    { 1, true, "أدرار", "Adrar", 600 },
                    { 2, true, "الشلف", "Chlef", 600 },
                    { 3, true, "الأغواط", "Laghouat", 600 },
                    { 4, true, "أم البواقي", "Oum El Bouaghi", 600 },
                    { 5, true, "باتنة", "Batna", 600 },
                    { 6, true, "بجاية", "Béjaïa", 600 },
                    { 7, true, "بسكرة", "Biskra", 600 },
                    { 8, true, "بشار", "Béchar", 600 },
                    { 9, true, "البليدة", "Blida", 600 },
                    { 10, true, "البويرة", "Bouira", 600 },
                    { 11, true, "تمنراست", "Tamanrasset", 600 },
                    { 12, true, "تبسة", "Tébessa", 600 },
                    { 13, true, "تلمسان", "Tlemcen", 600 },
                    { 14, true, "تيارت", "Tiaret", 600 },
                    { 15, true, "تيزي وزو", "Tizi Ouzou", 600 },
                    { 16, true, "الجزائر", "Alger", 600 },
                    { 17, true, "الجلفة", "Djelfa", 600 },
                    { 18, true, "جيجل", "Jijel", 600 },
                    { 19, true, "سطيف", "Sétif", 600 },
                    { 20, true, "سعيدة", "Saïda", 600 },
                    { 21, true, "سكيكدة", "Skikda", 600 },
                    { 22, true, "سيدي بلعباس", "Sidi Bel Abbès", 600 },
                    { 23, true, "عنابة", "Annaba", 600 },
                    { 24, true, "قالمة", "Guelma", 600 },
                    { 25, true, "قسنطينة", "Constantine", 600 },
                    { 26, true, "المدية", "Médéa", 600 },
                    { 27, true, "مستغانم", "Mostaganem", 600 },
                    { 28, true, "المسيلة", "M'Sila", 600 },
                    { 29, true, "معسكر", "Mascara", 600 },
                    { 30, true, "ورقلة", "Ouargla", 600 },
                    { 31, true, "وهران", "Oran", 600 },
                    { 32, true, "البيض", "El Bayadh", 600 },
                    { 33, true, "إليزي", "Illizi", 600 },
                    { 34, true, "برج بوعريريج", "Bordj Bou Arréridj", 600 },
                    { 35, true, "بومرداس", "Boumerdès", 600 },
                    { 36, true, "الطارف", "El Tarf", 600 },
                    { 37, true, "تندوف", "Tindouf", 600 },
                    { 38, true, "تيسمسيلت", "Tissemsilt", 600 },
                    { 39, true, "الوادي", "El Oued", 600 },
                    { 40, true, "خنشلة", "Khenchela", 600 },
                    { 41, true, "سوق أهراس", "Souk Ahras", 600 },
                    { 42, true, "تيبازة", "Tipaza", 600 },
                    { 43, true, "ميلة", "Mila", 600 },
                    { 44, true, "عين الدفلى", "Aïn Defla", 600 },
                    { 45, true, "النعامة", "Naâma", 600 },
                    { 46, true, "عين تموشنت", "Aïn Témouchent", 600 },
                    { 47, true, "غرداية", "Ghardaïa", 600 },
                    { 48, true, "غليزان", "Relizane", 600 },
                    { 49, true, "تيميمون", "Timimoun", 600 },
                    { 50, true, "برج باجي مختار", "Bordj Badji Mokhtar", 600 },
                    { 51, true, "أولاد جلال", "Ouled Djellal", 600 },
                    { 52, true, "بني عباس", "Béni Abbès", 600 },
                    { 53, true, "عين صالح", "In Salah", 600 },
                    { 54, true, "عين قزام", "In Guezzam", 600 },
                    { 55, true, "تقرت", "Touggourt", 600 },
                    { 56, true, "جانت", "Djanet", 600 },
                    { 57, true, "المغير", "El M'Ghair", 600 },
                    { 58, true, "المنيعة", "El Meniaa", 600 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_authors_name_trgm",
                table: "authors",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_authors_slug",
                table: "authors",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_book_authors_books_id",
                table: "book_authors",
                column: "books_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_categories_categories_id",
                table: "book_categories",
                column: "categories_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_effective_price",
                table: "books",
                column: "effective_price");

            migrationBuilder.CreateIndex(
                name: "ix_books_isbn",
                table: "books",
                column: "isbn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_language",
                table: "books",
                column: "language");

            migrationBuilder.CreateIndex(
                name: "ix_books_published_on",
                table: "books",
                column: "published_on");

            migrationBuilder.CreateIndex(
                name: "ix_books_slug",
                table: "books",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_title_trgm",
                table: "books",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_authors");

            migrationBuilder.DropTable(
                name: "book_categories");

            migrationBuilder.DropTable(
                name: "wilayas");

            migrationBuilder.DropTable(
                name: "authors");

            migrationBuilder.DropTable(
                name: "books");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
