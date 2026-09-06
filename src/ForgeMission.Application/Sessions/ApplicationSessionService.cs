using System.Collections.Concurrent;
using ForgeMission.Application.Transport;
using ForgeMission.ClientRuntime;
using ForgeMission.Core.Tools;

namespace ForgeMission.Application;

internal sealed class ApplicationSessionService(
    CapabilityAuthorizationPolicy policy, Action<ApplicationEvent> publish, CancellationToken applicationStopping)
{
    private readonly ConcurrentDictionary<string, ApplicationSession> _sessions = [];
    private readonly object _lifecycleGate = new();
    private Task _operations = Task.CompletedTask;
    private Task? _dispose;
    private bool _closing;

    // The only way a first session and local execution root come into existence (43.20 task 1).
    // Its caller is always a project create/open endpoint, after ProjectStore has produced and
    // validated that home — which is what makes "no session or tool authority at Desktop boot"
    // structural rather than a rule Presentation has to remember.
    public ApplicationSession CreateForProject(
        string projectHome, string? mission = null,
        SessionRuntimeKind runtime = SessionRuntimeKind.Mission)
    {
        lock (_lifecycleGate)
        {
            ThrowIfClosing();
            return Open(projectHome, mission, runtime);
        }
    }

    // Replacement only: a mission switch inside an already-open Project. Removing and disposing the
    // outgoing entry first is what stops an abandoned Janus session's durable tail before it can
    // execute a later local tool — see ConversationRuntimeSession.DisposeAsync. Requiring the
    // outgoing session to exist, and its root to match, is what stops this path from quietly
    // becoming a second way to open an arbitrary folder.
    public Task<ApplicationSession> ReplaceAsync(
        string replacesSessionId, string workspaceRoot, string? mission = null,
        SessionRuntimeKind runtime = SessionRuntimeKind.Mission)
    {
        lock (_lifecycleGate)
        {
            ThrowIfClosing();
            if (!_sessions.TryGetValue(replacesSessionId, out var replaced))
                throw new SessionReplacementRejectedException(
                    "A session replacement must name the current session.");

            if (!string.Equals(replaced.ProjectHome, workspaceRoot, StringComparison.Ordinal))
                throw new SessionReplacementRejectedException(
                    "A session replacement must keep the open Project's home as its root.");

            if (!((ICollection<KeyValuePair<string, ApplicationSession>>)_sessions)
                    .Remove(new KeyValuePair<string, ApplicationSession>(replacesSessionId, replaced)))
                throw new SessionReplacementRejectedException(
                    "A session replacement must name the current session.");

            var replacement = ReplaceCoreAsync(replaced, workspaceRoot, mission, runtime);
            _operations = Task.WhenAll(_operations, replacement);
            return replacement;
        }
    }

    private async Task<ApplicationSession> ReplaceCoreAsync(
        ApplicationSession replaced, string workspaceRoot, string? mission, SessionRuntimeKind runtime)
    {
        await replaced.DisposeAsync();
        lock (_lifecycleGate)
        {
            ThrowIfClosing();
            return Open(workspaceRoot, mission, runtime);
        }
    }

    private ApplicationSession Open(
        string workspaceRoot, string? mission, SessionRuntimeKind runtime)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var confirmation = new PendingConfirmationHandler(sessionId, publish);
        var execution = ClientExecutionSession.Create(workspaceRoot, policy, confirmation, applicationStopping);
        var session = new ApplicationSession(sessionId, workspaceRoot, execution, confirmation, mission, runtime);
        if (!_sessions.TryAdd(sessionId, session))
            throw new InvalidOperationException("Unable to create Client Runtime session.");
        return session;
    }

    public bool TryGet(string sessionId, out ApplicationSession? session) => _sessions.TryGetValue(sessionId, out session);

    public ValueTask DisposeAsync()
    {
        lock (_lifecycleGate)
        {
            if (_dispose is null)
            {
                _closing = true;
                var sessions = _sessions.Values.ToArray();
                _sessions.Clear();
                _dispose = DisposeCoreAsync(sessions, _operations);
            }

            return new ValueTask(_dispose);
        }
    }

    private static async Task DisposeCoreAsync(IReadOnlyCollection<ApplicationSession> sessions, Task operations)
    {
        try
        {
            await Task.WhenAll(sessions.Select(session => session.DisposeAsync().AsTask()).Append(operations));
        }
        catch (SessionReplacementRejectedException)
        {
            // A replacement that was already draining when shutdown closed admission has correctly
            // joined that drain. Its stale open is an expected shutdown outcome, not a failed close.
        }
    }

    private void ThrowIfClosing()
    {
        if (_closing)
            throw new SessionReplacementRejectedException("The application session service is closing.");
    }
}

