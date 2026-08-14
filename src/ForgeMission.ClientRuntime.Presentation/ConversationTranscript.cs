using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Presentation;

public enum ConversationEntryKind
{
    UserMessage,
    Typing,
    ParticipantMessage,
    Approval,
    NotApproved,
    ToolCall,
    Status,
}

/// <summary>One rendered row of the Janus group-chat transcript. A single flat shape (rather than
/// a type per <see cref="ConversationEntryKind"/>) mirrors <see cref="ConversationEvent"/>'s own
/// convention: only the fields matching <see cref="Kind"/> are populated.</summary>
public sealed record ConversationEntry(
    ConversationEntryKind Kind,
    ConversationParticipant Participant,
    string? Text = null,
    int? Attempt = null,
    ConversationApprovalOutcome? ApprovalOutcome = null,
    string? Feedback = null,
    Guid? ToolRequestId = null,
    string? ToolName = null,
    bool ToolCompleted = false,
    bool ToolIsError = false,
    ConversationEventKind? StatusKind = null,
    ConversationRunStatus? RunStatus = null,
    string? ArtifactId = null);

/// <summary>
/// Pure, dependency-free projection from a durable <see cref="ConversationEvent"/> stream to one
/// ordered group-chat transcript. Deduplicates strictly by EventId, so applying the same event
/// twice — a live/replay overlap, or a reconnected tail re-delivering an already-seen event —
/// never produces a second bubble/tool row. Entries are only ever appended or replaced in place
/// (never removed), so an index recorded for an in-progress typing indicator or tool row always
/// stays valid. Owns no HTTP, SSE parsing, or ConversationHost knowledge; Client Runtime's
/// ConversationRuntimeSession relays already-decoded ConversationEvent values here.
/// </summary>
public sealed class ConversationTranscript
{
    private readonly HashSet<Guid> _appliedEventIds = [];
    private readonly List<ConversationEntry> _entries = [];

    // The most recently appended typing/message entry's index for one (participant, attempt) —
    // used both to replace an open typing indicator with its completed message/error, and to
    // merge a contiguous run of same-participant/same-attempt messages into one visual bubble.
    private readonly Dictionary<(ConversationParticipant Participant, int Attempt), int> _lastEntryIndexByAttempt = [];

    // A tool row's index, so its matching ToolResult updates the same row instead of appending a
    // second one.
    private readonly Dictionary<Guid, int> _toolIndexByRequestId = [];

    private ConversationApprovalOutcome? _lastApprovalOutcome;
    private string? _lastApprovalFeedback;

    public long HighestSequence { get; private set; }

    public IReadOnlyList<ConversationEntry> Entries => _entries;

    public void Apply(ConversationEvent evt)
    {
        if (!_appliedEventIds.Add(evt.EventId))
            return;

        if (evt.Sequence > HighestSequence)
            HighestSequence = evt.Sequence;

        switch (evt.Kind)
        {
            case ConversationEventKind.UserMessage:
                _entries.Add(new ConversationEntry(ConversationEntryKind.UserMessage, evt.Participant, Text: evt.Text));
                break;
            case ConversationEventKind.ParticipantStarted:
                ApplyParticipantStarted(evt);
                break;
            case ConversationEventKind.ParticipantMessage:
                ApplyParticipantMessage(evt);
                break;
            case ConversationEventKind.Approval when evt.Approval is not null:
                ApplyApproval(evt);
                break;
            case ConversationEventKind.ToolRequested when evt.ToolRequest is not null:
                ApplyToolRequested(evt);
                break;
            case ConversationEventKind.ToolResult when evt.ToolResult is not null:
                ApplyToolResult(evt);
                break;
            case ConversationEventKind.RunStatus when evt.RunStatus is not null:
                ApplyRunStatus(evt);
                break;
            case ConversationEventKind.Error:
                ApplyError(evt);
                break;
            case ConversationEventKind.Artifact when evt.Artifact is not null:
                _entries.Add(new ConversationEntry(ConversationEntryKind.Status, evt.Participant,
                    StatusKind: evt.Kind, ArtifactId: evt.Artifact.ArtifactId));
                break;
        }
    }

