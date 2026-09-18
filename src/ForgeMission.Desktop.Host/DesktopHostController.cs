using System.IO.Pipes;
using ForgeMission.Desktop.Contracts;

namespace ForgeMission.Desktop.Host;

// Owns only the inherited-pipe protocol. MAUI owns the native app loop and MauiDesktopHost owns
// UI-thread dispatch; keeping those responsibilities separate preserves the fixed protocol.
public sealed class DesktopHostController
{
    public void Start(IDesktopHost host)
    {
        if (!TryParsePipeHandles(Environment.GetCommandLineArgs()[1..], out var commandHandle, out var eventHandle))
        {
            host.ShowLocalContent(HostContent.Failed(
                "This process is started by ForgeMission.Desktop and requires inherited pipe handles."));
            return;
        }

        var commands = new AnonymousPipeClientStream(PipeDirection.In, commandHandle);
        var events = new AnonymousPipeClientStream(PipeDirection.Out, eventHandle);
        host.RegisterRetryRequestedHandler(() => SendRetryRequested(events));
        host.ShowLocalContent(HostContent.Booting);

        var commandReader = new Thread(() => ReadCommands(commands, host))
        {
            IsBackground = true,
            Name = "desktop-host-commands",
        };
        commandReader.Start();
    }

    private static bool TryParsePipeHandles(string[] args, out string commandHandle, out string eventHandle)
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

    private static void ReadCommands(Stream commands, IDesktopHost host)
    {
        using (commands)
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
                // The Supervisor went away mid-frame; the Host must not linger as an orphan.
            }
        }

        Environment.Exit(0);
    }

    private static void Apply(DesktopHostCommand command, IDesktopHost host)
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

    private static void SendRetryRequested(Stream events)
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
            // The command reader will observe the closed Supervisor pipe and end this process.
        }
    }
}
