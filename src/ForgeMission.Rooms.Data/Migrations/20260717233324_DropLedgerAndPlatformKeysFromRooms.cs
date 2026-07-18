using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeMission.Rooms.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLedgerAndPlatformKeysFromRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DISABLED (42.6): these DropTable calls would delete live billing data if this migration
            // ever runs against a populated forge_rooms via the /app/migrate job. The cutover to
            // authbilling_db does NOT require dropping the old tables — ForgeUI reads billing from
            // authbilling_db regardless, so the stale forge_rooms tables are harmless if left in place.
            // Re-enable ONLY as a deliberate, backed-up cleanup — never as an auto-running deploy step.
            // See docs/phases/phase-42.6 → "PROD-CRITICAL follow-up".
            // migrationBuilder.DropTable(name: "ledger_entries");
            // migrationBuilder.DropTable(name: "platform_keys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_micro_usd = table.Column<long>(type: "bigint", nullable: false),
                    compute_seconds = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    input_tokens = table.Column<long>(type: "bigint", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    output_tokens = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_keys",
                columns: table => new
                {
                    key_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    secret_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
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
                name: "ix_ledger_entries_member_id",
                table: "ledger_entries",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_keys_member_id",
                table: "platform_keys",
                column: "member_id");
        }
    }
}
