using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ForgeMission.Desktop.Contracts;
using ForgeMission.Desktop.Photino;
using ForgeMission.Orchestration;
using Microsoft.Extensions.Configuration;

// Two ways to run: pass a Client Runtime URL explicitly (dev/test convenience — points at a
// Client Runtime already running elsewhere), or pass nothing and this process owns the Client
// Runtime's whole lifecycle (the real, double-click desktop experience — publish both projects
// into one folder and run only this one).
//
// Everything below is host-agnostic: it depends only on IDesktopHost (ForgeMission.Desktop.
// Contracts), never on Photino.NET types directly. The one place a concrete host is chosen is the
// `new PhotinoDesktopHost()` line below (ForgeMission.Desktop.Photino) — replacing the host means
// swapping that one line plus a new IDesktopHost implementation project, not this orchestration.
//
// Deliberately synchronous, no `await` anywhere in this file: an `await` in top-level statements
// makes the compiler generate an async Main, and the continuation after it resumes on a
// thread-pool thread, not the process's real main thread. macOS AppKit requires all window/menu
// operations happen on that real main thread — crossing threads here crashes with "API misuse:
// setting the main menu on a non-main thread" the moment the native window is constructed. Blocking
// synchronously (Task.Wait, not await) keeps everything on the one thread throughout.
Process? clientRuntime = null;
IMissionRuntimeLauncher? launcher = null;
string url;

if (args.Length == 1 && Uri.TryCreate(args[0], UriKind.Absolute, out var explicitUrl) &&
    explicitUrl.Scheme is "http" or "https")
{
    url = explicitUrl.AbsoluteUri;
}
else if (args.Length == 0)
{
    var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();
    var resolveTask = MissionRuntimeResolver.ResolveAsync(configuration);
    resolveTask.Wait();
    var (resolvedUrl, resolvedLauncher) = resolveTask.Result;
    launcher = resolvedLauncher;
    clientRuntime = StartClientRuntime(resolvedUrl);
    url = WaitForReadyUrl(clientRuntime);
}
else
{
    throw new ArgumentException("Usage: ForgeMission.Desktop [<client-runtime-url>]");
}

// A safety net for abrupt termination (SIGTERM, Ctrl+C) that skips past the normal WaitForClose()
// return below — otherwise the Client Runtime child is orphaned. AppDomain.ProcessExit does NOT
// reliably fire on an external `kill`/SIGTERM (confirmed by testing: the child was still orphaned
// after `kill -TERM` even with a ProcessExit handler registered) — PosixSignalRegistration is the
// mechanism that actually intercepts the signal before the process dies. Nothing can catch
// SIGKILL; that's a universal OS-level limitation, not specific to this process.
var signalRegistrations = clientRuntime is { } runtimeToClean
    ? new[]
    {
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
        {
            KillIfRunning(runtimeToClean);
            launcher?.DisposeAsync().AsTask().Wait();
        }),
        PosixSignalRegistration.Create(PosixSignal.SIGINT, _ =>
        {
            KillIfRunning(runtimeToClean);
            launcher?.DisposeAsync().AsTask().Wait();
        }),
    }
    : [];

// The one line that names a concrete host — Photino is today's implementation, not a fixed
// requirement. Swapping desktop hosts touches only this line and a new IDesktopHost
// implementation project; nothing else in this file should ever reference a concrete host type.
IDesktopHost host = new PhotinoDesktopHost();
host.Load(url);

// The closing handler, not the code after Run() below, is the reliable place to clean up the
// child process. Confirmed by testing: closing the last window on macOS tears the process down via
// native AppKit machinery before control ever returns to the managed code that follows Run() — the
// child was still orphaned after a real window close even with cleanup code sitting right after
// that call. Photino's underlying RegisterWindowClosingHandler runs its callback synchronously from
// native code before the close proceeds (confirmed against Photino.NET's own source: "Handler can
// return true to prevent the window from closing" — returning false here lets the close continue
// immediately after cleanup runs).
if (clientRuntime is { } runtimeToCleanOnClose)
{
    host.RegisterClosingHandler(() =>
    {
        KillIfRunning(runtimeToCleanOnClose);
        launcher?.DisposeAsync().AsTask().Wait();
        return false;
    });
}

host.Run();

// Belt-and-suspenders for whichever termination path this line does still run on (e.g. platforms
// where Run() returns normally) — a no-op everywhere else since KillIfRunning checks
// HasExited first.
if (clientRuntime is not null)
{
    KillIfRunning(clientRuntime);
    launcher?.DisposeAsync().AsTask().Wait();
}

