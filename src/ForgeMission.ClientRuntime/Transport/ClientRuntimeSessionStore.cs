using System.Collections.Concurrent;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.TransportHost;

internal sealed class ClientRuntimeSessionStore(ClientRuntimeEventHub events, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, ClientRuntimeSession> _sessions = [];

    // The only way a first session and local execution root come into existence (43.20 task 1).
    // Its caller is always a project create/open endpoint, after ProjectStore has produced and
    // validated that home — which is what makes "no session or tool authority at Desktop boot"
    // structural rather than a rule Presentation has to remember.
    public ClientRuntimeSession CreateForProject(
        string projectHome, string? mission = null,
        SessionRuntimeKind runtime = SessionRuntimeKind.Mission) =>
        Open(projectHome, mission, runtime);

    // Replacement only: a mission switch inside an already-open Project. Removing and disposing the
    // outgoing entry first is what stops an abandoned Janus session's durable tail before it can
    // execute a later local tool — see ConversationRuntimeSession.DisposeAsync. Requiring the
    // outgoing session to exist, and its root to match, is what stops this path from quietly
    // becoming a second way to open an arbitrary folder.
    public async Task<ClientRuntimeSession> ReplaceAsync(
        string replacesSessionId, string workspaceRoot, string? mission = null,
        SessionRuntimeKind runtime = SessionRuntimeKind.Mission)
    {
        if (!_sessions.TryGetValue(replacesSessionId, out var replaced))
            throw new SessionReplacementRejectedException(
                "A session replacement must name the current session.");

        if (!string.Equals(replaced.Workspace.Root, workspaceRoot, StringComparison.Ordinal))
            throw new SessionReplacementRejectedException(
                "A session replacement must keep the open Project's home as its root.");

        _sessions.TryRemove(replacesSessionId, out _);
        await replaced.Conversation.DisposeAsync();
        // Every slot, or a replaced session's durable tail would outlive it.
        await replaced.MissionControl.DisposeAsync();
        await replaced.ProjectMission.DisposeAsync();
        return Open(workspaceRoot, mission, runtime);
    }

    private ClientRuntimeSession Open(
        string workspaceRoot, string? mission, SessionRuntimeKind runtime)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var confirmation = new PendingConfirmationHandler(sessionId, events);
        var state = new WorkspaceState();
        state.OpenFolder(workspaceRoot,
            new PolicyCapabilityAuthorizer(BuildPolicy(configuration)), confirmation);
        var session = new ClientRuntimeSession(sessionId, state, confirmation, mission, runtime);
        if (!_sessions.TryAdd(sessionId, session))
            throw new InvalidOperationException("Unable to create Client Runtime session.");
        return session;
    }

    public bool TryGet(string sessionId, out ClientRuntimeSession? session) => _sessions.TryGetValue(sessionId, out session);

    private static CapabilityAuthorizationPolicy BuildPolicy(IConfiguration configuration)
    {
        var terminal = ParseOutcome(configuration["Authorization:TerminalOutcome"]);
        var file = ParseOutcome(configuration["Authorization:FileOutcome"]);
        return new CapabilityAuthorizationPolicy(
        [
            new KeyValuePair<string, CapabilityAuthorizationRule>("file", new(file)),
            new KeyValuePair<string, CapabilityAuthorizationRule>("terminal", new(terminal)),
        ]);
    }

    private static AuthorizationOutcome ParseOutcome(string? value) =>
        Enum.TryParse<AuthorizationOutcome>(value, ignoreCase: true, out var outcome)
            ? outcome
            : AuthorizationOutcome.AutoApproved;
}

// A misuse or stale-race guard, not a Project domain outcome: no correct surface sends a
// replacement for a session that is gone or for a different root, so it fails loudly at the
// transport rather than being rendered as a normal state.
internal sealed class SessionReplacementRejectedException(string message) : Exception(message);

