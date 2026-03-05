using ACRLiverManager.App.ViewModels;
using AcrLiveryManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ACRLiverManager.App.Services;

public static class ServiceRegistry
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IGamePathService, GamePathService>();
        services.AddSingleton<IInstallRegistryService, InstallRegistryService>();
        services.AddSingleton<ILiveryRepositoryService, LiveryRepositoryService>();
        services.AddSingleton<ILiveryInstallerService, LiveryInstallerService>();
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IArchiveImportService, ArchiveImportService>();

        // VMs
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}