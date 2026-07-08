using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// 39.2 ledger store guarantees against real Postgres: balance is SUM(amount_micro_usd), grants
/// credit and debits reduce, and the starting-grant idempotency check works. Also exercises the
/// AddLedger migration (the fixture migrates before these run).
/// </summary>
public sealed class LedgerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();
    private ILedgerStore Ledger => fixture.Services.GetRequiredService<ILedgerStore>();

    private async Task<Guid> NewMemberAsync()
    {
        var m = await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human,
            DisplayName = "Ledger Tester",
            Issuer = "dev",
            Subject = $"sub-{Guid.NewGuid():N}",
        });
        return m.Id;
    }

    [Fact]
    public async Task Balance_is_zero_for_a_member_with_no_entries()
    {
        var member = await NewMemberAsync();
        Assert.Equal(0L, await Ledger.GetBalanceMicroUsdAsync(member));
    }

    [Fact]
    public async Task Grant_then_debits_sum_to_the_balance()
    {
        var member = await NewMemberAsync();

        await Ledger.AppendAsync(new LedgerEntry
        {
            MemberId = member, AmountMicroUsd = 5_000_000, Kind = LedgerEntryKind.Grant,
        });
        Assert.Equal(5_000_000L, await Ledger.GetBalanceMicroUsdAsync(member));

        await Ledger.AppendAsync(new LedgerEntry
        {
            MemberId = member, AmountMicroUsd = -94, Kind = LedgerEntryKind.Debit, MissionRef = "Forge",
            Model = "gpt-4o-mini", InputTokens = 360, OutputTokens = 66, ComputeSeconds = 6.55,
        });
        await Ledger.AppendAsync(new LedgerEntry
        {
            MemberId = member, AmountMicroUsd = -150, Kind = LedgerEntryKind.Debit, MissionRef = "Assistant",
        });

        Assert.Equal(5_000_000L - 94 - 150, await Ledger.GetBalanceMicroUsdAsync(member));
    }

    [Fact]
    public async Task HasEntryOfKind_reflects_a_grant()
    {
        var member = await NewMemberAsync();
        Assert.False(await Ledger.HasEntryOfKindAsync(member, LedgerEntryKind.Grant));

        await Ledger.AppendAsync(new LedgerEntry
        {
            MemberId = member, AmountMicroUsd = 5_000_000, Kind = LedgerEntryKind.Grant,
        });

        Assert.True(await Ledger.HasEntryOfKindAsync(member, LedgerEntryKind.Grant));
        Assert.False(await Ledger.HasEntryOfKindAsync(member, LedgerEntryKind.Topup));
    }

    [Fact]
    public async Task Entries_are_isolated_per_member()
    {
        var a = await NewMemberAsync();
        var b = await NewMemberAsync();

        await Ledger.AppendAsync(new LedgerEntry { MemberId = a, AmountMicroUsd = 1_000, Kind = LedgerEntryKind.Grant });

        Assert.Equal(1_000L, await Ledger.GetBalanceMicroUsdAsync(a));
        Assert.Equal(0L, await Ledger.GetBalanceMicroUsdAsync(b));
    }
}
