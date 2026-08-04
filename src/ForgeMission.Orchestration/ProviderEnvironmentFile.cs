namespace ForgeMission.Orchestration;

// Provider keys must not depend on the Electron parent environment: Finder and Dock launches do
// not inherit a terminal's pwsh exports. This deliberately small dotenv reader is the single local
// source for values forwarded to the Docker Mission Runtime.
public static class ProviderEnvironmentFile
{
    private static readonly HashSet<string> AllowedVariables =
    [
        "MCL_API_KEY",
        "MCL_MODEL",
        "MCL_PROVIDER",
        "MCL_ENDPOINT",
        "OPENAI_API_KEY",
        "CLAUDE_API_KEY",
        "XAI_API_KEY",
        "GROK_API_KEY",
        "GOOGLE_SEARCH_API_KEY",
    ];

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Forge",
        "provider.env");

    public static string[] Load(string? configuredPath = null)
    {
        var path = Path.GetFullPath(configuredPath ?? DefaultPath);
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"Local Docker Mission Runtime needs {path}. Create it with MCL_API_KEY=<provider key>.");

        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw new InvalidOperationException($"Invalid provider environment entry in {path}: {rawLine}");

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!AllowedVariables.Contains(name))
                throw new InvalidOperationException($"Unsupported provider environment variable '{name}' in {path}.");
            if (value.Length == 0)
                throw new InvalidOperationException($"Provider environment variable '{name}' is empty in {path}.");

            variables[name] = value;
        }

        if (!variables.ContainsKey("MCL_API_KEY"))
            throw new InvalidOperationException($"Local Docker Mission Runtime needs MCL_API_KEY in {path}.");

        return variables.Select(variable => $"{variable.Key}={variable.Value}").ToArray();
    }
}
