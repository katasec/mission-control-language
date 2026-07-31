using System.Net;
using System.Net.Sockets;

namespace ForgeMission.Docker;

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

    public static IReadOnlyList<PrereqCheck> Evaluate(IEnumerable<PrereqCheck> checks)
    {
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

        return results;
    }
}
