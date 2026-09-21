namespace ForgeMission.Desktop.Host;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp(HostStartupArguments? startupArguments = null)
    {
        startupArguments ??= new HostStartupArguments(Environment.GetCommandLineArgs()[1..]);

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton(startupArguments);
        builder.Services.AddSingleton<DesktopHostController>();
        return builder.Build();
    }
}
