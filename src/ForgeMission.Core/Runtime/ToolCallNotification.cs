using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

public sealed record ToolCallNotification(
    FunctionCallContent Call,
    ToolCallNotificationState State,
    ToolExecutionResult? Result = null);

public enum ToolCallNotificationState
{
    Running,
    Done,
}