    private void ApplyParticipantStarted(ConversationEvent evt)
    {
        _entries.Add(new ConversationEntry(ConversationEntryKind.Typing, evt.Participant, Attempt: evt.Attempt));
        _lastEntryIndexByAttempt[(evt.Participant, evt.Attempt ?? 0)] = _entries.Count - 1;
    }

    private void ApplyParticipantMessage(ConversationEvent evt)
    {
        var key = (evt.Participant, evt.Attempt ?? 0);
        if (_lastEntryIndexByAttempt.TryGetValue(key, out var index) && index == _entries.Count - 1)
        {
            var existing = _entries[index];
            var text = existing.Kind == ConversationEntryKind.ParticipantMessage
                ? $"{existing.Text}{evt.Text}"
                : evt.Text;
            _entries[index] = existing with { Kind = ConversationEntryKind.ParticipantMessage, Text = text };
            return;
        }

        _entries.Add(new ConversationEntry(ConversationEntryKind.ParticipantMessage, evt.Participant,
            Text: evt.Text, Attempt: evt.Attempt));
        _lastEntryIndexByAttempt[key] = _entries.Count - 1;
    }

    private void ApplyError(ConversationEvent evt)
    {
        var key = (evt.Participant, evt.Attempt ?? 0);
        if (_lastEntryIndexByAttempt.TryGetValue(key, out var index) && index == _entries.Count - 1
            && _entries[index].Kind == ConversationEntryKind.Typing)
        {
            _entries[index] = _entries[index] with { Kind = ConversationEntryKind.Status, StatusKind = evt.Kind, Text = evt.Reason };
            return;
        }

        _entries.Add(new ConversationEntry(ConversationEntryKind.Status, evt.Participant, StatusKind: evt.Kind, Text: evt.Reason));
    }

    private void ApplyApproval(ConversationEvent evt)
    {
        var approval = evt.Approval!;
        _entries.Add(new ConversationEntry(ConversationEntryKind.Approval, evt.Participant,
            ApprovalOutcome: approval.Outcome, Feedback: approval.Feedback));
        _lastApprovalOutcome = approval.Outcome;
        _lastApprovalFeedback = approval.Feedback;
    }

    private void ApplyToolRequested(ConversationEvent evt)
    {
        var request = evt.ToolRequest!;
        if (_toolIndexByRequestId.ContainsKey(request.RequestId))
            return; // EventId dedupe already prevents this; defensive only.

        _entries.Add(new ConversationEntry(ConversationEntryKind.ToolCall, evt.Participant,
            ToolRequestId: request.RequestId, ToolName: request.ToolName));
        _toolIndexByRequestId[request.RequestId] = _entries.Count - 1;
    }

    private void ApplyToolResult(ConversationEvent evt)
    {
        var result = evt.ToolResult!;
        if (_toolIndexByRequestId.TryGetValue(result.RequestId, out var index))
        {
            _entries[index] = _entries[index] with { ToolCompleted = true, ToolIsError = result.IsError };
            return;
        }

        // A result without a locally observed request (e.g. transcript replay starting after the
        // request but at/before the result) still needs to be visible.
        _entries.Add(new ConversationEntry(ConversationEntryKind.ToolCall, evt.Participant,
            ToolRequestId: result.RequestId, ToolCompleted: true, ToolIsError: result.IsError));
    }

    private void ApplyRunStatus(ConversationEvent evt)
    {
        var status = evt.RunStatus!.Value;
        if (status == ConversationRunStatus.Queued)
        {
            _lastApprovalOutcome = null;
            _lastApprovalFeedback = null;
        }

        var isNotApproved = status == ConversationRunStatus.Rejected
            || (status == ConversationRunStatus.Failed && _lastApprovalOutcome == ConversationApprovalOutcome.RevisionRequested);
        if (isNotApproved)
        {
            _entries.Add(new ConversationEntry(ConversationEntryKind.NotApproved, evt.Participant, Feedback: _lastApprovalFeedback));
            return;
        }

        _entries.Add(new ConversationEntry(ConversationEntryKind.Status, evt.Participant, StatusKind: evt.Kind, RunStatus: status));
    }
}
