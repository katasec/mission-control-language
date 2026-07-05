using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeMission.Rooms.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdentityAndInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "memberships",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "consumer");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "members",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer",
                table: "members",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject",
                table: "members",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "room_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_invites_members_created_by",
                        column: x => x.created_by,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_invites_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_members_issuer_subject",
                table: "members",
                columns: new[] { "issuer", "subject" },
                unique: true,
                filter: "subject IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_room_invites_created_by",
                table: "room_invites",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_room_invites_room_id",
                table: "room_invites",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ux_room_invites_token",
                table: "room_invites",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_invites");

            migrationBuilder.DropIndex(
                name: "ux_members_issuer_subject",
                table: "members");

            migrationBuilder.DropColumn(
                name: "role",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "email",
                table: "members");

            migrationBuilder.DropColumn(
                name: "issuer",
                table: "members");

            migrationBuilder.DropColumn(
                name: "subject",
                table: "members");
        }
    }
}