// Graceful-first, hard-kill as a fallback. Confirmed live: a hard Process.Kill() sends SIGKILL on
// Unix, giving the Client Runtime's own Main() no chance to run its `await using var
// dockerMissionRuntime` disposal — every prior test left a real Docker container running
// (`docker ps` showed it, still healthy, well after the app had exited). ASP.NET Core's generic
// host handles SIGTERM by draining and shutting down gracefully, which is what lets Main()
// continue past `app.RunAsync()` and actually reach that disposal (stop + remove the container).
// Windows has no direct SIGTERM equivalent for another process without much more Win32 plumbing
// (matches this project's existing "Mac first, Windows/Linux validated periodically" priority) —
// falls straight to the hard kill there.
static void KillIfRunning(Process process)
{
    try
    {
        if (process.HasExited)
            return;

        if (!OperatingSystem.IsWindows())
        {
            using (var sigterm = Process.Start(new ProcessStartInfo("/bin/kill", ["-TERM", process.Id.ToString()])
            {
                UseShellExecute = false,
            }))
            {
                sigterm?.WaitForExit();
            }

            // Docker's own default stop grace period is 10s before it SIGKILLs the container
            // itself; give the graceful shutdown path at least that long before giving up on it.
            if (process.WaitForExit(TimeSpan.FromSeconds(10)))
                return;
        }

        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }
    catch (InvalidOperationException)
    {
        // Already exited between the check and the call — nothing to do.
    }
}

static Process StartClientRuntime(string missionRuntimeBaseUrl)
{
    var (fileName, dllArgument) = ResolveClientRuntimeCommand();
    var process = new Process
    {
        StartInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        },
        EnableRaisingEvents = true,
    };
    if (dllArgument is not null)
        process.StartInfo.ArgumentList.Add(dllArgument);

    process.StartInfo.EnvironmentVariables["MissionRuntime__BaseUrl"] = missionRuntimeBaseUrl;
    process.StartInfo.EnvironmentVariables["MissionRuntime__Credential"] = "local";

    process.Start();
    return process;
}

// Prefers the co-located native binary (the published, single-folder desktop app). Falls back to
// `dotnet <sibling project's dll>` for the standard bin/<Configuration>/<TFM> layout `dotnet run`
// produces, so the dev loop doesn't need a full publish for every iteration. If neither resolves,
// the caller gets a clear error rather than a silent hang.
static (string FileName, string? DllArgument) ResolveClientRuntimeCommand()
{
    var exeName = OperatingSystem.IsWindows() ? "ForgeMission.ClientRuntime.exe" : "ForgeMission.ClientRuntime";
    var nativePath = Path.Combine(AppContext.BaseDirectory, exeName);
    if (File.Exists(nativePath))
        return (nativePath, null);

    var tfmDir = new DirectoryInfo(AppContext.BaseDirectory);
    var srcDir = tfmDir.Parent?.Parent?.Parent?.Parent;
    var devDllPath = srcDir is null
        ? null
        : Path.Combine(srcDir.FullName, "ForgeMission.ClientRuntime", "bin",
            tfmDir.Parent!.Name, tfmDir.Name, "ForgeMission.ClientRuntime.dll");

    if (devDllPath is not null && File.Exists(devDllPath))
        return (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", devDllPath);

    throw new FileNotFoundException(
        "Could not find ForgeMission.ClientRuntime next to this executable, or as a sibling dev " +
        "build. Publish both projects into the same folder, or pass a Client Runtime URL " +
        $"explicitly. Looked for: {nativePath}" + (devDllPath is null ? "" : $" and {devDllPath}"));
}

// Blocks the calling thread on purpose — see the top-of-file note on why this can never be async.
static string WaitForReadyUrl(Process process)
{
    var readyUrl = new TaskCompletionSource<string>();
    var stderr = new StringBuilder();

    process.OutputDataReceived += (_, e) =>
    {
        if (e.Data?.StartsWith("FORGE_CLIENT_RUNTIME_URL=", StringComparison.Ordinal) == true)
            readyUrl.TrySetResult(e.Data["FORGE_CLIENT_RUNTIME_URL=".Length..]);
    };
    process.ErrorDataReceived += (_, e) =>
    {
        if (e.Data is not null)
            stderr.AppendLine(e.Data);
    };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    if (!readyUrl.Task.Wait(TimeSpan.FromSeconds(20)))
    {
        process.Kill(entireProcessTree: true);
        throw new InvalidOperationException($"Client Runtime did not start within 20s. {stderr}");
    }

    return readyUrl.Task.Result;
}
