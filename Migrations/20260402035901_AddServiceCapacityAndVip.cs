using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaN5.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCapacityAndVip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVip",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCapacity",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVip",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MaxCapacity",
                table: "Services");
        }
    }
}
