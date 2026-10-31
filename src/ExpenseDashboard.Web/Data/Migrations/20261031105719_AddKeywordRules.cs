using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseDashboard.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KeywordRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeywordRules_Name",
                table: "KeywordRules",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeywordRules");
        }
    }
}
