using System.Diagnostics;

namespace ForgeMission.Core.Tools;

public sealed class LocalDiskWorkspace : IWorkspace
{
    private readonly TimeSpan _timeout;

    public LocalDiskWorkspace(string root, TimeSpan? timeout = null)
        : this([root], timeout)
    {
    }

    public LocalDiskWorkspace(IEnumerable<string> roots, TimeSpan? timeout = null)
    {
        Roots = roots.Select(root => WorkspaceGuard.RealPath(Path.GetFullPath(root))).ToList();
        if (Roots.Count == 0)
            throw new ArgumentException("At least one workspace root is required.", nameof(roots));

        _timeout = timeout ?? TimeSpan.FromMinutes(5);
    }

    public IReadOnlyList<string> Roots { get; }

    public bool TryResolvePath(string path, out string resolved, out string? error) =>
        WorkspaceGuard.TryResolve(Roots, path, out resolved, out error);

    public Task<bool> ExistsAsync(string resolvedPath, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(resolvedPath));

    public Task<string> ReadFileAsync(string resolvedPath, CancellationToken ct = default) =>
        File.ReadAllTextAsync(resolvedPath, ct);

    public Task WriteFileAsync(string resolvedPath, string content, CancellationToken ct = default) =>
        File.WriteAllTextAsync(resolvedPath, content, ct);

    public async Task<ToolExecutionResult> ExecuteAsync(
        string command,
        string? workingDir = null,
        CancellationToken ct = default)
    {
        var resolvedWorkingDir = Roots[0];
        if (workingDir is not null && !TryResolvePath(workingDir, out resolvedWorkingDir, out var pathError))
            return ToolExecutionResult.Error(pathError!);

        var psi = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            WorkingDirectory       = resolvedWorkingDir,
        };
        psi.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        psi.ArgumentList.Add(command);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Error($"Failed to start command: {ex.Message}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return ToolExecutionResult.Error($"Command timed out after {_timeout.TotalSeconds:0}s: {command}");
        }

        var stdout  = await stdoutTask;
        var stderr  = await stderrTask;
        var combined = string.Join('\n', new[] { stdout, stderr }.Where(s => !string.IsNullOrEmpty(s)));

        return process.ExitCode == 0
            ? new ToolExecutionResult(combined)
            : ToolExecutionResult.Error($"Command exited with code {process.ExitCode}\n{combined}".TrimEnd());
    }
}
