using ForgeMission.ClientRuntime.Transport;

if (args.Length is < 3 or > 4)
    throw new ArgumentException("Usage: <base-url> <workspace-root> <file-name> [confirm]");

using var http = new HttpClient { BaseAddress = new Uri(args[0], UriKind.Absolute) };
var channel = new HttpClientRuntimeChannel(http);
// A session and its local execution root can only come from a Project (43.20 task 1), so this
// non-Desktop surface opens one the same way Desktop does — proof in itself that the contract is
// surface-neutral, since nothing here references Blazor or a Host.
var opened = await channel.SendAsync<ProjectCreateRequest, ProjectOperationResponse>(
    new ProjectCreateRequest("Client Runtime transport probe", HomePath: args[1]), CancellationToken.None);
var session = opened.Session
    ?? throw new InvalidOperationException(opened.Error?.Message ?? "Client Runtime returned no Project session.");

if (args.Length == 4 && args[3].Equals("confirm", StringComparison.OrdinalIgnoreCase))
{
    using var eventsCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    await using var events = channel.Subscribe(eventsCancellation.Token).GetAsyncEnumerator(eventsCancellation.Token);
    var confirmationEvent = events.MoveNextAsync().AsTask();
    await Task.Delay(100, eventsCancellation.Token);
    var dispatch = channel.SendAsync<CapabilityDispatchRequest, CapabilityDispatchResponse>(
        new CapabilityDispatchRequest(session.SessionId,
            new CapabilityRequestData("terminal", CapabilityOperation.ExecuteTerminal, Command: "echo confirmation-approved")),
        eventsCancellation.Token);

    var receivedConfirmation = false;
    while (await confirmationEvent)
    {
        if (events.Current.Kind == ClientRuntimeEventKind.ConfirmationRequest)
        {
            receivedConfirmation = true;
            break;
        }

        confirmationEvent = events.MoveNextAsync().AsTask();
    }

    if (!receivedConfirmation || events.Current.ConfirmationId is null)
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
