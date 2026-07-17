using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ForgeMission.Billing;

public static class BillingServiceCollectionExtensions
{
    /// <summary>
    /// Wire the billing bounded context over its own <c>authbilling_db</c> (42.6): a pooled
    /// <see cref="NpgsqlDataSource"/>, the raw-Npgsql ledger + platform-key stores, and the
    /// <see cref="BillingService"/>. Shared verbatim by ForgeUI (room path) and ForgeAPI (hosted
    /// <c>/v1</c>) — one meter, one ledger, keyed by <c>memberId</c>. The caller runs
    /// <see cref="AuthBillingSchema.EnsureCreatedAsync"/> once at startup to bootstrap the schema.
    /// </summary>
    public static IServiceCollection AddAuthBilling(
        this IServiceCollection services, string connectionString, BillingOptions? options = null)
    {
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
        services.AddSingleton<ILedgerStore, NpgsqlLedgerStore>();
        services.AddSingleton<IPlatformKeyStore, NpgsqlPlatformKeyStore>();
        services.AddSingleton(options ?? new BillingOptions());
        services.AddSingleton<BillingService>();
        return services;
    }

    /// <summary>
    /// Wire the request-path platform-key resolver (42.5 ③) — resolves a presented <c>fg_live_…</c>
    /// token to its member + balance behind a short cache. Needs <see cref="AddAuthBilling"/> (for the
    /// key + ledger stores). The HMAC key must match the issuer's <c>PlatformKeys:HmacKey</c>.
    /// </summary>
    public static IServiceCollection AddPlatformKeyResolver(
        this IServiceCollection services, PlatformKeyResolverOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IPlatformKeyResolver, PlatformKeyResolver>();
        return services;
    }
}
