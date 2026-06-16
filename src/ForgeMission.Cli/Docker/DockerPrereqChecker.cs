using System.Net;
using System.Net.Sockets;
using Spectre.Console;

namespace ForgeMission.Cli.Docker;

public static class DockerPrereqChecker
{
    public static async Task<PrereqCheck> CheckDockerAsync()
    {
        var (ok, detail) = await DockerCli.GetVersionAsync();
        return ok
            ? new PrereqCheck("Docker", PrereqStatus.Pass, detail)
            : new PrereqCheck("Docker", PrereqStatus.Fail, detail);
    }

    public static PrereqCheck CheckPort(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return new PrereqCheck($"Port {port}", PrereqStatus.Pass, $"port {port} available");
        }
        catch (SocketException)
        {
            return new PrereqCheck($"Port {port}", PrereqStatus.Fail, $"port {port} already in use");
        }
    }

    public static PrereqCheck CheckFileExists(string path, string label)
    {
        if (File.Exists(path))
            return new PrereqCheck(label, PrereqStatus.Pass, path);
        return new PrereqCheck(label, PrereqStatus.Fail, $"{path} not found");
    }

    public static bool RunAndPrint(IEnumerable<PrereqCheck> checks)
    {
        AnsiConsole.MarkupLine("\n [bold]Checking prerequisites...[/]\n");

        var table = new Table();
        table.AddColumn("Requirement");
        table.AddColumn("Status");
        table.AddColumn("Detail");
        table.Border(TableBorder.Simple);

        var results = new List<PrereqCheck>();
        bool failed = false;

        foreach (var check in checks)
        {
            if (failed)
                results.Add(check with { Status = PrereqStatus.Skipped, Detail = "–" });
            else
            {
                results.Add(check);
                if (check.Status == PrereqStatus.Fail)
                    failed = true;
            }
        }

        foreach (var r in results)
        {
            var (statusMarkup, detailMarkup) = r.Status switch
            {
                PrereqStatus.Pass    => ("[green]✓ pass[/]", r.Detail),
                PrereqStatus.Fail    => ("[red]✗ fail[/]", r.Detail),
                PrereqStatus.Skipped => ("[grey]– skip[/]", "[grey]–[/]"),
                _                    => ("?", r.Detail)
            };
            table.AddRow(r.Label, statusMarkup, detailMarkup);
        }

        AnsiConsole.Write(table);

        if (failed)
            AnsiConsole.MarkupLine("[red]Prerequisites not met. Cannot continue.[/]");

        return !failed;
    }
}
