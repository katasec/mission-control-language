using ForgeMission.ClientRuntime.Services;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ProjectRunReadStateTests
{
    [Fact]
    public void DelayedPriorGeneration_DoesNotReplaceTheSelectedRun()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var state = ProjectRunReadState.Empty.SelectRun(first).SelectRun(second);
        var detail = new ProjectRunDetail(Summary(first), "old", 1, 1);

        var reduced = state.ApplyDetail(generation: 1, detail);

        Assert.Null(reduced.Detail);
        Assert.Equal(second, reduced.SelectedRunId);
    }

    [Fact]
    public void InconsistentDuplicateRunSequence_IsRejected()
    {
        var run = Guid.NewGuid();
        var page = new ProjectRunPage(Guid.NewGuid(), 2, 2, false,
            [Summary(run, 1), Summary(run, 2)], null);

        Assert.Throws<InvalidOperationException>(() => ProjectRunReadState.Empty.ReplacePage(page));
    }

    private static ProjectRunSummary Summary(Guid runId, long last = 1) => new(runId, Guid.NewGuid(), "Janus", "Task",
        1, last, ConversationRunStatus.Running, 0, 0, DateTimeOffset.UtcNow);
}
