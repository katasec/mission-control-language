using RunnerContracts = ForgeMission.Runner.Contracts;

namespace ForgeMission.Api;

/// <summary>
/// API A and the Runner intentionally own separate transport DTOs. This bridge preserves the
/// agent-turn wire shape at their boundary without coupling either contract to the other.
/// </summary>
internal static class MissionToolTurnMapper
{
    public static IReadOnlyList<RunnerContracts.TurnMessage>? ToRunnerHistory(
        IReadOnlyList<TurnMessage>? history)
        => history?.Select(message => new RunnerContracts.TurnMessage
        {
            Role = message.Role,
            Content = message.Content.Select(content => new RunnerContracts.TurnContent
            {
                Type = content.Type,
                Text = content.Text,
                ToolUseId = content.ToolUseId,
                ToolName = content.ToolName,
                ToolInput = content.ToolInput,
                ToolResult = content.ToolResult,
            }).ToList(),
        }).ToList();

    public static IReadOnlyList<RunnerContracts.MissionToolDecl>? ToRunnerTools(
        IReadOnlyList<MissionToolDecl>? tools)
        => tools?.Select(tool => new RunnerContracts.MissionToolDecl
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = tool.InputSchema,
        }).ToList();

    public static List<ToolUseCall>? ToApiToolUse(
        IReadOnlyList<RunnerContracts.ToolUseCall>? toolUse)
        => toolUse?.Select(call => new ToolUseCall
        {
            Id = call.Id,
            Name = call.Name,
            Arguments = call.Arguments,
        }).ToList();
}
