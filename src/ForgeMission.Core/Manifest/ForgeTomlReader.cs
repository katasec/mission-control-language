namespace ForgeMission.Core.Manifest;

// Minimal TOML reader for forge.toml — handles only the schema we need:
//   [section] and [section.name] headers
//   key = "string value"
//   key = env("VAR") or env("VAR", "default")
// No arrays, no inline tables, no integers, no booleans — TOML is a superset of what we parse.
public static class ForgeTomlReader
{
    public static readonly string FileName = "forge.toml";

    public static ForgeManifest? TryRead(string missionFilePath)
    {
        var dir      = Path.GetDirectoryName(Path.GetFullPath(missionFilePath))!;
        var tomlPath = Path.Combine(dir, FileName);

        if (!File.Exists(tomlPath))
            return null;

        var lines = File.ReadAllLines(tomlPath);
        return Parse(lines, tomlPath);
    }

    private static ForgeManifest Parse(string[] lines, string path)
    {
        var experts       = new Dictionary<string, string>(StringComparer.Ordinal);
        var providers     = new Dictionary<string, ProviderProfile>(StringComparer.Ordinal);
        var executionRows = new Dictionary<string, string>(StringComparer.Ordinal);

        // Track current section: "experts", "providers.<name>", "execution", or null
        string? section     = null;
        string? profileName = null;
        var     profileRows = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            var raw  = lines[i];
            var line = raw.Split('#')[0].Trim(); // strip inline comments
            if (line.Length == 0) continue;

            // Section header
            if (line.StartsWith('['))
            {
                if (!line.EndsWith(']'))
                    throw new ForgeTomlException($"Line {i + 1}: malformed section header", path);

                var header = line[1..^1].Trim();
                if (header.StartsWith("providers.", StringComparison.Ordinal))
                {
                    profileName = header["providers.".Length..].Trim();
                    if (profileName.Length == 0)
                        throw new ForgeTomlException($"Line {i + 1}: empty provider name", path);
                    profileRows[profileName] = new Dictionary<string, string>(StringComparer.Ordinal);
                    section = "providers";
                }
                else
                {
                    section     = header;
                    profileName = null;
                }
                continue;
            }

            // Key = value
            var eq = line.IndexOf('=');
            if (eq <= 0)
                throw new ForgeTomlException($"Line {i + 1}: expected key = value", path);

            var key   = line[..eq].Trim();
            var value = ResolveValue(line[(eq + 1)..].Trim(), i + 1, path);

            switch (section)
            {
                case "experts":
                    experts[key] = value;
                    break;
                case "providers" when profileName is not null:
                    profileRows[profileName][key] = value;
                    break;
                case "execution":
                    var knownExecutionKeys = new[] { "backend", "defaultTimeout" };
                    if (!knownExecutionKeys.Contains(key))
                        throw new ForgeTomlException($"[execution] unknown field \"{key}\"", path);
                    executionRows[key] = value;
                    break;
                default:
                    // top-level keys — ignore for now (reserved for future use)
                    break;
            }
        }

        // Build ProviderProfile objects from rows
        foreach (var (name, rows) in profileRows)
        {
            AssertField(rows, "provider", $"[providers.{name}]", path);
            AssertField(rows, "model",    $"[providers.{name}]", path);

            var knownProviders = new[] { "openai", "anthropic", "azure", "ollama", "xai" };
            if (!knownProviders.Contains(rows["provider"]))
                throw new ForgeTomlException(
                    $"[providers.{name}] provider \"{rows["provider"]}\" is not recognised. " +
                    $"Known providers: {string.Join(", ", knownProviders)}", path);

            foreach (var k in rows.Keys)
            {
                var known = new[] { "provider", "model", "apiKey", "endpoint" };
                if (!known.Contains(k))
                    throw new ForgeTomlException($"[providers.{name}] unknown field \"{k}\"", path);
            }

            providers[name] = new ProviderProfile
            {
                Provider = rows["provider"],
                Model    = rows["model"],
                ApiKey   = rows.GetValueOrDefault("apiKey"),
                Endpoint = rows.GetValueOrDefault("endpoint"),
            };
        }

        // Validate and build ExecutionConfig
        if (executionRows.TryGetValue("backend", out var backend))
        {
            var knownBackends = new[] { "process" };
            if (!knownBackends.Contains(backend))
                throw new ForgeTomlException(
                    $"[execution] backend \"{backend}\" is not recognised. " +
                    $"Known backends: {string.Join(", ", knownBackends)}", path);
        }

        var execution = new ExecutionConfig
        {
            Backend        = executionRows.GetValueOrDefault("backend",        "process"),
            DefaultTimeout = executionRows.GetValueOrDefault("defaultTimeout", "30s"),
        };

        return new ForgeManifest { Experts = experts, Providers = providers, Execution = execution };
    }

    // Parses "string value", env("VAR") or env("VAR", "default"), strips surrounding quotes.
    private static string ResolveValue(string raw, int lineNum, string path)
    {
        if (raw.StartsWith("env(", StringComparison.Ordinal))
        {
            if (!raw.EndsWith(')'))
                throw new ForgeTomlException($"Line {lineNum}: malformed env() call", path);

            var inner   = raw[4..^1];
            var parts   = SplitArgs(inner);
            var varName = parts[0].Trim().Trim('"');
            var def     = parts.Length > 1 ? parts[1].Trim().Trim('"') : null;

            return Environment.GetEnvironmentVariable(varName) ?? def ?? string.Empty;
        }

        if (raw.StartsWith('"') && raw.EndsWith('"') && raw.Length >= 2)
            return raw[1..^1];

        throw new ForgeTomlException($"Line {lineNum}: value must be a quoted string or env() call", path);
    }

    private static string[] SplitArgs(string s)
    {
        var results = new List<string>();
        var depth   = 0;
        var start   = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '"') depth = 1 - depth;
            if (s[i] == ',' && depth == 0)
            {
                results.Add(s[start..i]);
                start = i + 1;
            }
        }
        results.Add(s[start..]);
        return [.. results];
    }

    private static void AssertField(Dictionary<string, string> rows, string key, string context, string path)
    {
        if (!rows.ContainsKey(key) || rows[key].Length == 0)
            throw new ForgeTomlException($"{context} missing required field \"{key}\"", path);
    }
}