// Mission is fixed for the session's lifetime — switching missions starts a fresh session
// (43.3 task 3) rather than mutating this one, so no attached mission ever changes mid-conversation.
// Runtime is likewise fixed at creation. Conversation is only ever populated for
// SessionRuntimeKind.DurableConversation, lazily on the first durable prompt.
internal sealed record ClientRuntimeSession(
    string Id,
    WorkspaceState Workspace,
    PendingConfirmationHandler Confirmation,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission)
{
    public ConversationSessionSlot Conversation { get; } = new();

    /// <summary>The Project's Mission Control conversation (43.20 task 2) — project-scoped, so it
    /// is deliberately a separate slot from the mission-scoped Janus one above. A mission switch
    /// replaces the session but keeps the same Project home, so the replacement simply reopens the
    /// same durable control conversation from the manifest.
    ///
    /// LEGACY: superseded by <see cref="ProjectMission"/> (43.21 task 1) and removed by 43.21
    /// task 3.</summary>
    public ProjectSessionSlot<ProjectControlRuntimeSession> MissionControl { get; } = new();

    /// <summary>The Project's Mission container (43.21 task 1) — the universal invocation path.
    /// Serialized by the same slot type and for the same reason as the control one above: open,
    /// start, and disposal must not interleave, so a concurrent session replacement can never
    /// leave an orphaned tail the store no longer tracks.</summary>
    public ProjectSessionSlot<ProjectMissionRuntimeSession> ProjectMission { get; } = new();
}

/// <summary>
/// The project-scoped equivalent of <see cref="ConversationSessionSlot"/>, and serialized for the
/// same reason: open, submit, and disposal must not interleave. One gate means a concurrent
/// session replacement can never leave an orphaned tail the store no longer tracks — whichever of
/// admission or disposal reaches the gate first decides the outcome for the other.
/// Serializing every call (not only the first) also makes two concurrent opens resolve correctly:
/// the second finds the conversation already opened and returns it without a second create.
///
/// Generic since 43.21 task 1 so the legacy control session and the Project Mission session share
/// ONE implementation of this lifecycle rather than two copies that could diverge on the very
/// races it exists to close.
/// </summary>
internal sealed class ProjectSessionSlot<TSession> : IAsyncDisposable where TSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TSession? _session;
    private bool _closed;

    public async Task<TResult> InvokeAsync<TResult>(
        Func<TSession> factory,
        Func<TSession, Task<TResult>> operation,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_closed)
                throw new InvalidOperationException("This Client Runtime session has been replaced.");

            _session ??= factory();
            return await operation(_session);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs <paramref name="operation"/> only if this slot has already been opened — a
    /// submit must never lazily create a session and skip the open path's manifest read and
    /// replay. <paramref name="notOpened"/> supplies the caller's own typed exception, so each
    /// endpoint keeps mapping its own failure rather than sharing one that means less.</summary>
    public async Task<TResult> InvokeOpenedAsync<TResult>(
        Func<TSession, Task<TResult>> operation, Func<Exception> notOpened, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_closed)
                throw new InvalidOperationException("This Client Runtime session has been replaced.");
            if (_session is null)
                throw notOpened();

            return await operation(_session);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        TSession? toDispose;
        await _gate.WaitAsync();
        try
        {
            if (_closed)
                return; // Idempotent, mirroring ConversationSessionSlot.

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

// The sole entry point for a durable Client Runtime session's prompt lifecycle. Admission, lazy
// ConversationRuntimeSession creation, and SendAsync are one operation serialized by _gate — never
// exposed as separate GetOrCreate/SendAsync steps a caller could interleave with disposal. This
// closes a real race: a /transport/prompt request can call ClientRuntimeSessionStore.TryGet and
// obtain this slot's owning ClientRuntimeSession just before a mission switch replaces and
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
    private ConversationRuntimeSession? _session;
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

    public async ValueTask DisposeAsync()
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
