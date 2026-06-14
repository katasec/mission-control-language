using ForgeMission.Core.Parser;

namespace ForgeMission.Tests.Parser;

public class ParserTests
{
    [Fact]
    public void ValidMission_SingleExpert_ParsesCorrectly()
    {
        var result = FmlParser.Parse("mission BuildOperator = KubernetesArchitect");

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        Assert.Equal("BuildOperator", mission.Name);
        Assert.Equal(["KubernetesArchitect"], mission.Pipeline.Steps);
    }

    [Fact]
    public void ValidMission_MultiStepPipeline_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
                |> PrincipalReviewer
            """;

        var result = FmlParser.Parse(source);

        var mission = Assert.Single(result.Declarations) as MissionDeclaration;
        Assert.NotNull(mission);
        Assert.Equal("BuildOperator", mission.Name);
        Assert.Equal(["KubernetesArchitect", "SecurityArchitect", "PrincipalReviewer"], mission.Pipeline.Steps);
    }

    [Fact]
    public void ValidExpert_ParsesCorrectly()
    {
        var source = """
            expert KubernetesArchitect =
                RequirementsAnalyst
                |> PlatformArchitect
                |> ReliabilityArchitect
            """;

        var result = FmlParser.Parse(source);

        var expert = Assert.Single(result.Declarations) as ExpertDeclaration;
        Assert.NotNull(expert);
        Assert.Equal("KubernetesArchitect", expert.Name);
        Assert.Equal(["RequirementsAnalyst", "PlatformArchitect", "ReliabilityArchitect"], expert.Pipeline.Steps);
    }

    [Fact]
    public void RecursiveExpert_ReferencingOtherExperts_ParsesCorrectly()
    {
        var source = """
            expert KubernetesArchitect =
                RequirementsAnalyst
                |> PlatformArchitect

            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """;

        var result = FmlParser.Parse(source);

        Assert.Equal(2, result.Declarations.Count);
        Assert.IsType<ExpertDeclaration>(result.Declarations[0]);
        Assert.IsType<MissionDeclaration>(result.Declarations[1]);
    }

    [Fact]
    public void MissionAndExpert_InSameFile_ParsesCorrectly()
    {
        var source = """
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
                |> PrincipalReviewer

            expert KubernetesArchitect =
                RequirementsAnalyst
                |> PlatformArchitect
            """;

        var result = FmlParser.Parse(source);

        Assert.Equal(2, result.Declarations.Count);
        var mission = result.Declarations[0] as MissionDeclaration;
        var expert = result.Declarations[1] as ExpertDeclaration;
        Assert.NotNull(mission);
        Assert.NotNull(expert);
        Assert.Equal("BuildOperator", mission.Name);
        Assert.Equal("KubernetesArchitect", expert.Name);
    }

    [Fact]
    public void LowercaseIdentifier_ThrowsParseException()
    {
        var source = "mission BuildOperator = kubernetesArchitect";

        var ex = Assert.Throws<ParseException>(() => FmlParser.Parse(source));
        Assert.Contains("PascalCase", ex.Message);
    }

    [Fact]
    public void MissingEquals_ThrowsParseException()
    {
        var source = "mission BuildOperator KubernetesArchitect";

        var ex = Assert.Throws<ParseException>(() => FmlParser.Parse(source));
        Assert.Contains("Equals", ex.Message);
    }

    [Fact]
    public void EmptyPipeline_ThrowsParseException()
    {
        var source = "mission BuildOperator =";

        Assert.Throws<ParseException>(() => FmlParser.Parse(source));
    }
}
