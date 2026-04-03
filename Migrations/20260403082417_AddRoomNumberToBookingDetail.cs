using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaN5.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomNumberToBookingDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomNumber",
                table: "BookingDetails",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "BookingDetails");
        }
    }
}
