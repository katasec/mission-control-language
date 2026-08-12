using System.Text.Json;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Round-trips every durable conversation contract type through
/// <see cref="ConversationContractsJsonContext"/> only — the actual wire format Host and (from
/// Task 7) Client Runtime share. A round trip is proved by re-serializing the deserialized value
/// and comparing JSON text rather than record equality, because <see cref="JsonElement"/> fields
/// (<see cref="ConversationToolRequest.Arguments"/>, <see cref="ConversationCapabilityDeclaration.InputSchema"/>)
/// have reference-based default equality even when their content is identical.</summary>
public class ConversationContractsRoundTripTests
{
    private static void AssertRoundTrips<T>(T value)
    {
        var json1 = JsonSerializer.Serialize(value, typeof(T), ConversationContractsJsonContext.Default);
        var deserialized = JsonSerializer.Deserialize(json1, typeof(T), ConversationContractsJsonContext.Default);
        var json2 = JsonSerializer.Serialize(deserialized, typeof(T), ConversationContractsJsonContext.Default);
        Assert.Equal(json1, json2);
    }

    // Deliberately never disposed: the returned JsonElement stays valid only while its parent
    // JsonDocument is alive, and every caller here uses it synchronously within one test method.
    private static JsonElement SampleJson(string json) => JsonDocument.Parse(json).RootElement;

    private static ConversationEvent BuildEvent(ConversationEventKind kind)
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        return kind switch
        {
            ConversationEventKind.UserMessage => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 1, kind, ConversationParticipant.User,
                null, "please refactor the parser", null, null, null, null, null, null, occurredAt),

