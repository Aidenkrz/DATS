using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DATS.Web.Migrations
{

    public partial class AddForceLogoutFlag : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForceLogout",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }


        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForceLogout",
                table: "Users");
        }
    }
}
