namespace ForgeMission.Conversations.Contracts;

/// <summary>
/// The fixed, product-level names of missions a Project may submit. This shared contract keeps
/// Client Runtime, Host, and Worker validation aligned without turning missions into configuration.
/// </summary>
public static class ProjectMissionNames
{
    public const string Janus = "Janus";
    public const string Naive = "Naive";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly([Janus, Naive]);

    public static bool IsKnown(string? name) => name is Janus or Naive;
}
