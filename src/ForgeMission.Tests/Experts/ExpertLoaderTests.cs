using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Experts;

public class ExpertLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ExpertLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string ValidExpertMarkdown(
        string name   = "KubernetesArchitect",
        string input  = "MissionBrief",
        string output = "ArchitectureProposal",
        string body   = "You are a Kubernetes platform architect.") => $"""
        ---
        name: {name}
        input: {input}
        output: {output}
        ---

        {body}
        """;

    // Writes a flat expert file (legacy format)
    private void WriteFlatExpert(string filename, string content)
        => File.WriteAllText(Path.Combine(_dir, filename), content);

    // Writes a directory-per-expert (new format)
    private void WriteDirExpert(string name, string content)
    {
        var dir = Path.Combine(_dir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "expert.md"), content);
    }

    // ---------------------------------------------------------------------------
    // Flat file format (backwards compat)

    [Fact]
    public void LoadAll_FlatFile_LoadsCorrectly()
    {
        WriteFlatExpert("KubernetesArchitect.md", ValidExpertMarkdown());
        var experts = new ExpertLoader(_dir).LoadAll();
        Assert.Single(experts);
        Assert.True(experts.ContainsKey("KubernetesArchitect"));
    }

    [Fact]
    public void LoadAll_FlatFile_ParsesFrontmatterFields()
    {
        WriteFlatExpert("KubernetesArchitect.md", ValidExpertMarkdown());
        var expert = new ExpertLoader(_dir).LoadAll()["KubernetesArchitect"];
        Assert.Equal("KubernetesArchitect", expert.Name);
        Assert.Equal("MissionBrief", expert.Input);
        Assert.Equal("ArchitectureProposal", expert.Output);
    }

    // ---------------------------------------------------------------------------
    // Directory-per-expert format (new)

    [Fact]
    public void LoadAll_DirectoryPerExpert_LoadsCorrectly()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown());
        var experts = new ExpertLoader(_dir).LoadAll();
        Assert.Single(experts);
        Assert.True(experts.ContainsKey("KubernetesArchitect"));
    }

    [Fact]
    public void LoadAll_DirectoryPerExpert_ParsesFrontmatterFields()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown());
        var expert = new ExpertLoader(_dir).LoadAll()["KubernetesArchitect"];
        Assert.Equal("KubernetesArchitect", expert.Name);
        Assert.Equal("MissionBrief", expert.Input);
        Assert.Equal("ArchitectureProposal", expert.Output);
    }

    [Fact]
    public void LoadAll_MultipleDirectoryExperts_LoadsAll()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        WriteDirExpert("SecurityArchitect",   ValidExpertMarkdown("SecurityArchitect",   "ArchitectureProposal", "SecurityReview"));
        WriteDirExpert("PrincipalReviewer",   ValidExpertMarkdown("PrincipalReviewer",   "SecurityReview", "FinalReport"));
        var experts = new ExpertLoader(_dir).LoadAll();
        Assert.Equal(3, experts.Count);
    }

    [Fact]
    public void LoadAll_DirectoryWithoutExpertMd_IsIgnored()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown());
        Directory.CreateDirectory(Path.Combine(_dir, "EmptyDir")); // no expert.md
        var experts = new ExpertLoader(_dir).LoadAll();
        Assert.Single(experts);
    }

    [Fact]
    public void LoadAll_DirectoryExpert_TakesPrecedenceOverFlatFile()
    {
        // Both formats define the same expert — directory wins
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown(body: "From directory"));
        WriteFlatExpert("KubernetesArchitect.md", ValidExpertMarkdown(body: "From flat file"));
        var experts = new ExpertLoader(_dir).LoadAll();
        Assert.Single(experts);
        Assert.Contains("From directory", experts["KubernetesArchitect"].SystemPrompt);
    }

    [Fact]
    public void LoadAll_MissingFrontmatterField_ThrowsExpertLoadException()
    {
        WriteDirExpert("Bad", "---\nname: Bad\n---\nBody");
        var ex = Assert.Throws<ExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("input", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // Validation

    [Fact]
    public void Validate_MissingExpert_ThrowsExpertLoadException()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Assert.Throws<ExpertLoadException>(() => ExpertLoader.Validate(ast, experts));
        Assert.Contains("SecurityArchitect", ex.Message);
    }

    [Fact]
    public void Validate_AllExpertsPresent_DoesNotThrow()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        WriteDirExpert("SecurityArchitect",   ValidExpertMarkdown("SecurityArchitect", "ArchitectureProposal", "SecurityReview"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Record.Exception(() => ExpertLoader.Validate(ast, experts));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExpertDeclaredInAst_DoesNotRequireMarkdownFile()
    {
        var ast = MclParser.Parse("""
            expert KubernetesArchitect =
                RequirementsAnalyst
                |> PlatformArchitect

            mission BuildOperator =
                KubernetesArchitect
            """);

        WriteDirExpert("RequirementsAnalyst", ValidExpertMarkdown("RequirementsAnalyst", "Brief", "Analysis"));
        WriteDirExpert("PlatformArchitect",   ValidExpertMarkdown("PlatformArchitect", "Analysis", "Design"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Record.Exception(() => ExpertLoader.Validate(ast, experts));
        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // Lock file loading

    [Fact]
    public void LoadFromLockFile_ReadsExpertsByPath()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown());
        var relativePath = Path.Combine("KubernetesArchitect", "expert.md");

        var lockFile = new LockFile
        {
            Sources = ["./experts"],
            Experts = new Dictionary<string, LockFileExpert>
            {
                ["KubernetesArchitect"] = new() { Source = "./experts", Path = relativePath }
            }
        };

        var experts = ExpertLoader.LoadFromLockFile(lockFile, _dir);
        Assert.Single(experts);
        Assert.Equal("KubernetesArchitect", experts["KubernetesArchitect"].Name);
    }
}
