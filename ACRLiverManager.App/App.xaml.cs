using System.Windows;
using ACRLiverManager.App.Services;
using ACRLiverManager.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ACRLiverManager.App;

public partial class App : Application
{
    private readonly IServiceProvider _sp = ServiceRegistry.Build();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WinSparkleUpdater.Initialize();

        var vm = _sp.GetRequiredService<MainViewModel>();

        var wnd = new MainWindow { DataContext = vm };
        wnd.Show();
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        WinSparkleUpdater.Cleanup();
        base.OnExit(e);
    }
}