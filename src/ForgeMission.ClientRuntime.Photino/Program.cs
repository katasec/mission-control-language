using Photino.NET;

if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out var clientRuntimeUrl) ||
    clientRuntimeUrl.Scheme is not "http" and not "https")
{
    throw new ArgumentException("Usage: ForgeMission.ClientRuntime.Photino <client-runtime-url>");
}

var window = new PhotinoWindow()
    .SetTitle("Forge")
    .SetUseOsDefaultSize(true)
    .Center()
    .Load(clientRuntimeUrl.AbsoluteUri);

window.WaitForClose();
