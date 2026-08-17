using System.IO.Pipes;
using ForgeMission.Desktop.Contracts;
using ForgeMission.Desktop.Host;
using ForgeMission.Desktop.Photino;

// The disposable native host process. It owns a window, its own local Booting/Failed content, and
// the two inherited pipes the Desktop Supervisor handed it. It owns no runtime, no credential, and
// no cleanup: when this window closes, this process exits, and the Supervisor — which is still
// alive — observes that exit and cleans up everything it started.
//
// Deliberately synchronous, no `await` in this file: an `await` in top-level statements makes the
// compiler generate an async Main whose continuation resumes on a thread-pool thread, and macOS
// AppKit requires window/menu work on the real main thread. All asynchronous work lives on the
// command-reader thread below, which marshals back through the host adapter.
if (!TryParsePipeHandles(args, out var commandHandle, out var eventHandle))
{
    Console.Error.WriteLine(
        "Usage: ForgeMission.Desktop.Host --command-pipe <handle> --event-pipe <handle>. " +
        "This process is started by ForgeMission.Desktop; it is not launched directly.");
    return 2;
}

using var commands = new AnonymousPipeClientStream(PipeDirection.In, commandHandle);
using var events = new AnonymousPipeClientStream(PipeDirection.Out, eventHandle);

IDesktopHost host = new PhotinoDesktopHost();
host.RegisterRetryRequestedHandler(() => SendRetryRequested(events));
host.ShowLocalContent(HostContent.Booting);

var commandReader = new Thread(() => ReadCommands(commands, host))
{
    IsBackground = true,
    Name = "desktop-host-commands",
};
commandReader.Start();

// Blocks on the main thread until the user closes the window; returning from here ends the process.
host.Run();
return 0;

static bool TryParsePipeHandles(string[] args, out string commandHandle, out string eventHandle)
{
    commandHandle = "";
    eventHandle = "";

    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        switch (args[i])
        {
            case "--command-pipe":
                commandHandle = args[i + 1];
                break;
            case "--event-pipe":
                eventHandle = args[i + 1];
                break;
        }
    }

    return commandHandle.Length > 0 && eventHandle.Length > 0;
}

// One command at a time, applied to the window and nothing else. A null read means the Supervisor
// closed its end: this Host has no owner left, so it stops rather than lingering as an orphan
// window. Normal shutdown does not reach that path — the Supervisor terminates the Host as part of
// its own cleanup.
static void ReadCommands(Stream commands, IDesktopHost host)
{
    try
    {
        while (DesktopHostProtocol.ReadCommandAsync(commands, CancellationToken.None).GetAwaiter().GetResult()
               is { } command)
        {
            Apply(command, host);
        }
    }
    catch (Exception ex) when (ex is IOException or ObjectDisposedException)
    {
        // The Supervisor went away mid-frame; same outcome as a clean close below.
    }

    Environment.Exit(0);
}

static void Apply(DesktopHostCommand command, IDesktopHost host)
{
    switch (command.Kind)
    {
        case DesktopHostCommandKind.Navigate:
            host.Navigate(command.Payload);
            break;
        case DesktopHostCommandKind.ShowFailure:
            host.ShowLocalContent(HostContent.Failed(command.Payload));
            break;
    }
}

// Runs on the window's own thread, from the Retry click. A failed write means the Supervisor is
// already gone, which the command reader is about to observe as well.
static void SendRetryRequested(Stream events)
{
    try
    {
        DesktopHostProtocol
            .WriteAsync(events, new DesktopHostEvent(DesktopHostEventKind.RetryRequested), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
    catch (Exception ex) when (ex is IOException or ObjectDisposedException)
    {
    }
}
