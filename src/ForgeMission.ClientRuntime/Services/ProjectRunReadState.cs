using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// Pure bounded read reducer shared by the Mission and Explorer surfaces. Paging replaces the
/// current window; generation prevents a delayed response for a prior Project/run from repainting
/// the newly selected trace.
/// </summary>
internal sealed record ProjectRunReadState(
    int Generation, Guid? SelectedRunId, ProjectRunPage? Page,
    ProjectRunDetail? Detail, ProjectRunEventPage? Events)
{
    public static ProjectRunReadState Empty { get; } = new(0, null, null, null, null);

    public ProjectRunReadState ReplacePage(ProjectRunPage page)
    {
        ValidatePage(page);
        return this with { Page = page };
    }

    public ProjectRunReadState SelectRun(Guid runId) => runId == Guid.Empty
        ? throw new ArgumentOutOfRangeException(nameof(runId))
        : new(Generation + 1, runId, Page, null, null);

    public ProjectRunReadState ApplyDetail(int generation, ProjectRunDetail detail) =>
        generation != Generation || detail.Run.RunId != SelectedRunId ? this : this with { Detail = detail };

    public ProjectRunReadState ApplyEvents(int generation, ProjectRunEventPage events) =>
        generation != Generation || events.RunId != SelectedRunId ? this : this with { Events = events };

    private static void ValidatePage(ProjectRunPage page)
    {
        var seen = new Dictionary<Guid, ProjectRunSummary>();
        foreach (var run in page.Runs)
        {
            if (seen.TryGetValue(run.RunId, out var existing) &&
                (existing.AcceptedSequence != run.AcceptedSequence || existing.LastSequence != run.LastSequence))
                throw new InvalidOperationException("A Project run page contains inconsistent duplicate run sequences.");
            seen[run.RunId] = run;
        }
    }
}
