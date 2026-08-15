using System.Collections.Concurrent;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.TransportHost;

internal sealed class ClientRuntimeSessionStore(ClientRuntimeEventHub events, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, ClientRuntimeSession> _sessions = [];
    private readonly Lazy<string> _defaultWorkspaceRoot = new(DefaultWorkspace.CreateNext);

    // replacesSessionId is the outgoing session's ID when the picker is replacing a still-live
    // session (a mission switch) rather than creating a workspace's first session. Removing and
    // disposing that entry first is what stops an abandoned Janus session's durable tail before
    // it can execute a later local tool — see ConversationRuntimeSession.DisposeAsync.
    public async Task<ClientRuntimeSession> CreateAsync(
        string workspaceRoot, string? mission = null,
        SessionRuntimeKind runtime = SessionRuntimeKind.Mission, string? replacesSessionId = null)
    {
        if (replacesSessionId is not null && _sessions.TryRemove(replacesSessionId, out var replaced))
            await replaced.Conversation.DisposeAsync();

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

    public Task<ClientRuntimeSession> CreateDefaultAsync(
        string? mission = null, SessionRuntimeKind runtime = SessionRuntimeKind.Mission) =>
        CreateAsync(_defaultWorkspaceRoot.Value, mission, runtime);

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
