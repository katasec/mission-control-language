using System.Diagnostics;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime;

/// <summary>
/// The sole terminal provider admitted for a mission profile. On macOS it starts every command
/// under sandbox-exec's deny-default profile: the Project is the only writable/readable user
/// location, network is absent, and the child receives a fixed minimal environment.  On platforms
/// without an equivalent OS boundary this provider is unavailable rather than falling back to a
/// working-directory convention.
/// </summary>
internal sealed class ProjectBoundTerminalProvider : ITerminalProvider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private readonly string _root;

    public ProjectBoundTerminalProvider(string root)
    {
        _root = Path.GetFullPath(root);
        if (!OperatingSystem.IsMacOS() || !File.Exists("/usr/bin/sandbox-exec"))
            throw new PlatformNotSupportedException(
                "ProjectWorkspaceAndTerminal requires the macOS sandbox-exec containment boundary.");
    }

    public string CapabilityName => "terminal";

    public async Task<ToolExecutionResult> ExecuteAsync(string command, string? workingDir = null, CancellationToken ct = default)
    {
        var directory = workingDir is null ? _root : ResolveWorkingDirectory(workingDir);
        if (directory is null)
            return ToolExecutionResult.Error("Terminal working directory is outside the Project workspace.");

        var start = new ProcessStartInfo("/usr/bin/sandbox-exec")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };
        start.Environment.Clear();
        start.Environment["PATH"] = "/usr/bin:/bin";
        start.Environment["HOME"] = _root;
        start.ArgumentList.Add("-p");
        start.ArgumentList.Add(ProfileFor(_root));
        start.ArgumentList.Add("/bin/sh");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(command);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Exception ex) { return ToolExecutionResult.Error($"Failed to start contained command: {ex.Message}"); }

        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return ToolExecutionResult.Error($"Contained command timed out after {Timeout.TotalSeconds:0}s.");
        }

        var combined = string.Join('\n', new[] { await output, await error }.Where(value => !string.IsNullOrEmpty(value)));
        return process.ExitCode == 0
            ? new ToolExecutionResult(combined)
            : ToolExecutionResult.Error($"Contained command exited with code {process.ExitCode}\n{combined}".TrimEnd());
    }

    private string? ResolveWorkingDirectory(string relative)
    {
        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        return candidate == _root || candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? candidate
            : null;
    }

    private static string ProfileFor(string root)
    {
        var roots = new[] { root, MacPhysicalAlias(root) }
            .Distinct(StringComparer.Ordinal)
            .Select(path => path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal))
            .ToArray();
        var permittedRoots = string.Join(" ", roots.Select(path => $"(subpath \"{path}\")"));
        return $"(version 1)\n" +
               // Apple supplies the loader/runtime operation definitions through system.sb;
               // deny-default below still owns all grants made to the child.
               "(import \"system.sb\")\n" +
               "(deny default)\n" +
               "(allow process-fork)\n" +
               // The macOS dynamic loader may execute an architecture-specific image outside
               // the lexical /bin path. Filesystem, network, Mach service, and environment
               // grants remain deny-default, so this permits child processes without granting
               // them an escape from the sandbox.
               "(allow process-exec)\n" +
               "(allow file-read* (subpath \"/bin\") (subpath \"/usr/bin\") (subpath \"/usr/lib\") (subpath \"/System\") (subpath \"/dev\"))\n" +
               $"(allow file-read* {permittedRoots})\n" +
               $"(allow file-write* {permittedRoots})\n" +
               "(allow file-write* (literal \"/dev/null\"))\n" +
               "(allow sysctl-read)\n";
    }

    // macOS presents /tmp and /var through compatibility aliases while sandbox decisions use
    // their /private paths. Both names refer to the same Project directory, not a second grant.
    private static string MacPhysicalAlias(string root) => root switch
    {
        var path when path.StartsWith("/var/", StringComparison.Ordinal) => "/private" + path,
        var path when path.StartsWith("/tmp/", StringComparison.Ordinal) => "/private" + path,
        _ => root,
    };
}
