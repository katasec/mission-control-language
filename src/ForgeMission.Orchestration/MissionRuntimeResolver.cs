using Microsoft.Extensions.Configuration;

namespace ForgeMission.Orchestration;

public static class MissionRuntimeResolver
{
    internal const string DefaultCloudEndpoint = "https://api.forge.katasec.com";

    public static async Task<(string BaseUrl, string Mode, IMissionRuntimeLauncher? Launcher)> ResolveAsync(
        IConfiguration configuration, CancellationToken ct = default)
    {
        var mode = ResolveMode(configuration);
        if (mode.Equals("cloud", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = ResolveCloudBaseUrl(
                configuration["MissionRuntime:BaseUrl"],
                Environment.GetEnvironmentVariable("FORGE_API_ENDPOINT"));
            return (baseUrl, mode, null);
        }

        if (!mode.Equals("docker", StringComparison.OrdinalIgnoreCase))
        {
            var configuredUrl = configuration["MissionRuntime:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "MissionRuntime:BaseUrl is required when MissionRuntime:Mode is not 'docker'.");
            return (configuredUrl, mode, null);
        }

        var launcher = await LocalDockerMissionRuntimeLauncher.StartAsync(configuration, ct);
        return (launcher.BaseUrl, mode, launcher);
    }

    internal static string ResolveCloudBaseUrl(string? configuredUrl, string? apiEndpoint)
    {
        if (!string.IsNullOrWhiteSpace(configuredUrl))
            return configuredUrl;

        var endpoint = apiEndpoint?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(endpoint) ? DefaultCloudEndpoint : endpoint;
    }

    internal static string ResolveMode(IConfiguration configuration) =>
        configuration["MissionRuntime:Mode"] ?? "cloud";
}