            ConversationEventKind.ParticipantStarted => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 2, kind, ConversationParticipant.Proposer,
                null, null, null, null, null, null, null, null, occurredAt),

            ConversationEventKind.ParticipantMessage => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 3, kind, ConversationParticipant.Proposer,
                1, "here is my proposal", null, null, null, null, null, null, occurredAt),

            ConversationEventKind.Approval => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 4, kind, ConversationParticipant.Approver,
                1, null, null, new ConversationApproval(ConversationApprovalOutcome.Approved, "looks good"),
                null, null, null, null, occurredAt),

            ConversationEventKind.ToolRequested => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 5, kind, ConversationParticipant.Implementer,
                1, null, null, null,
                new ConversationToolRequest(Guid.NewGuid(), "read_file", SampleJson("""{"path":"mission/notes.md"}""")),
                null, null, null, occurredAt),

            ConversationEventKind.ToolResult => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 6, kind, ConversationParticipant.Forge,
                1, null, null, null, null,
                new ConversationToolResult(Guid.NewGuid(), "file contents here", false),
                null, null, occurredAt),

            ConversationEventKind.RunStatus => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 7, kind, ConversationParticipant.Forge,
                null, null, null, null, null, null, null, ConversationRunStatus.Completed, occurredAt),

            ConversationEventKind.Artifact => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 8, kind, ConversationParticipant.Implementer,
                1, null, null, null, null, null,
                new ConversationArtifactReference("art-1", "text/plain", "output.txt"), null, occurredAt),

            ConversationEventKind.Error => new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, runId, 9, kind, ConversationParticipant.Forge,
                null, null, "provider request failed", null, null, null, null, null, occurredAt),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unhandled ConversationEventKind"),
        };
    }

    public static IEnumerable<object[]> AllEventKinds()
        => Enum.GetValues<ConversationEventKind>().Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(AllEventKinds))]
    public void ConversationEvent_RoundTripsForEveryEventKind(ConversationEventKind kind)
        => AssertRoundTrips(BuildEvent(kind));

    [Fact]
    public void ParticipantStartedEvent_OmitsEveryOptionalPayloadField()
    {
        var evt = BuildEvent(ConversationEventKind.ParticipantStarted);
        var json = JsonSerializer.Serialize(evt, typeof(ConversationEvent), ConversationContractsJsonContext.Default);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var omitted in new[] { "attempt", "text", "reason", "approval", "toolRequest", "toolResult", "artifact", "runStatus" })
        {
            Assert.False(root.TryGetProperty(omitted, out _), $"expected '{omitted}' to be omitted from {json}");
        }

        Assert.True(root.TryGetProperty("kind", out var kindProperty));
        Assert.Equal("participantStarted", kindProperty.GetString());
    }

    [Fact]
    public void UserMessageEvent_OmitsUnusedOptionalPayloadFieldsButKeepsText()
    {
        var evt = BuildEvent(ConversationEventKind.UserMessage);
        var json = JsonSerializer.Serialize(evt, typeof(ConversationEvent), ConversationContractsJsonContext.Default);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("text", out _));
        foreach (var omitted in new[] { "attempt", "reason", "approval", "toolRequest", "toolResult", "artifact", "runStatus" })
        {
            Assert.False(root.TryGetProperty(omitted, out _), $"expected '{omitted}' to be omitted from {json}");
        }
    }

    [Fact]
    public void ConversationSnapshot_RoundTrips()
        => AssertRoundTrips(new ConversationSnapshot(
            Guid.NewGuid(), "Janus", Guid.NewGuid(), 12,
            ConversationRunStatus.WaitingForTool, Guid.NewGuid(), DateTimeOffset.UtcNow));

    [Fact]
    public void ConversationCommand_StartMission_RoundTrips()
        => AssertRoundTrips(new ConversationCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Janus", "Refactor the parser",
            [new ConversationCapabilityDeclaration("read_file", "Read a mission-relative file", SampleJson("""{"type":"object"}"""))],
            null));

    [Fact]
    public void ConversationCommand_ContinueAfterTool_RoundTrips()
        => AssertRoundTrips(new ConversationCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.ContinueAfterTool,
            "Janus", "Refactor the parser", [],
            new ConversationToolResult(Guid.NewGuid(), "done", false)));

    [Fact]
    public void ConversationProgress_RoundTrips()
        => AssertRoundTrips(new ConversationProgress(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, 1, "proposal text", null, null, null, null, null, null,
            DateTimeOffset.UtcNow));

    [Fact]
    public void ConversationApproval_RoundTrips()
        => AssertRoundTrips(new ConversationApproval(ConversationApprovalOutcome.RevisionRequested, "needs tests"));

    [Fact]
    public void ConversationToolRequest_RoundTrips()
        => AssertRoundTrips(new ConversationToolRequest(Guid.NewGuid(), "write_file", SampleJson("""{"path":"a.txt","content":"hi"}""")));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConversationToolResult_RoundTrips(bool isError)
        => AssertRoundTrips(new ConversationToolResult(Guid.NewGuid(), "result content", isError));

    [Fact]
    public void ConversationArtifactReference_RoundTrips()
        => AssertRoundTrips(new ConversationArtifactReference("art-1", "application/json", "trace.json"));

    [Fact]
    public void ConversationArtifactReference_WithNullFileName_RoundTrips()
        => AssertRoundTrips(new ConversationArtifactReference("art-2", "text/plain", null));

    [Fact]
    public void ConversationCapabilityDeclaration_RoundTrips()
        => AssertRoundTrips(new ConversationCapabilityDeclaration("read_file", "Read a mission-relative file", SampleJson("""{"type":"object"}""")));

    [Theory]
    [MemberData(nameof(CapabilityArrays))]
    public void ConversationCapabilityDeclarationArray_RoundTrips(ConversationCapabilityDeclaration[] capabilities)
        => AssertRoundTrips(capabilities);

    public static IEnumerable<object[]> CapabilityArrays()
    {
        yield return [Array.Empty<ConversationCapabilityDeclaration>()];
        yield return [new ConversationCapabilityDeclaration[]
        {
            new("read_file", "Read a mission-relative file", SampleJson("""{"type":"object"}""")),
        }];
        yield return [new ConversationCapabilityDeclaration[]
        {
            new("read_file", "Read a mission-relative file", SampleJson("""{"type":"object"}""")),
            new("write_file", "Write a mission-relative file", SampleJson("""{"type":"object"}""")),
        }];
    }

    [Fact]
    public void StartConversationRequest_RoundTrips()
        => AssertRoundTrips(new StartConversationRequest(
            Guid.NewGuid(), "Janus", "Refactor the parser",
            [new ConversationCapabilityDeclaration("read_file", "Read a mission-relative file", SampleJson("""{"type":"object"}"""))]));

    [Fact]
    public void StartConversationResponse_RoundTrips()
        => AssertRoundTrips(new StartConversationResponse(Guid.NewGuid(), Guid.NewGuid(), 1, ConversationRunStatus.Queued));

    [Fact]
    public void SubmitConversationCommandRequest_RoundTrips()
        => AssertRoundTrips(new SubmitConversationCommandRequest(Guid.NewGuid(), "one more thing"));

    [Fact]
    public void SubmitConversationCommandResponse_RoundTrips()
        => AssertRoundTrips(new SubmitConversationCommandResponse(Guid.NewGuid(), Guid.NewGuid(), 2, ConversationRunStatus.Running));

    [Fact]
    public void SubmitToolResultRequest_RoundTrips()
        => AssertRoundTrips(new SubmitToolResultRequest(Guid.NewGuid(), Guid.NewGuid(), "tool output", false));

    [Fact]
    public void SubmitToolResultResponse_RoundTrips()
        => AssertRoundTrips(new SubmitToolResultResponse(Guid.NewGuid(), Guid.NewGuid(), 3, ConversationRunStatus.Running));

    public static IEnumerable<object[]> AllParticipants()
        => Enum.GetValues<ConversationParticipant>().Select(v => new object[] { v });

    [Theory]
    [MemberData(nameof(AllParticipants))]
    public void ConversationParticipant_RoundTripsForEveryValue(ConversationParticipant value)
        => AssertRoundTrips(value);

    public static IEnumerable<object[]> AllRunStatuses()
        => Enum.GetValues<ConversationRunStatus>().Select(v => new object[] { v });

    [Theory]
    [MemberData(nameof(AllRunStatuses))]
    public void ConversationRunStatus_RoundTripsForEveryValue(ConversationRunStatus value)
        => AssertRoundTrips(value);

    public static IEnumerable<object[]> AllApprovalOutcomes()
        => Enum.GetValues<ConversationApprovalOutcome>().Select(v => new object[] { v });

    [Theory]
    [MemberData(nameof(AllApprovalOutcomes))]
    public void ConversationApprovalOutcome_RoundTripsForEveryValue(ConversationApprovalOutcome value)
        => AssertRoundTrips(value);

    public static IEnumerable<object[]> AllCommandKinds()
        => Enum.GetValues<ConversationCommandKind>().Select(v => new object[] { v });

    [Theory]
    [MemberData(nameof(AllCommandKinds))]
    public void ConversationCommandKind_RoundTripsForEveryValue(ConversationCommandKind value)
        => AssertRoundTrips(value);

    [Fact]
    public void Enums_SerializeAsCamelCaseStrings()
    {
        Assert.Equal("\"userMessage\"", JsonSerializer.Serialize(ConversationEventKind.UserMessage, typeof(ConversationEventKind), ConversationContractsJsonContext.Default));
        Assert.Equal("\"waitingForTool\"", JsonSerializer.Serialize(ConversationRunStatus.WaitingForTool, typeof(ConversationRunStatus), ConversationContractsJsonContext.Default));
        Assert.Equal("\"revisionRequested\"", JsonSerializer.Serialize(ConversationApprovalOutcome.RevisionRequested, typeof(ConversationApprovalOutcome), ConversationContractsJsonContext.Default));
        Assert.Equal("\"continueAfterTool\"", JsonSerializer.Serialize(ConversationCommandKind.ContinueAfterTool, typeof(ConversationCommandKind), ConversationContractsJsonContext.Default));
        Assert.Equal("\"implementer\"", JsonSerializer.Serialize(ConversationParticipant.Implementer, typeof(ConversationParticipant), ConversationContractsJsonContext.Default));
    }

    [Fact]
    public void JsonElement_RoundTripsThroughContext()
        => AssertRoundTrips(SampleJson("""{"path":"mission/notes.md","recursive":true}"""));
}
