namespace ForgeMission.Core.Manifest;

// Minimal TOML reader for forge.toml — handles only the schema we need:
//   [section] and [section.name] headers
//   key = "string value"
//   key = env("VAR") or env("VAR", "default")
//   key = ["string", "array"]
//   key = 100
//   key = true
// No inline tables or general TOML semantics — TOML is a superset of what we parse.
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
        var artifactInputs = new Dictionary<string, Dictionary<string, TomlValue>>(StringComparer.Ordinal);
        var artifactModes  = new Dictionary<string, Dictionary<string, TomlValue>>(StringComparer.Ordinal);

        // Track current section: "experts", "providers.<name>", "execution",
        // "capabilities.artifacts.inputs.<name>", "capabilities.artifacts.modes.<mode>", or null.
        string? section       = null;
        string? profileName   = null;
        string? capabilityKey = null;
        var     profileRows   = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

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
                else if (header.StartsWith("capabilities.artifacts.inputs.", StringComparison.Ordinal))
                {
                    capabilityKey = header["capabilities.artifacts.inputs.".Length..].Trim();
                    if (capabilityKey.Length == 0)
                        throw new ForgeTomlException($"Line {i + 1}: empty artifact input name", path);
                    artifactInputs[capabilityKey] = new Dictionary<string, TomlValue>(StringComparer.Ordinal);
                    section = "artifactInput";
                    profileName = null;
                }
                else if (header.StartsWith("capabilities.artifacts.modes.", StringComparison.Ordinal))
                {
                    capabilityKey = header["capabilities.artifacts.modes.".Length..].Trim();
                    if (capabilityKey.Length == 0)
                        throw new ForgeTomlException($"Line {i + 1}: empty artifact mode name", path);
                    artifactModes[capabilityKey] = new Dictionary<string, TomlValue>(StringComparer.Ordinal);
                    section = "artifactMode";
                    profileName = null;
                }
                else
                {
                    section       = header;
                    profileName   = null;
                    capabilityKey = null;
                }
                continue;
            }

            // Key = value
            var eq = line.IndexOf('=');
            if (eq <= 0)
                throw new ForgeTomlException($"Line {i + 1}: expected key = value", path);

            var key      = line[..eq].Trim();
            var rawValue = line[(eq + 1)..].Trim();
            if (rawValue.StartsWith('[') && !rawValue.EndsWith(']'))
                rawValue = ReadMultilineArray(rawValue, lines, ref i, path);
            var value = ResolveValue(rawValue, i + 1, path);

            switch (section)
            {
                case "experts":
                    experts[key] = value.AsString(i + 1, path);
                    break;
                case "providers" when profileName is not null:
                    profileRows[profileName][key] = value.AsString(i + 1, path);
                    break;
                case "execution":
                    var knownExecutionKeys = new[] { "backend", "defaultTimeout" };
                    if (!knownExecutionKeys.Contains(key))
                        throw new ForgeTomlException($"[execution] unknown field \"{key}\"", path);
                    executionRows[key] = value.AsString(i + 1, path);
                    break;
                case "artifactInput" when capabilityKey is not null:
                    AddKnownArtifactField(artifactInputs[capabilityKey], key, value, i + 1, path,
                        ["content_types", "max_size_mb"]);
                    break;
                case "artifactMode" when capabilityKey is not null:
                    AddKnownArtifactField(artifactModes[capabilityKey], key, value, i + 1, path,
                        ["output_content_type", "output_extension", "default"]);
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

        var capabilities = new CapabilityConfig
        {
            Artifacts = BuildArtifactCapabilities(artifactInputs, artifactModes, path),
        };

        return new ForgeManifest
        {
            Experts = experts,
            Providers = providers,
            Execution = execution,
            Capabilities = capabilities,
        };
    }

    // Parses "string value", env("VAR") or env("VAR", "default"), strips surrounding quotes.
    private static TomlValue ResolveValue(string raw, int lineNum, string path)
    {
        if (raw.StartsWith("env(", StringComparison.Ordinal))
        {
            if (!raw.EndsWith(')'))
                throw new ForgeTomlException($"Line {lineNum}: malformed env() call", path);

            var inner   = raw[4..^1];
            var parts   = SplitArgs(inner);
            var varName = parts[0].Trim().Trim('"');
            var def     = parts.Length > 1 ? parts[1].Trim().Trim('"') : null;

            return TomlValue.String(Environment.GetEnvironmentVariable(varName) ?? def ?? string.Empty);
        }

        if (raw.StartsWith('"') && raw.EndsWith('"') && raw.Length >= 2)
            return TomlValue.String(raw[1..^1]);

        if (raw.StartsWith('[') && raw.EndsWith(']'))
            return TomlValue.StringArray(ParseStringArray(raw, lineNum, path));

        if (int.TryParse(raw, out var i))
            return TomlValue.Int(i);

        if (raw is "true" or "false")
            return TomlValue.Bool(raw == "true");

        throw new ForgeTomlException(
            $"Line {lineNum}: value must be a quoted string, env() call, string array, integer, or boolean", path);
    }

    private static ArtifactCapabilities BuildArtifactCapabilities(
        Dictionary<string, Dictionary<string, TomlValue>> inputRows,
        Dictionary<string, Dictionary<string, TomlValue>> modeRows,
        string path)
    {
        var inputs = new Dictionary<string, ArtifactInputCapability>(StringComparer.Ordinal);
        foreach (var (name, rows) in inputRows)
        {
            Require(rows, "content_types", $"[capabilities.artifacts.inputs.{name}]", path);
            Require(rows, "max_size_mb", $"[capabilities.artifacts.inputs.{name}]", path);

            inputs[name] = new ArtifactInputCapability
            {
                ContentTypes = rows["content_types"].AsStringArray($"[capabilities.artifacts.inputs.{name}].content_types", path),
                MaxSizeMb = rows["max_size_mb"].AsInt($"[capabilities.artifacts.inputs.{name}].max_size_mb", path),
            };
        }

        var modes = new Dictionary<string, ArtifactModeCapability>(StringComparer.Ordinal);
        foreach (var (name, rows) in modeRows)
        {
            Require(rows, "output_content_type", $"[capabilities.artifacts.modes.{name}]", path);
            Require(rows, "output_extension", $"[capabilities.artifacts.modes.{name}]", path);

            modes[name] = new ArtifactModeCapability
            {
                OutputContentType = rows["output_content_type"].AsString($"[capabilities.artifacts.modes.{name}].output_content_type", path),
                OutputExtension = rows["output_extension"].AsString($"[capabilities.artifacts.modes.{name}].output_extension", path),
                Default = rows.TryGetValue("default", out var d)
                    && d.AsBool($"[capabilities.artifacts.modes.{name}].default", path),
            };
        }

        return new ArtifactCapabilities { Inputs = inputs, Modes = modes };
    }

    private static void AddKnownArtifactField(
        Dictionary<string, TomlValue> rows,
        string key,
        TomlValue value,
        int lineNum,
        string path,
        string[] known)
    {
        if (!known.Contains(key))
            throw new ForgeTomlException($"Line {lineNum}: unknown artifact capability field \"{key}\"", path);
        rows[key] = value;
    }

    private static string ReadMultilineArray(string firstLine, string[] lines, ref int index, string path)
    {
        var parts = new List<string> { firstLine };
        while (++index < lines.Length)
        {
            var line = lines[index].Split('#')[0].Trim();
            parts.Add(line);
            if (line.EndsWith(']')) return string.Join(" ", parts);
        }

        throw new ForgeTomlException("Unterminated array value", path);
    }

    private static string[] ParseStringArray(string raw, int lineNum, string path)
    {
        var inner = raw[1..^1].Trim();
        if (inner.Length == 0) return [];

        var values = SplitArgs(inner)
            .Select(v => v.Trim().TrimEnd(',').Trim())
            .Where(v => v.Length > 0)
            .Select(v =>
            {
                if (!v.StartsWith('"') || !v.EndsWith('"') || v.Length < 2)
                    throw new ForgeTomlException($"Line {lineNum}: array values must be quoted strings", path);
                return v[1..^1];
            })
            .ToArray();
        return values;
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

    private static void Require(Dictionary<string, TomlValue> rows, string key, string context, string path)
    {
        if (!rows.ContainsKey(key))
            throw new ForgeTomlException($"{context} missing required field \"{key}\"", path);
    }

    private readonly record struct TomlValue(
        string? StringValue,
        string[]? StringArrayValue,
        int? IntValue,
        bool? BoolValue)
    {
        public static TomlValue String(string value) => new(value, null, null, null);
        public static TomlValue StringArray(string[] value) => new(null, value, null, null);
        public static TomlValue Int(int value) => new(null, null, value, null);
        public static TomlValue Bool(bool value) => new(null, null, null, value);

        public string AsString(int lineNum, string path) =>
            StringValue ?? throw new ForgeTomlException($"Line {lineNum}: value must be a quoted string or env() call", path);

        public string AsString(string context, string path) =>
            StringValue ?? throw new ForgeTomlException($"{context} must be a quoted string", path);

        public string[] AsStringArray(string context, string path) =>
            StringArrayValue ?? throw new ForgeTomlException($"{context} must be a string array", path);

        public int AsInt(string context, string path) =>
            IntValue ?? throw new ForgeTomlException($"{context} must be an integer", path);

        public bool AsBool(string context, string path) =>
            BoolValue ?? throw new ForgeTomlException($"{context} must be a boolean", path);
    }
}
