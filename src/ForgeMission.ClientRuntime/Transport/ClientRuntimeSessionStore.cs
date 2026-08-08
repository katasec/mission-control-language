using System.Collections.Concurrent;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.TransportHost;

internal sealed class ClientRuntimeSessionStore(ClientRuntimeEventHub events, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, ClientRuntimeSession> _sessions = [];

    public ClientRuntimeSession Create(string workspaceRoot, string? mission = null)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var confirmation = new PendingConfirmationHandler(sessionId, events);
        var state = new WorkspaceState();
        state.OpenFolder(workspaceRoot,
            new PolicyCapabilityAuthorizer(BuildPolicy(configuration)), confirmation);
        var session = new ClientRuntimeSession(sessionId, state, confirmation, mission);
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

// Mission is fixed for the session's lifetime — switching missions starts a fresh session
// (43.3 task 3) rather than mutating this one, so no attached mission ever changes mid-conversation.
internal sealed record ClientRuntimeSession(
    string Id,
    WorkspaceState Workspace,
    PendingConfirmationHandler Confirmation,
    string? Mission = null);
