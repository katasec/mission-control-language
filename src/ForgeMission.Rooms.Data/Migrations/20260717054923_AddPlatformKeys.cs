using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeMission.Rooms.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_keys",
                columns: table => new
                {
                    key_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_keys", x => x.key_id);
                    table.ForeignKey(
                        name: "FK_platform_keys_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_keys_member_id",
                table: "platform_keys",
                column: "member_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_keys");
        }
    }
}
