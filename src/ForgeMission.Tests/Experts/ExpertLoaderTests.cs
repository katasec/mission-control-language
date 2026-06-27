using ForgeMission.Core.Experts;
using ForgeMission.Parser;
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
        var agg = Assert.Throws<AggregateExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("input", agg.Errors[0].Message);
    }

    // ---------------------------------------------------------------------------
    // Validation

    [Fact]
    public void Validate_MissingExpert_ThrowsExpertLoadException()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
            }
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
            mission BuildOperator = {
                KubernetesArchitect
                -> SecurityArchitect
            }
            """);

        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        WriteDirExpert("SecurityArchitect",   ValidExpertMarkdown("SecurityArchitect", "ArchitectureProposal", "SecurityReview"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Record.Exception(() => ExpertLoader.Validate(ast, experts));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_MissionUsedAsStep_DoesNotRequireMarkdownFile()
    {
        // Mission composition: a step that names another mission in the AST
        // is valid even without a markdown expert file.
        var ast = MclParser.Parse("""
            mission InnerMission = {
                RequirementsAnalyst
                -> PlatformArchitect
            }

            mission BuildOperator = {
                InnerMission
            }
            """);

        WriteDirExpert("RequirementsAnalyst", ValidExpertMarkdown("RequirementsAnalyst", "Brief", "Analysis"));
        WriteDirExpert("PlatformArchitect",   ValidExpertMarkdown("PlatformArchitect", "Analysis", "Design"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Record.Exception(() => ExpertLoader.Validate(ast, experts));
        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // Role field

    [Fact]
    public void ParseFile_NoRoleField_IsJudgeFalse()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown());
        var expert = new ExpertLoader(_dir).LoadAll()["KubernetesArchitect"];
        Assert.False(expert.IsJudge);
    }

    [Fact]
    public void ParseFile_RoleJudge_IsJudgeTrue()
    {
        WriteDirExpert("QualityJudge", $"""
            ---
            name: QualityJudge
            input: explanation
            output: verdict
            role: judge
            ---

            You are a quality judge.
            """);
        var expert = new ExpertLoader(_dir).LoadAll()["QualityJudge"];
        Assert.True(expert.IsJudge);
    }

    [Fact]
    public void ParseFile_RoleCritic_IsJudgeFalse()
    {
        WriteDirExpert("PitchCritic", $"""
            ---
            name: PitchCritic
            input: draft
            output: critique
            role: critic
            ---

            You are a critic.
            """);
        var expert = new ExpertLoader(_dir).LoadAll()["PitchCritic"];
        Assert.False(expert.IsJudge);
    }

    // ---------------------------------------------------------------------------
    // Kind field

    [Fact]
    public void ParseFile_NoKindField_DefaultsToLlm()
    {
        WriteDirExpert("KubernetesArchitect", ValidExpertMarkdown());
        var expert = new ExpertLoader(_dir).LoadAll()["KubernetesArchitect"];
        Assert.Equal("llm", expert.Kind);
        Assert.False(expert.IsHttp);
    }

    [Fact]
    public void ParseFile_KindHttp_IsHttpTrue()
    {
        WriteDirExpert("Scorer", """
            ---
            name: Scorer
            input: features
            output: score
            kind: http
            endpoint: http://localhost:5000/score
            ---
            """);
        var expert = new ExpertLoader(_dir).LoadAll()["Scorer"];
        Assert.Equal("http", expert.Kind);
        Assert.True(expert.IsHttp);
    }

    [Fact]
    public void ParseFile_KindHttp_ReadsEndpoint()
    {
        WriteDirExpert("Scorer", """
            ---
            name: Scorer
            input: features
            output: score
            kind: http
            endpoint: http://localhost:5000/score
            ---
            """);
        var expert = new ExpertLoader(_dir).LoadAll()["Scorer"];
        Assert.Equal("http://localhost:5000/score", expert.Endpoint);
    }

    [Fact]
    public void ParseFile_KindHttp_MissingEndpoint_Throws()
    {
        WriteDirExpert("Scorer", """
            ---
            name: Scorer
            input: features
            output: score
            kind: http
            ---
            """);
        var agg = Assert.Throws<AggregateExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("endpoint", agg.Errors[0].Message);
    }

    // ---------------------------------------------------------------------------
    // kind: onnx validation

    private static string OnnxExpertMarkdown(
        string? model     = "./models/iso.onnx",
        string? inputs    = "cpu,mem",
        string? outputKey = "score",
        string? threshold = "0.85") =>
        $"""
        ---
        name: AnomalyDetector
        input: Normalised metric features
        output: Anomaly score
        kind: onnx
        {(model     is not null ? $"model: {model}"         : "")}
        {(inputs    is not null ? $"inputs: {inputs}"       : "")}
        {(outputKey is not null ? $"outputKey: {outputKey}" : "")}
        {(threshold is not null ? $"threshold: {threshold}" : "")}
        ---
        Detect anomalies.
        """;

    [Fact]
    public void OnnxExpert_ValidFrontmatter_ParsesAllFields()
    {
        WriteDirExpert("AnomalyDetector", OnnxExpertMarkdown());
        var experts = new ExpertLoader(_dir).LoadAll();
        var def = experts["AnomalyDetector"];
        Assert.Equal("onnx", def.Kind, ignoreCase: true);
        Assert.Equal("./models/iso.onnx", def.Model);
        Assert.Equal("cpu,mem", def.Inputs);
        Assert.Equal("score", def.OutputKey);
        Assert.Equal("0.85", def.Threshold);
        Assert.True(def.IsOnnx);
    }

    [Fact]
    public void OnnxExpert_MissingModel_Throws()
    {
        WriteDirExpert("AnomalyDetector", OnnxExpertMarkdown(model: null));
        var agg = Assert.Throws<AggregateExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("model", agg.Errors[0].Message);
    }

    [Fact]
    public void OnnxExpert_MissingInputs_Throws()
    {
        WriteDirExpert("AnomalyDetector", OnnxExpertMarkdown(inputs: null));
        var agg = Assert.Throws<AggregateExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("inputs", agg.Errors[0].Message);
    }

    [Fact]
    public void OnnxExpert_MissingOutputKey_Throws()
    {
        WriteDirExpert("AnomalyDetector", OnnxExpertMarkdown(outputKey: null));
        var agg = Assert.Throws<AggregateExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("outputKey", agg.Errors[0].Message);
    }

    [Fact]
    public void OnnxExpert_MissingThreshold_Throws()
    {
        WriteDirExpert("AnomalyDetector", OnnxExpertMarkdown(threshold: null));
        var agg = Assert.Throws<AggregateExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("threshold", agg.Errors[0].Message);
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
            Experts = new Dictionary<string, LockFileExpert>
            {
                ["KubernetesArchitect"] = new() { Source = "experts", Path = relativePath }
            }
        };

        var experts = ExpertLoader.LoadFromLockFile(lockFile, _dir);
        Assert.Single(experts);
        Assert.Equal("KubernetesArchitect", experts["KubernetesArchitect"].Name);
    }
}
