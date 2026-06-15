using ForgeMission.Core.Parser;
using ForgeMission.Core.Resolution;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Experts;

public class ExpertLoader(string expertsDirectory)
{
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
            var path = Path.IsPathRooted(entry.Path)
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
        var declaredInAst = ast.Declarations
            .OfType<ExpertDeclaration>()
            .Select(e => e.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missionParams = ast.Declarations
            .OfType<MissionDeclaration>()
            .SelectMany(m => m.Params)
            .ToHashSet(StringComparer.Ordinal);

        var allSteps = ast.Declarations
            .SelectMany(d => d switch
            {
                MissionDeclaration m => m.Pipeline.Steps.Select(s => s.ExpertName),
                ExpertDeclaration e  => e.Pipeline.Steps.Select(s => s.ExpertName),
                _                    => Enumerable.Empty<string>()
            })
            .Distinct(StringComparer.Ordinal);

        var missing = allSteps
            .Where(step => !declaredInAst.Contains(step)
                        && !experts.ContainsKey(step)
                        && !missionParams.Contains(step))
            .OrderBy(s => s)
            .ToList();

        if (missing.Count > 0)
            throw new ExpertLoadException(
                $"Missing expert definitions for: {string.Join(", ", missing)}. " +
                "Each expert must be declared in the .fml file or have a matching markdown file in the experts directory.");
    }

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

        return new ExpertDefinition(meta.Name, meta.Input, meta.Output, body.Trim());
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
        public string Name   { get; set; } = "";
        public string Input  { get; set; } = "";
        public string Output { get; set; } = "";
    }
}
