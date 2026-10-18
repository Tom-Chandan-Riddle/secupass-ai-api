using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecuPass.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddStaffUserIsActive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "AspNetUsers",
            type: "boolean",
            nullable: false,
            // Set by hand: users who exist before this migration stay active and can still log in.
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsActive",
            table: "AspNetUsers");
    }
}