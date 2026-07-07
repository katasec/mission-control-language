using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

/// <summary>Reserved-handle set (38.5 task 6) — pure, no DB.</summary>
public sealed class ReservedHandlesTests
{
    [Theory]
    [InlineData("@assistant")]
    [InlineData("@guard")]
    [InlineData("@claude")]
    [InlineData("@openai")]
    [InlineData("@grok")]
    public void Official_handles_are_reserved(string handle) =>
        Assert.True(ReservedHandles.IsReserved(handle));

    [Theory]
    [InlineData("@ASSISTANT")]   // case-insensitive
    [InlineData("guard")]        // leading @ optional
    [InlineData("  @claude  ")]  // trimmed
    public void Reservation_is_case_and_at_tolerant(string handle) =>
        Assert.True(ReservedHandles.IsReserved(handle));

    [Theory]
    [InlineData("@my-reviewer")]
    [InlineData("@legal")]
    [InlineData("")]
    [InlineData(null)]
    public void Unclaimed_handles_are_not_reserved(string? handle) =>
        Assert.False(ReservedHandles.IsReserved(handle));
}

/// <summary>
/// The essential-agent seed (38.5 task 6): it must insert the built-in agents on a fresh DB and
/// <em>rename</em> any pre-existing rows carrying the old "@forge/…" handles — the migration path
/// that runs in every environment.
/// </summary>
public sealed class EssentialAgentSeedTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private IDbContextFactory<RoomsDbContext> Factory =>
        fixture.Services.GetRequiredService<IDbContextFactory<RoomsDbContext>>();

    // One method covers the whole migration story because the class-fixture DB is shared and
    // both concerns touch the same fixed agent ids (splitting them would couple on test order).
    [Fact]
    public async Task Seeds_bare_handles_and_renames_pre_existing_forge_rows()
    {
        // Fresh DB → insert built-ins with bare handles.
        await RoomsSeeder.SeedEssentialAgentsAsync(Factory);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            Assert.Equal("@guard", (await db.Members.FindAsync(RoomsSeeder.HallucinationGuardId))!.DisplayName);
            Assert.Equal("@assistant", (await db.Members.FindAsync(RoomsSeeder.AssistantId))!.DisplayName);
        }

        // Simulate a DB seeded before task 6: same fixed ids, old "@forge/…" handles.
        await using (var db = await Factory.CreateDbContextAsync())
        {
            (await db.Members.FindAsync(RoomsSeeder.HallucinationGuardId))!.DisplayName = "@forge/hallucination-guard";
            (await db.Members.FindAsync(RoomsSeeder.AssistantId))!.DisplayName = "@forge/assistant";
            await db.SaveChangesAsync();
        }

        // Re-seeding self-heals the rename in place — no new rows.
        await RoomsSeeder.SeedEssentialAgentsAsync(Factory);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            Assert.Equal("@guard", (await db.Members.FindAsync(RoomsSeeder.HallucinationGuardId))!.DisplayName);
            Assert.Equal("@assistant", (await db.Members.FindAsync(RoomsSeeder.AssistantId))!.DisplayName);
            Assert.Equal(2, await db.Members.CountAsync(m => m.Kind == MemberKind.Agent));
        }
    }
}
