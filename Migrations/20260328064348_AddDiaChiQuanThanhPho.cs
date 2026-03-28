using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaN5.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaChiQuanThanhPho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.AddColumn<int>(
                name: "MaDiaChi",
                table: "Customers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ThanhPhos",
                columns: table => new
                {
                    MaThanhPho = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenThanhPho = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhPhos", x => x.MaThanhPho);
                });

            migrationBuilder.CreateTable(
                name: "Quans",
                columns: table => new
                {
                    MaQuan = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenQuan = table.Column<string>(type: "TEXT", nullable: false),
                    MaThanhPho = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quans", x => x.MaQuan);
                    table.ForeignKey(
                        name: "FK_Quans_ThanhPhos_MaThanhPho",
                        column: x => x.MaThanhPho,
                        principalTable: "ThanhPhos",
                        principalColumn: "MaThanhPho",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiaChis",
                columns: table => new
                {
                    MaDiaChi = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SoNha = table.Column<string>(type: "TEXT", nullable: true),
                    Duong = table.Column<string>(type: "TEXT", nullable: true),
                    MaQuan = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaChis", x => x.MaDiaChi);
                    table.ForeignKey(
                        name: "FK_DiaChis_Quans_MaQuan",
                        column: x => x.MaQuan,
                        principalTable: "Quans",
                        principalColumn: "MaQuan",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_MaDiaChi",
                table: "Customers",
                column: "MaDiaChi");

            migrationBuilder.CreateIndex(
                name: "IX_DiaChis_MaQuan",
                table: "DiaChis",
                column: "MaQuan");

            migrationBuilder.CreateIndex(
                name: "IX_Quans_MaThanhPho",
                table: "Quans",
                column: "MaThanhPho");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_DiaChis_MaDiaChi",
                table: "Customers",
                column: "MaDiaChi",
                principalTable: "DiaChis",
                principalColumn: "MaDiaChi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_DiaChis_MaDiaChi",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "DiaChis");

            migrationBuilder.DropTable(
                name: "Quans");

            migrationBuilder.DropTable(
                name: "ThanhPhos");

            migrationBuilder.DropIndex(
                name: "IX_Customers_MaDiaChi",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MaDiaChi",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "TEXT",
                nullable: true);
        }
    }
}
