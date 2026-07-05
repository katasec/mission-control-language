using ForgeMission.Rooms;
using Microsoft.AspNetCore.Components.Authorization;

namespace ForgeUI.Services;

/// <summary>
/// The authenticated member for the current Blazor circuit (38.4) — replaces the 38.1 dev stub.
/// Resolves the principal from the auth state and provisions/looks up the domain Member,
/// cached for the circuit. Null when the visitor is not signed in.
/// </summary>
public sealed class CurrentUser(AuthenticationStateProvider authState, MemberProvisioningService provisioning)
{
    private Member? _cached;

    public async Task<Member?> GetMemberAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
            return _cached;

        var state = await authState.GetAuthenticationStateAsync();
        _cached = await provisioning.ResolveAsync(state.User, ct);
        return _cached;
    }
}
