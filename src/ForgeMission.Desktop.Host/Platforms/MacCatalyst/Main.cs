using UIKit;

namespace ForgeMission.Desktop.Host;

public static class Program
{
    private static HostStartupArguments? _startupArguments;

    internal static HostStartupArguments StartupArguments => _startupArguments
        ?? throw new InvalidOperationException("Mac Catalyst startup arguments have not been captured.");

    private static void Main(string[] args)
    {
        _startupArguments = new HostStartupArguments(args[1..]);
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
