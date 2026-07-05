using ForgeMission.Rooms;

namespace ForgeUI.Services;

/// <summary>
/// Dev-stub identity (38.1): the "current user" is whichever seeded human the
/// browser session selects. Scoped per Blazor circuit, so two browser sessions
/// can be two different users. Replaced by real OIDC identity in 38.4.
/// </summary>
public sealed class StubIdentity
{
    public Member? Current { get; set; }
}
