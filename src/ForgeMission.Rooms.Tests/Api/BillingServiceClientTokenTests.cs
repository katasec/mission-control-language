using ForgeMission.Billing;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// M7 idempotency (42.6 task 5a) against real Postgres: a <c>SettleRunAsync</c> retry carrying the
/// same <see cref="ExecuteMission.ClientToken"/> (not literally referenced here — the DTO lives in
/// ForgeMission.Api — but this is the ledger-side half of that contract) must not double-debit.
/// </summary>
public sealed class BillingServiceClientTokenTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private BillingService Billing => fixture.Services.GetRequiredService<BillingService>();
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();

    private async Task<Guid> NewMemberAsync()
    {
        var m = await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human,
            DisplayName = "ClientToken Tester",
            Issuer = "dev",
            Subject = $"sub-{Guid.NewGuid():N}",
        });
        return m.Id;
    }

    private static RunUsage Usage(long inTok = 100, long outTok = 50, double secs = 1.0) =>
        new(InputTokens: inTok, OutputTokens: outTok, ComputeSeconds: secs, Model: "gpt-4o-mini");

    [Fact]
    public async Task Retried_ClientToken_returns_the_prior_debit_without_charging_again()
    {
        var member = await NewMemberAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        var first = await Billing.SettleRunAsync(member, "WebSearch", Usage(), clientToken: token);
        var balanceAfterFirst = await Billing.GetBalanceMicroUsdAsync(member);

        var second = await Billing.SettleRunAsync(member, "WebSearch", Usage(), clientToken: token);
        var balanceAfterSecond = await Billing.GetBalanceMicroUsdAsync(member);

        Assert.True(first > 0);
        Assert.Equal(first, second);
        Assert.Equal(balanceAfterFirst, balanceAfterSecond);
    }

    [Fact]
    public async Task Different_ClientTokens_both_debit()
    {
        var member = await NewMemberAsync();

        var first = await Billing.SettleRunAsync(member, "WebSearch", Usage(), clientToken: $"tok-{Guid.NewGuid():N}");
        var second = await Billing.SettleRunAsync(member, "WebSearch", Usage(), clientToken: $"tok-{Guid.NewGuid():N}");

        Assert.Equal(-(first + second), await Billing.GetBalanceMicroUsdAsync(member));
    }

    [Fact]
    public async Task Null_ClientToken_always_debits_preserving_original_behaviour()
    {
        var member = await NewMemberAsync();

        var first = await Billing.SettleRunAsync(member, "WebSearch", Usage());
        var second = await Billing.SettleRunAsync(member, "WebSearch", Usage());

        Assert.Equal(-(first + second), await Billing.GetBalanceMicroUsdAsync(member));
    }
}
