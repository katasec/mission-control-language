using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Application;

public sealed class MissionHandsConversationServiceTests
{
    [Fact]
    public void ExecuteTransport_CarriesOnlyAttachmentCorrelation_NotCallerCapabilityWork()
    {
        var names = typeof(ExecuteMissionHandsRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["SessionId", "ConversationId", "AttachmentId"], names);
        Assert.DoesNotContain("Request", names);
        Assert.DoesNotContain("CommandId", names);
    }

    [Fact]
    public void RecoveryTransport_CarriesOnlyTheFreshAttachmentCorrelation()
    {
        var names = typeof(RecoverMissionHandsRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["SessionId", "ConversationId", "AttachmentId"], names);
    }

    [Fact]
    public void GenericMissionToolRequest_MapsOnlyTheExactBobDeclarations()
    {
        var read = Request("Read", """{"file_path":"notes.md","offset":2,"limit":3}""");
        var bash = Request("Bash", """{"command":"printf contained"}""");
        var callerInvented = Request("execute_terminal", """{"command":"pwd"}""");

        var mappedRead = MissionHandsConversationService.ToCapabilityRequest(read);
        var mappedBash = MissionHandsConversationService.ToCapabilityRequest(bash);

        Assert.Equal("file", mappedRead!.Value.Name);
        Assert.Equal(new ReadFileCapabilityRequest("notes.md", 2, 3), mappedRead.Value.Request);
        Assert.Equal("terminal", mappedBash!.Value.Name);
        Assert.Equal(new ExecuteTerminalCapabilityRequest("printf contained"), mappedBash.Value.Request);
        Assert.Null(MissionHandsConversationService.ToCapabilityRequest(callerInvented));
    }

    private static MissionToolRequest Request(string toolName, string arguments)
    {
        using var document = JsonDocument.Parse(arguments);
        return new MissionToolRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "mission", "agent", "call",
            toolName, document.RootElement.Clone(), "continuation");
    }
}
