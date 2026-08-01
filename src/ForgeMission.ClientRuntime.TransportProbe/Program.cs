using ForgeMission.ClientRuntime.Transport;

if (args.Length is < 3 or > 4)
    throw new ArgumentException("Usage: <base-url> <workspace-root> <file-name> [confirm]");

using var http = new HttpClient { BaseAddress = new Uri(args[0], UriKind.Absolute) };
var channel = new HttpClientRuntimeChannel(http);
var session = await channel.SendAsync<SessionSetupRequest, SessionSetupResponse>(
    new SessionSetupRequest(args[1]), CancellationToken.None);

if (args.Length == 4 && args[3].Equals("confirm", StringComparison.OrdinalIgnoreCase))
{
    using var eventsCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    await using var events = channel.Subscribe(eventsCancellation.Token).GetAsyncEnumerator(eventsCancellation.Token);
    await Task.Delay(100, eventsCancellation.Token);
    var dispatch = channel.SendAsync<CapabilityDispatchRequest, CapabilityDispatchResponse>(
        new CapabilityDispatchRequest(session.SessionId,
            new CapabilityRequestData("terminal", CapabilityOperation.ExecuteTerminal, Command: "echo confirmation-approved")),
        eventsCancellation.Token);

    while (await events.MoveNextAsync())
    {
        if (events.Current.Kind == ClientRuntimeEventKind.ConfirmationRequest)
            break;
    }
    if (events.Current.Kind != ClientRuntimeEventKind.ConfirmationRequest || events.Current.ConfirmationId is null)
        throw new InvalidOperationException("Client Runtime did not publish a confirmation request.");

    var confirmation = await channel.SendAsync<ConfirmationResponseRequest, ConfirmationResponse>(
        new ConfirmationResponseRequest(session.SessionId, events.Current.ConfirmationId, Approved: true), eventsCancellation.Token);
    if (!confirmation.Accepted)
        throw new InvalidOperationException("Client Runtime did not accept the confirmation response.");

    Console.WriteLine((await dispatch).Content);
    return;
}

var response = await channel.SendAsync<CapabilityDispatchRequest, CapabilityDispatchResponse>(
    new CapabilityDispatchRequest(session.SessionId,
        new CapabilityRequestData("file", CapabilityOperation.ReadFile, FilePath: args[2])), CancellationToken.None);
if (response.IsError)
    throw new InvalidOperationException(response.Content);
Console.WriteLine(response.Content);
