namespace ForgeMission.Desktop.Host;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton<DesktopHostController>();
        return builder.Build();
    }
}