// A misuse or stale-race guard, not a Project domain outcome: no correct surface sends a
// replacement for a session that is gone or for a different root, so it fails loudly at the
// transport rather than being rendered as a normal state.
public sealed class SessionReplacementRejectedException(string message) : Exception(message);

// Mission is fixed for the session's lifetime — switching missions starts a fresh session
// (43.3 task 3) rather than mutating this one, so no attached mission ever changes mid-conversation.
// Runtime is likewise fixed at creation. Conversation is only ever populated for
// SessionRuntimeKind.DurableConversation, lazily on the first durable prompt.
internal sealed record ApplicationSession(
    string Id,
    string ProjectHome,
    ClientExecutionSession Execution,
    PendingConfirmationHandler Confirmation,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission)
{
    private readonly object _disposeGate = new();
    private Task? _dispose;

    public ConversationSessionSlot Conversation { get; } = new();

    /// <summary>One bounded Project Mission read owner per live session. It has no workspace
    /// capability dependency and is disposed with the same replacement boundary as the legacy
    /// conversation slots.</summary>
    public ProjectMissionReadSessionSlot ProjectMission { get; } = new();

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            _dispose ??= DisposeCoreAsync();
            return new ValueTask(_dispose);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await Conversation.DisposeAsync();
        await ProjectMission.DisposeAsync();
        await Execution.DisposeAsync();
    }
}

// The sole entry point for a durable Client Runtime session's prompt lifecycle. Admission, lazy
// ConversationRuntimeSession creation, and SendAsync are one operation serialized by _gate — never
// exposed as separate GetOrCreate/SendAsync steps a caller could interleave with disposal. This
// closes a real race: a /transport/prompt request can call ApplicationSessionService.TryGet and
// obtain this slot's owning ApplicationSession just before a mission switch replaces and
// disposes that same session. Without one serialized lifecycle, that prompt could still create
// and start a ConversationRuntimeSession after replacement — an orphaned durable session and tail
// the store no longer tracks or can ever dispose, free to execute a local tool after the user
// switched away. With SendPromptAsync as the only entry point: whichever of admission or
// disposal reaches _gate first decides the outcome for the other. If disposal wins, it closes the
// slot before the prompt is admitted, and the prompt is rejected outright — no Host call, no
// session, no tail. If the prompt wins, disposal blocks on the same gate until that admitted call
// returns, then disposes whatever session it just created — so a session is never left running
// after Dispose returns, in either ordering. Serializing every call (not only the first) through
// one gate also makes two concurrent "first" prompts resolve correctly: the second can only enter
// after the first has already set the session's ConversationId, so it takes the follow-up branch
// instead of starting a second conversation.
internal sealed class ConversationSessionSlot : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeGate = new();
    private ConversationRuntimeSession? _session;
    private Task? _dispose;
    private bool _closed;

    public async Task<Guid> SendPromptAsync(
        Func<ConversationRuntimeSession> factory, string prompt, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_closed)
                throw new InvalidOperationException("This Client Runtime session has been replaced.");

            _session ??= factory();
            return await _session.SendAsync(prompt, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            _dispose ??= DisposeCoreAsync();
            return new ValueTask(_dispose);
        }
    }

    private async Task DisposeCoreAsync()
    {
        ConversationRuntimeSession? toDispose;
        await _gate.WaitAsync();
        try
        {
            if (_closed)
                return; // Idempotent: a prior DisposeAsync already closed (and, if needed, disposed) this slot.

            _closed = true;
            toDispose = _session;
        }
        finally
        {
            _gate.Release();
        }

        if (toDispose is not null)
            await toDispose.DisposeAsync();
    }
}
