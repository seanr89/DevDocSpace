using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevDocSpace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminContentEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "ApiSpecs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "ApiSpecs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentUpdatedAt",
                table: "ApiSpecs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContentUpdatedById",
                table: "ApiSpecs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Namespace = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocPages_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecs_ContentUpdatedById",
                table: "ApiSpecs",
                column: "ContentUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocPages_Namespace_Path",
                table: "DocPages",
                columns: new[] { "Namespace", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocPages_UpdatedById",
                table: "DocPages",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiSpecs_Users_ContentUpdatedById",
                table: "ApiSpecs",
                column: "ContentUpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiSpecs_Users_ContentUpdatedById",
                table: "ApiSpecs");

            migrationBuilder.DropTable(
                name: "DocPages");

            migrationBuilder.DropIndex(
                name: "IX_ApiSpecs_ContentUpdatedById",
                table: "ApiSpecs");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "ApiSpecs");

            migrationBuilder.DropColumn(
                name: "ContentUpdatedAt",
                table: "ApiSpecs");

            migrationBuilder.DropColumn(
                name: "ContentUpdatedById",
                table: "ApiSpecs");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "ApiSpecs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
