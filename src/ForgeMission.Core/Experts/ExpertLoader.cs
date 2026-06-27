using System.Diagnostics.CodeAnalysis;
using ForgeMission.Parser;
using ForgeMission.Core.Resolution;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Experts;

public class ExpertLoader(string expertsDirectory)
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExpertFrontmatter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<string>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Dictionary<string, string>))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Type preserved via DynamicDependency")]
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Load experts from a resolved lock file catalog.</summary>
    public static Dictionary<string, ExpertDefinition> LoadFromLockFile(LockFile lockFile, string lockFileDirectory)
    {
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal);
        foreach (var (name, entry) in lockFile.Experts)
        {
            var path = entry.Path.StartsWith("~/")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), entry.Path[2..].Replace('/', Path.DirectorySeparatorChar))
                : Path.IsPathRooted(entry.Path)
                ? entry.Path
                : Path.GetFullPath(Path.Combine(lockFileDirectory, entry.Path));
            experts[name] = ParseFile(path);
        }
        return experts;
    }

    /// <summary>Load experts from a directory, collecting all errors before surfacing them.</summary>
    public Dictionary<string, ExpertDefinition> LoadAll()
    {
        if (!Directory.Exists(expertsDirectory))
            throw new ExpertLoadException($"Experts directory not found: {expertsDirectory}");

        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal);
        var errors  = new List<ExpertLoadException>();

        void TryLoad(string mdPath)
        {
            try
            {
                var expert = ParseFile(mdPath);
                if (!experts.ContainsKey(expert.Name))
                    experts[expert.Name] = expert;
            }
            catch (AggregateExpertLoadException ex) { errors.AddRange(ex.Errors); }
            catch (ExpertLoadException ex)           { errors.Add(ex); }
        }

        // Directory-per-expert: experts/Name/expert.md
        foreach (var dir in Directory.GetDirectories(expertsDirectory))
        {
            var expertMd = Path.Combine(dir, "expert.md");
            if (File.Exists(expertMd)) TryLoad(expertMd);
        }

        // Flat fallback: experts/Name.md
        foreach (var file in Directory.GetFiles(expertsDirectory, "*.md"))
            TryLoad(file);

        if (errors.Count > 0)
            throw new AggregateExpertLoadException(errors);

        return experts;
    }

    public static void Validate(Program ast, Dictionary<string, ExpertDefinition> experts,
        TextWriter? warnings = null)
    {
        var missionNames = ast.Declarations
            .OfType<MissionDeclaration>()
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missionParams = ast.Declarations
            .OfType<MissionDeclaration>()
            .SelectMany(m => m.Params)
            .ToHashSet(StringComparer.Ordinal);

        var allSteps = ast.Declarations
            .OfType<MissionDeclaration>()
            .SelectMany(m => GetStepNames(m.Pipeline))
            .Distinct(StringComparer.Ordinal);

        var missing = allSteps
            .Where(step => !experts.ContainsKey(step)
                        && !missionNames.Contains(step)
                        && !missionParams.Contains(step))
            .OrderBy(s => s)
            .ToList();

        if (missing.Count > 0)
            throw new ExpertLoadException(
                $"Missing expert definitions for: {string.Join(", ", missing)}. " +
                "Each expert must have a matching markdown file in the experts directory.");

        if (warnings is not null)
        {
            foreach (var mission in ast.Declarations.OfType<MissionDeclaration>())
                ValidateContextKeys(mission, experts, warnings);
        }
    }

    // Walk the pipeline accumulating declared outputKeys; warn when a step's inputKeys
    // reference a key that no upstream step has declared, or with a mismatched type.
    private static void ValidateContextKeys(
        MissionDeclaration mission,
        Dictionary<string, ExpertDefinition> experts,
        TextWriter warnings)
    {
        // Seed with the standard runtime keys every pipeline starts with.
        var available = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"]    = "string",
            ["feedback"]  = "string",
            ["max_loops"] = "int",
        };

        foreach (var element in mission.Pipeline.Elements)
        {
            var steps = element switch
            {
                StepElement se     => (IEnumerable<Step>)[se.Step],
                ParallelElement pe => pe.Steps,
                _                  => Enumerable.Empty<Step>()
            };

            foreach (var step in steps)
            {
                if (!experts.TryGetValue(step.ExpertName, out var expert)) continue;

                // Check inputKeys against what's available upstream.
                if (expert.InputKeys is { } inputKeys)
                {
                    foreach (var (key, declaredType) in inputKeys)
                    {
                        if (!available.TryGetValue(key, out var upstreamType))
                        {
                            warnings.WriteLine(
                                $"warning MCL011: expert '{step.ExpertName}' in mission '{mission.Name}' " +
                                $"declares inputKey '{key}: {declaredType}' but no upstream expert declares this outputKey.");
                        }
                        else if (!string.Equals(upstreamType, declaredType, StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.WriteLine(
                                $"warning MCL012: expert '{step.ExpertName}' in mission '{mission.Name}' " +
                                $"expects '{key}: {declaredType}' but upstream declares '{key}: {upstreamType}'.");
                        }
                    }
                }

                // Accumulate this step's outputKeys for downstream steps.
                if (expert.OutputKeys is { } outputKeys)
                {
                    foreach (var (key, type) in outputKeys)
                        available[key] = type;
                }
            }
        }
    }

    private static IEnumerable<string> GetStepNames(Pipeline pipeline)
        => pipeline.Elements.SelectMany(e => e switch
        {
            StepElement se     => (IEnumerable<string>)[se.Step.ExpertName],
            ParallelElement pe => pe.Steps.Select(s => s.ExpertName),
            _                  => Enumerable.Empty<string>()
        });

    internal static ExpertDefinition ParseFile(string path)
    {
        var content = File.ReadAllText(path);
        var (frontmatter, body, frontmatterStartLine) = SplitFrontmatter(path, content);

        ExpertFrontmatter meta;
        try
        {
            meta = Yaml.Deserialize<ExpertFrontmatter>(frontmatter);
        }
        catch (YamlException ex)
        {
            var line = frontmatterStartLine + (int)ex.Start.Line - 1;
            var col  = (int)ex.Start.Column - 1;
            throw new ExpertLoadException(ex.InnerException?.Message ?? ex.Message, path, line, col);
        }

        // Collect all semantic errors before surfacing them
        var errors = new List<ExpertLoadException>();

        if (string.IsNullOrWhiteSpace(meta.Name))
        {
            var (l, c, ec) = FindField(frontmatter, "name", frontmatterStartLine);
            errors.Add(new ExpertLoadException($"Missing required frontmatter field 'name' in {Path.GetFileName(path)}", path, l, c, ec));
        }
        if (string.IsNullOrWhiteSpace(meta.Input))
        {
            var (l, c, ec) = FindField(frontmatter, "input", frontmatterStartLine);
            errors.Add(new ExpertLoadException($"Missing required frontmatter field 'input' in {Path.GetFileName(path)}", path, l, c, ec));
        }
        if (string.IsNullOrWhiteSpace(meta.Output))
        {
            var (l, c, ec) = FindField(frontmatter, "output", frontmatterStartLine);
            errors.Add(new ExpertLoadException($"Missing required frontmatter field 'output' in {Path.GetFileName(path)}", path, l, c, ec));
        }

        // Use filename as fallback name in kind messages if name field is missing
        var expertName = string.IsNullOrWhiteSpace(meta.Name) ? Path.GetFileNameWithoutExtension(path) : meta.Name;
        var kindLine   = FindField(frontmatter, "kind", frontmatterStartLine);

        if (meta.Kind.Equals("http", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(meta.Endpoint))
            errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:http but is missing required field 'endpoint'.", path, kindLine.line, kindLine.col, kindLine.endCol));

        if (meta.Kind.Equals("rule", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(meta.Check))
            errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:rule but is missing required field 'check'.", path, kindLine.line, kindLine.col, kindLine.endCol));

        if (meta.Kind.Equals("onnx", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(meta.Model))
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:onnx but is missing required field 'model'.", path, kindLine.line, kindLine.col, kindLine.endCol));
            if (meta.Inputs.Count == 0)
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:onnx but is missing required field 'inputs'.", path, kindLine.line, kindLine.col, kindLine.endCol));
            if (string.IsNullOrWhiteSpace(meta.OutputKey))
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:onnx but is missing required field 'outputKey'.", path, kindLine.line, kindLine.col, kindLine.endCol));
            if (string.IsNullOrWhiteSpace(meta.Threshold))
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:onnx but is missing required field 'threshold'.", path, kindLine.line, kindLine.col, kindLine.endCol));
        }

        if (meta.Kind.Equals("exec", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(meta.Command))
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:exec but is missing required field 'command'.", path, kindLine.line, kindLine.col, kindLine.endCol));
            if (meta.Inputs.Count == 0)
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:exec but is missing required field 'inputs'.", path, kindLine.line, kindLine.col, kindLine.endCol));
            if (string.IsNullOrWhiteSpace(meta.OutputKey))
                errors.Add(new ExpertLoadException($"Expert '{expertName}' has kind:exec but is missing required field 'outputKey'.", path, kindLine.line, kindLine.col, kindLine.endCol));
        }

        if (errors.Count > 0)
            throw new AggregateExpertLoadException(errors);

        return new ExpertDefinition(meta.Name, meta.Input, meta.Output, body.Trim(), meta.Role, meta.Kind,
            meta.Endpoint, meta.Check, meta.OnFail, meta.Model, meta.Inputs, meta.OutputKey, meta.Threshold,
            meta.Command, meta.Args, meta.Timeout,
            ExpertDirectory: Path.GetDirectoryName(path) ?? "",
            OutputKeys: meta.OutputKeys.Count > 0 ? meta.OutputKeys : null,
            InputKeys:  meta.InputKeys.Count  > 0 ? meta.InputKeys  : null);
    }

    // Returns (frontmatter text, body text, 1-based line number of the first frontmatter line in the file).
    private static (string Frontmatter, string Body, int FrontmatterStartLine) SplitFrontmatter(string path, string content)
    {
        const string delimiter = "---";
        var lines = content.Split('\n');

        if (lines.Length < 2 || lines[0].Trim() != delimiter)
            throw new ExpertLoadException(
                $"Missing frontmatter delimiter '---' at start of {Path.GetFileName(path)}", path, 1, 0, 3);

        int closingIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == delimiter) { closingIndex = i; break; }
        }

        if (closingIndex < 0)
            throw new ExpertLoadException(
                $"Unclosed frontmatter block in {Path.GetFileName(path)}", path, 1, 0, 3);

        var frontmatter = string.Join('\n', lines[1..closingIndex]);
        var body        = string.Join('\n', lines[(closingIndex + 1)..]);
        return (frontmatter, body, FrontmatterStartLine: 2); // line 1 is "---", fields start at line 2
    }

    // Scans frontmatter text for "fieldName:" and returns its file-absolute line/col span.
    // When the field is absent, points at the opening "---" delimiter (startLine - 1).
    private static (int line, int col, int endCol) FindField(string frontmatter, string fieldName, int startLine)
    {
        var lines = frontmatter.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var raw     = lines[i];
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith(fieldName + ":", StringComparison.Ordinal))
            {
                var col = raw.Length - trimmed.Length;
                return (startLine + i, col, col + fieldName.Length);
            }
        }
        // Field not present — point at the opening "---" delimiter
        return (startLine - 1, 0, 3);
    }

    private class ExpertFrontmatter
    {
        public string       Name      { get; set; } = "";
        public string       Input     { get; set; } = "";
        public string       Output    { get; set; } = "";
        public string       Role      { get; set; } = "";
        public string       Kind      { get; set; } = "llm";
        public string       Endpoint  { get; set; } = "";
        public string       Check     { get; set; } = "";
        public string       OnFail    { get; set; } = "";
        public string       Model     { get; set; } = "";
        public List<string> Inputs    { get; set; } = [];
        public string       OutputKey { get; set; } = "";
        public string       Threshold { get; set; } = "";
        public string       Command     { get; set; } = "";
        public List<string> Args        { get; set; } = [];
        public string       Timeout     { get; set; } = "";
        public Dictionary<string, string> OutputKeys { get; set; } = [];
        public Dictionary<string, string> InputKeys  { get; set; } = [];
    }
}
