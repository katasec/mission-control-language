using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Presentation;

/// <summary>
/// One entry in the Project's human control thread: what a person asked, and how that run ended.
///
/// There is deliberately no field for an expert's words. The experts speak in the run's trace, and
/// the enduring human ↔ Forge thread receives a concise outcome instead — so "Missions never shows
/// a Proposer, Approver, Implementer, Naive, approval, or tool turn" is a property of this type
/// rather than a rule a renderer has to keep remembering.
/// </summary>
/// <param name="Instruction">Exactly what was submitted. The control thread owns the prompt; the
/// trace never repeats it as though an expert had said it.</param>
/// <param name="ExpertTurns">How many expert messages the run produced — a count of durable events,
/// never a reading of them.</param>
/// <param name="ToolCalls">How many tools the run requested. On this route it is always zero, and
/// showing the zero is the point.</param>
/// <param name="HasTrace">Whether any expert event has arrived yet. A trace that would open empty
/// is not offered.</param>
public sealed record MissionRunEntry(
    Guid RunId,
    string Mission,
    string Instruction,
    ConversationRunStatus Status,
    int ExpertTurns,
    int ToolCalls,
    bool HasTrace);

/// <summary>
/// Pure, dependency-free projection of one session's Project Mission runs into two separate things:
/// the control thread above, and one exact expert transcript per run.
///
/// It knows a run only because this session started it — <see cref="Accept"/> is the only way an
/// entry appears. That is what keeps a reopened Project's replayed history out: those events belong
/// to runs this session never started, so they are dropped rather than rendered as if they had just
/// happened. It is also why there is no history or reopen support here; that is Phase 43.4's.
///
/// Owns no HTTP, no SSE parsing and no Host knowledge, exactly like
/// <see cref="ConversationTranscript"/>, which it reuses unchanged for the trace so an expert's
/// message reaches the screen byte-identical to the durable event.
/// </summary>
public sealed class MissionRunThread
{
    private readonly List<MissionRunEntry> _entries = [];
    private readonly Dictionary<Guid, int> _indexByRun = [];
    private readonly Dictionary<Guid, ConversationTranscript> _traceByRun = [];

    public IReadOnlyList<MissionRunEntry> Entries => _entries;

    /// <summary>Records an accepted run. The mission comes from the Host's acceptance rather than
    /// the rendered selection, because the two can differ if the selection changed between drawing
    /// the button and pressing it.</summary>
    public void Accept(Guid runId, string mission, string instruction)
    {
        if (_indexByRun.ContainsKey(runId))
            return;

        _entries.Add(new MissionRunEntry(
            runId, mission, instruction, ConversationRunStatus.Queued, 0, 0, HasTrace: false));
        _indexByRun[runId] = _entries.Count - 1;
        _traceByRun[runId] = new ConversationTranscript();
    }

    /// <summary>Applies one durable fact. An event for a run this session did not start is ignored.</summary>
    public void Apply(ConversationEvent evt)
    {
        if (evt.RunId is not { } runId
            || !_indexByRun.TryGetValue(runId, out var index)
            || !_traceByRun.TryGetValue(runId, out var trace))
            return;

        // The prompt is the control thread's, not an expert turn. Everything else the run produced
        // is trace evidence and is passed through unread.
        if (evt.Kind != ConversationEventKind.UserMessage)
            trace.Apply(evt);

        _entries[index] = evt.Kind switch
        {
            ConversationEventKind.RunStatus when evt.RunStatus is { } status =>
                _entries[index] with { Status = status },
            ConversationEventKind.ParticipantMessage =>
                _entries[index] with { ExpertTurns = _entries[index].ExpertTurns + 1 },
            ConversationEventKind.ToolRequested =>
                _entries[index] with { ToolCalls = _entries[index].ToolCalls + 1 },
            _ => _entries[index],
        };

        _entries[index] = _entries[index] with { HasTrace = trace.Entries.Count > 0 };
    }

    /// <summary>The exact expert transcript for one run, or null for a run this session did not
    /// start.</summary>
    public ConversationTranscript? Trace(Guid runId) =>
        _traceByRun.GetValueOrDefault(runId);

    public MissionRunEntry? Entry(Guid runId) =>
        _indexByRun.TryGetValue(runId, out var index) ? _entries[index] : null;

    /// <summary>Whether a terminal status has arrived for this run.</summary>
    public static bool IsTerminal(ConversationRunStatus status) => status is
        ConversationRunStatus.Completed or ConversationRunStatus.Rejected
        or ConversationRunStatus.Interrupted or ConversationRunStatus.Failed;
}
