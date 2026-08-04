using Microsoft.Extensions.Configuration;

namespace ForgeMission.Orchestration;

public static class MissionRuntimeResolver
{
    public static async Task<(string BaseUrl, IMissionRuntimeLauncher? Launcher)> ResolveAsync(
        IConfiguration configuration, CancellationToken ct = default)
    {
        var mode = configuration["MissionRuntime:Mode"] ?? "docker";
        if (!mode.Equals("docker", StringComparison.OrdinalIgnoreCase))
        {
            var configuredUrl = configuration["MissionRuntime:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "MissionRuntime:BaseUrl is required when MissionRuntime:Mode is not 'docker'.");
            return (configuredUrl, null);
        }

        var launcher = await LocalDockerMissionRuntimeLauncher.StartAsync(configuration, ct);
        return (launcher.BaseUrl, launcher);
    }
}
