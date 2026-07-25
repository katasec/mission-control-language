using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ForgeMission.Core.Runtime;
using ForgeMission.Desktop.Services;
using ForgeMission.Desktop.ViewModels;
using ForgeMission.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Desktop;

public partial class App : Application
{
    private readonly ServiceProvider services = ConfigureServices();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(services.GetRequiredService<MainWindowViewModel>());
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IChatClientFactory, LocalKeyChatClientFactory>();
        serviceCollection.AddSingleton<VanillaMissionSessionFactory>();
        serviceCollection.AddSingleton<MainWindowViewModel>();
        return serviceCollection.BuildServiceProvider();
    }
}
