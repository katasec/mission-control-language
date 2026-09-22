using Katasec.OciClient;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Cli;

/// <summary>
/// Pulls a whole OCI <b>mission</b> (Phase 39.4) into the forge cache — the mission analog of
/// <see cref="OciExpertPuller"/>. A mission is self-contained (mission.mcl + lock + experts/**), so
/// it unpacks to a <em>directory</em> (<see cref="ForgeCache.MissionDir"/>), not a single file.
/// <para>
/// Trust for built-ins (39.4 decision) is <b>digest-pinning</b>: reference a mission by an immutable
/// <c>@sha256:…</c> digest and the registry can only return that exact content — the pull is
/// self-verifying. Cosign signature verification lands in 39.5 for third-party/custom missions.
/// </para>
/// </summary>
public static class OciMissionPuller
{
    /// <summary>
    /// Pull a mission by OCI reference (<c>ghcr.io/katasec/name@sha256:…</c> or <c>name@tag</c>)
    /// into <c>~/.forge/missions/…</c>. Returns the unpacked directory + status (<c>cached</c> or
    /// <c>pulled</c>). Type-checked: throws if the reference is not a Forge mission.
    /// </summary>
    public static async Task<(string Dir, string Status)> PullAsync(
        string ociRef, bool refresh, CancellationToken ct = default)
    {
        var (registry, name, reference) = OciExpertPuller.ParseRef(ociRef);
        var cacheDir = ForgeCache.MissionDir(registry, name, reference);

        // A populated cache dir (mission.mcl present) is a hit — digest refs are immutable, so a
        // cached digest never needs refetching.
        if (!refresh && File.Exists(Path.Combine(cacheDir, "mission.mcl")))
            return (cacheDir, "cached");

        var token = CredentialStore.GetToken(registry);
        using var client = new OciClient(credential: token);

        var bundle = await client.PullMissionAsync(registry, name, reference, ct); // throws if not a mission

        // Unpack fresh: clear any partial dir first so a failed prior pull can't leave a mix.
        if (Directory.Exists(cacheDir))
            Directory.Delete(cacheDir, recursive: true);
        MissionBundle.Unpack(bundle, cacheDir);

        return (cacheDir, "pulled");
    }
}
