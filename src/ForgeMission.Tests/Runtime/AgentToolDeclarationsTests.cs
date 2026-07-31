using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Runtime;

// Phase 43.1 task 1: the four tool declarations Forge Desktop attaches for its terminal agent
// expert — no wire relay in this path, forge originates the schemas itself.
public sealed class AgentToolDeclarationsTests
{
    private static readonly CapabilityRegistry AllCapabilities = new(
    [
        new WorkspaceFileProvider(new LocalDiskWorkspace(AppContext.BaseDirectory)),
        new WorkspaceTerminalProvider(new LocalDiskWorkspace(AppContext.BaseDirectory)),
    ]);

    [Fact]
    public void All_ContainsExactlyTheEssentials()
    {
        var names = AllCapabilities.ToolDeclarations.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Bash", "Edit", "Read", "Write"], names);
    }

    [Fact]
    public void Edit_Schema_MatchesThisSessionsOwnContract()
    {
        var schema = AgentToolDeclarations.Edit.JsonSchema;
        Assert.True(schema.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("old_string", out _));
        Assert.True(props.TryGetProperty("new_string", out _));
        Assert.True(props.TryGetProperty("replace_all", out _));

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(["file_path", "old_string", "new_string"], required);
    }

    [Fact]
    public void Bash_Schema_IsCommandOnly()
    {
        var schema = AgentToolDeclarations.Bash.JsonSchema;
        var props  = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["command"], props); // trimmed — no timeout/run_in_background/dangerouslyDisableSandbox
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Edit")]
    [InlineData("Write")]
    [InlineData("Bash")]
    public async Task Declaration_IsNeverExecutableDirectly(string toolName)
    {
        var tool = (AIFunction)AllCapabilities.ToolDeclarations.Single(t => t.Name == toolName);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => tool.InvokeAsync(new AIFunctionArguments()).AsTask());
    }
}
