using System.Diagnostics.CodeAnalysis;
using ForgeMission.Parser;
using ForgeMission.Core.Resolution;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Experts;

public class ExpertLoader(string expertsDirectory)
{
    // ExpertFrontmatter is a private POCO directly instantiated here; DynamicDependency
    // ensures the trimmer keeps it so YamlDotNet's reflection path can find its members.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExpertFrontmatter))]
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

    /// <summary>Load experts from a directory, supporting both directory-per-expert and flat file.</summary>
    public Dictionary<string, ExpertDefinition> LoadAll()
    {
        if (!Directory.Exists(expertsDirectory))
            throw new ExpertLoadException($"Experts directory not found: {expertsDirectory}");

        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal);

        // Directory-per-expert: experts/Name/expert.md
        foreach (var dir in Directory.GetDirectories(expertsDirectory))
        {
            var expertMd = Path.Combine(dir, "expert.md");
            if (!File.Exists(expertMd)) continue;
            var expert = ParseFile(expertMd);
            experts[expert.Name] = expert;
        }

        // Flat fallback: experts/Name.md (backwards compatibility)
        foreach (var file in Directory.GetFiles(expertsDirectory, "*.md"))
        {
            var expert = ParseFile(file);
            if (!experts.ContainsKey(expert.Name))
                experts[expert.Name] = expert;
        }

        return experts;
    }

    public static void Validate(Program ast, Dictionary<string, ExpertDefinition> experts)
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

        // Step is valid if it has a loaded expert file, is a known mission (composition),
        // or is a mission parameter. OCI/stdlib resolution happens at runtime (Spoke 3).
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
        var (frontmatter, body) = SplitFrontmatter(path, content);

        var meta = Yaml.Deserialize<ExpertFrontmatter>(frontmatter);

        if (string.IsNullOrWhiteSpace(meta.Name))
            throw new ExpertLoadException($"Missing required frontmatter field 'name' in {Path.GetFileName(path)}");
        if (string.IsNullOrWhiteSpace(meta.Input))
            throw new ExpertLoadException($"Missing required frontmatter field 'input' in {Path.GetFileName(path)}");
        if (string.IsNullOrWhiteSpace(meta.Output))
            throw new ExpertLoadException($"Missing required frontmatter field 'output' in {Path.GetFileName(path)}");
        if (meta.Kind.Equals("http", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(meta.Endpoint))
            throw new ExpertLoadException($"Expert '{meta.Name}' has kind:http but is missing required field 'endpoint'.");
        if (meta.Kind.Equals("rule", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(meta.Check))
            throw new ExpertLoadException($"Expert '{meta.Name}' has kind:rule but is missing required field 'check'.");

        return new ExpertDefinition(meta.Name, meta.Input, meta.Output, body.Trim(), meta.Role, meta.Kind, meta.Endpoint, meta.Check, meta.OnFail);
    }

    private static (string Frontmatter, string Body) SplitFrontmatter(string path, string content)
    {
        const string delimiter = "---";
        var lines = content.Split('\n');

        if (lines.Length < 2 || lines[0].Trim() != delimiter)
            throw new ExpertLoadException($"Missing frontmatter delimiter '---' at start of {Path.GetFileName(path)}");

        int closingIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == delimiter) { closingIndex = i; break; }
        }

        if (closingIndex < 0)
            throw new ExpertLoadException($"Unclosed frontmatter block in {Path.GetFileName(path)}");

        var frontmatter = string.Join('\n', lines[1..closingIndex]);
        var body        = string.Join('\n', lines[(closingIndex + 1)..]);
        return (frontmatter, body);
    }

    private class ExpertFrontmatter
    {
        public string Name     { get; set; } = "";
        public string Input    { get; set; } = "";
        public string Output   { get; set; } = "";
        public string Role     { get; set; } = "";
        public string Kind     { get; set; } = "llm";
        public string Endpoint { get; set; } = "";
        public string Check    { get; set; } = "";
        public string OnFail   { get; set; } = "";
    }
}
