using ForgeMission.Application.Transport;

namespace ForgeMission.Application;

/// <summary>Settles only the matching pending confirmation for an open Application session.</summary>
internal sealed class InteractionService(ApplicationSessionService sessions) : IInteractionService
{
    public ConfirmationResponse Respond(ConfirmationResponseRequest request)
    {
        var accepted = sessions.TryGet(request.SessionId, out var session)
            && session is not null
            && session.Confirmation.Resolve(request.ConfirmationId, request.Approved);
        return new ConfirmationResponse(accepted);
    }
}
