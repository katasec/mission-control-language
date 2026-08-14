using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Orleans;
using Orleans.Runtime;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The run state machine and terminal-state owner for one run, keyed <c>{TenantId}|{RunId:N}</c>.
/// Never creates a per-expert grain. On activation, an <c>ExecutingProvider</c> boundary either
/// adopts a terminal transcript fact it finds (transcript wins) or persists itself
/// Interrupted/Terminal with a stable interruption ID <em>before</em> reporting that fact to
/// <see cref="ConversationGrain"/> — never invoking a provider or re-sending work.
/// </summary>
public sealed class MissionRunGrain(
    [PersistentState("mission-run-checkpoint", "mission-run-checkpoint")] IPersistentState<MissionRunCheckpoint> checkpoint,
    IConversationEventStore eventStore,
    IGrainFactory grainFactory)
    : Grain, IMissionRunGrain
{
    // Derived from the grain's own key ({TenantId}|{RunId:N}) — never read back from checkpoint
    // state, so it is always available even on a MissionRunGrain's very first call, before any
    // WriteStateAsync has ever run.
    private string TenantId => this.GetPrimaryKeyString().Split('|', 2)[0];

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (checkpoint.State.ExecutionBoundary == MissionRunExecutionBoundary.ExecutingProvider)
        {
            var address = new ConversationAddress(TenantId, checkpoint.State.ConversationId);
            var latest = await eventStore.ReadLatestForRunAsync(address, checkpoint.State.RunId, cancellationToken);

            if (latest is { RunStatus: { } terminalStatus } && IsTerminal(terminalStatus))
            {
                // A terminal event appended just before the run checkpoint write — transcript
                // wins, no synthetic interruption is needed.
                checkpoint.State.Status = terminalStatus;
                checkpoint.State.ExecutionBoundary = MissionRunExecutionBoundary.Terminal;
                checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await checkpoint.WriteStateAsync();
            }
            else
            {
                // Generate/store the stable interruption identity and persist Interrupted/Terminal
                // BEFORE reporting it — the report is a durable, idempotent fact from here on.
                checkpoint.State.InterruptionEventId ??= Guid.NewGuid();
                checkpoint.State.InterruptionOccurredAtUtc ??= DateTimeOffset.UtcNow;
                checkpoint.State.Status = ConversationRunStatus.Interrupted;
                checkpoint.State.ExecutionBoundary = MissionRunExecutionBoundary.Terminal;
                checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await checkpoint.WriteStateAsync();

                await ReportInterruptionAsync();
            }
        }
        else if (checkpoint.State is
                 { ExecutionBoundary: MissionRunExecutionBoundary.Terminal, Status: ConversationRunStatus.Interrupted, InterruptionEventId: not null })
        {
            // A prior activation persisted Interrupted/Terminal + the stable ID, but this
            // activation cannot know whether ConversationGrain's append was ever confirmed. Retry
            // the SAME ID — idempotent via the event store's own event-ID dedupe.
            await ReportInterruptionAsync();
        }
        // WaitingForTool / NotStarted: no repair action.

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task ApplyDurableEventAsync(MissionRunEventInput @event)
    {
        if (@event.Kind == ConversationEventKind.UserMessage)
            return; // No run-state change — AcceptCommandAsync already initialized the run.

        checkpoint.State.TenantId = TenantId;
        checkpoint.State.RunId = @event.RunId;
        checkpoint.State.ConversationId = @event.ConversationId;

        switch (@event.Kind)
        {
            case ConversationEventKind.RunStatus:
                ApplyRunStatus(@event.RunStatus!.Value);
                break;

            case ConversationEventKind.ParticipantStarted:
                checkpoint.State.Status = ConversationRunStatus.Running;
                checkpoint.State.ExecutionBoundary = MissionRunExecutionBoundary.ExecutingProvider;
                break;

            case ConversationEventKind.ParticipantMessage:
            case ConversationEventKind.Approval:
            case ConversationEventKind.Artifact:
            case ConversationEventKind.ToolResult:
            case ConversationEventKind.Error:
                // Preserve the existing non-terminal Status — each of these is itself a durable
                // safe boundary before any later provider call may begin.
                checkpoint.State.ExecutionBoundary = MissionRunExecutionBoundary.NotStarted;
                break;

            case ConversationEventKind.ToolRequested:
                checkpoint.State.Status = ConversationRunStatus.WaitingForTool;
                checkpoint.State.ExecutionBoundary = MissionRunExecutionBoundary.WaitingForTool;
                break;
        }

        checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await checkpoint.WriteStateAsync();
    }

    public Task<ConversationRunStatus> GetStatusAsync() => Task.FromResult(checkpoint.State.Status);

    private void ApplyRunStatus(ConversationRunStatus status)
    {
        checkpoint.State.Status = status;
        checkpoint.State.ExecutionBoundary = status switch
        {
            ConversationRunStatus.Queued or ConversationRunStatus.Running => MissionRunExecutionBoundary.NotStarted,
            ConversationRunStatus.WaitingForTool => MissionRunExecutionBoundary.WaitingForTool,
            _ when IsTerminal(status) => MissionRunExecutionBoundary.Terminal,
            _ => checkpoint.State.ExecutionBoundary,
        };
    }

    private async Task ReportInterruptionAsync()
    {
        var conversationGrain = grainFactory.GetGrain<IConversationGrain>(
            new ConversationAddress(TenantId, checkpoint.State.ConversationId).PartitionKey);

        await conversationGrain.RecordRunInterruptionAsync(new MissionRunInterruption(
            checkpoint.State.RunId, checkpoint.State.InterruptionEventId!.Value, checkpoint.State.InterruptionOccurredAtUtc!.Value));
    }

    private static bool IsTerminal(ConversationRunStatus status) => status is
        ConversationRunStatus.Completed or ConversationRunStatus.Rejected or
        ConversationRunStatus.Interrupted or ConversationRunStatus.Failed;
}
