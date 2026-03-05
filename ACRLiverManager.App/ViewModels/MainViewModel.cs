using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using ACRLiverManager.App.Commands;
using ACRLiverManager.App.Services;
using AcrLiveryManager.Core.Models;
using AcrLiveryManager.Core.Services;

namespace ACRLiverManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IGamePathService _gamePaths;
    private readonly ILiveryRepositoryService _repo;
    private readonly ILiveryInstallerService _installer;
    private readonly IFolderPicker _folderPicker;
    private readonly IAppSettingsService _settingsService;

    private string _gameRoot = "";
    private string _installerRoot = "";
    private string _statusText = "Ready.";
    private bool _canWriteToPaks;

    public ObservableCollection<CarVm> Cars { get; } = [];
    public ObservableCollection<LiveryVm> Liveries { get; } = [];

    private CarVm? _selectedCar;

    public CarVm? SelectedCar
    {
        get => _selectedCar;
        set
        {
            if (!SetField(ref _selectedCar, value)) return;
            _ = LoadLiveriesForSelectedCarAsync();
        }
    }

    private LiveryVm? _selectedLivery;

    public LiveryVm? SelectedLivery
    {
        get => _selectedLivery;
        set
        {
            if (!SetField(ref _selectedLivery, value)) return;
            RefreshCommandStates();
        }
    }

    public string GameRoot
    {
        get => _gameRoot;
        set
        {
            if (!SetField(ref _gameRoot, value)) return;
            EvaluatePaksWriteAccess();
            RefreshCommandStates();
        }
    }

    public string InstallerRoot
    {
        get => _installerRoot;
        set
        {
            if (!SetField(ref _installerRoot, value)) return;
            RefreshCommandStates();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public AsyncRelayCommand RescanCommand { get; }
    public AsyncRelayCommand InstallCommand { get; }
    public AsyncRelayCommand ActivateCommand { get; }
    public AsyncRelayCommand DisableCommand { get; }
    public AsyncRelayCommand UninstallCommand { get; }

    public RelayCommand BrowseGameRootCommand { get; }

    public RelayCommand BrowseInstallerRootCommand { get; }

    public MainViewModel(
        IGamePathService gamePaths,
        ILiveryRepositoryService repo,
        ILiveryInstallerService installer,
        IFolderPicker folderPicker,
        IAppSettingsService settingsService)
    {
        _gamePaths = gamePaths;
        _repo = repo;
        _installer = installer;
        _folderPicker = folderPicker;
        _settingsService = settingsService;

        BrowseGameRootCommand = new RelayCommand(BrowseGameRoot);
        BrowseInstallerRootCommand = new RelayCommand(BrowseInstallerRoot);

        RescanCommand = new AsyncRelayCommand(RescanAsync, () => Directory.Exists(InstallerRoot));
        InstallCommand = new AsyncRelayCommand(InstallAsync, CanOperateOnSelectedLivery);
        ActivateCommand = new AsyncRelayCommand(ActivateAsync, CanOperateOnSelectedLivery);
        DisableCommand = new AsyncRelayCommand(DisableAsync, CanOperateOnSelectedLivery);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, CanOperateOnSelectedLivery);

        _ = LoadSettingsAsync();
    }

    private bool CanOperateOnSelectedLivery()
        => SelectedLivery is not null
           && Directory.Exists(InstallerRoot)
           && _gamePaths.IsValidGameRoot(GameRoot)
           && _canWriteToPaks;

    private void EvaluatePaksWriteAccess()
    {
        _canWriteToPaks = false;

        if (!_gamePaths.IsValidGameRoot(GameRoot))
        {
            StatusText = "Select a valid game root (must contain acr\\Content\\Paks).";
            return;
        }

        if (_gamePaths.CanWriteToPaks(GameRoot, out var reason))
        {
            _canWriteToPaks = true;
            StatusText = "Ready.";
        }
        else
        {
            StatusText = reason ?? "Cannot write to Paks folder.";
        }
    }

    private void RefreshCommandStates()
    {
        RescanCommand.RaiseCanExecuteChanged();
        InstallCommand.RaiseCanExecuteChanged();
        ActivateCommand.RaiseCanExecuteChanged();
        DisableCommand.RaiseCanExecuteChanged();
        UninstallCommand.RaiseCanExecuteChanged();
    }

    private async Task RescanAsync()
    {
        try
        {
            StatusText = "Scanning cars...";
            Cars.Clear();
            Liveries.Clear();

            var cars = await _repo.ScanCarsAsync(InstallerRoot);
            foreach (var c in cars)
                Cars.Add(new CarVm { CarId = c.CarId, DisplayName = c.DisplayName });

            StatusText = Cars.Count == 0
                ? "No cars found under installer root."
                : $"Found {Cars.Count} cars. Select one.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    private async Task LoadLiveriesForSelectedCarAsync()
    {
        try
        {
            Liveries.Clear();
            SelectedLivery = null;

            if (SelectedCar is null) return;

            StatusText = $"Scanning liveries for {SelectedCar.DisplayName}...";
            var pkgs = await _repo.ScanLiveriesForCarAsync(InstallerRoot, SelectedCar.CarId);

            foreach (var p in pkgs)
            {
                var vm = new LiveryVm
                {
                    CarId = p.CarId,
                    LiveryId = p.LiveryId,
                    DisplayName = p.DisplayName,
                    BaseName = p.BaseName,
                    SourceFolder = p.SourceFolder,
                    PreviewPath = p.PreviewPath
                };

                vm.PreviewImage = TryLoadPreview(vm.PreviewPath);
                await RefreshInstallStateAsync(vm, p);

                Liveries.Add(vm);
            }

            StatusText = pkgs.Count == 0 ? "No valid liveries found for this car." : $"Found {pkgs.Count} liveries.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load liveries: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    private async Task RefreshInstallStateAsync(LiveryVm vm, LiveryPackage pkg)
    {
        if (!_gamePaths.IsValidGameRoot(GameRoot)) return;

        var state = await _installer.GetStateAsync(pkg, GameRoot);
        vm.InstallState.IsInstalled = state.IsInstalled;
        vm.InstallState.IsEnabled = state.IsEnabled;
    }

    private static BitmapImage? TryLoadPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private LiveryPackage? GetSelectedPackage()
    {
        if (SelectedCar is null || SelectedLivery is null) return null;

        // Rebuild package from VM + known folder structure (scanner already validated it).
        // For correctness you’d cache the LiveryPackage list; this is MVP-friendly.
        var folder = SelectedLivery.SourceFolder;

        var pak = Directory.EnumerateFiles(folder, "*.pak").FirstOrDefault();
        var utoc = Directory.EnumerateFiles(folder, "*.utoc").FirstOrDefault();
        var ucas = Directory.EnumerateFiles(folder, "*.ucas").FirstOrDefault();
        if (pak is null || utoc is null || ucas is null) return null;

        var baseName = Path.GetFileNameWithoutExtension(pak);

        return new LiveryPackage(
            CarId: SelectedCar.CarId,
            LiveryId: SelectedLivery.LiveryId,
            DisplayName: SelectedLivery.DisplayName,
            SourceFolder: folder,
            BaseName: baseName,
            PakPath: pak,
            UtocPath: utoc,
            UcasPath: ucas,
            PreviewPath: SelectedLivery.PreviewPath
        );
    }

    private async Task InstallAsync()
    {
        var pkg = GetSelectedPackage();
        if (pkg is null)
        {
            StatusText = "Invalid livery selection.";
            return;
        }

        try
        {
            StatusText = "Installing...";
            await _installer.InstallAsync(pkg, GameRoot, enableAfterInstall: true);
            StatusText = "Installed.";
            await LoadLiveriesForSelectedCarAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Install failed: {ex.Message}";
        }
    }

    private async Task ActivateAsync()
    {
        var pkg = GetSelectedPackage();
        if (pkg is null)
        {
            StatusText = "Invalid livery selection.";
            return;
        }

        try
        {
            StatusText = "Activating (disabling others for this car)...";
            await _installer.ActivateForCarAsync(pkg, GameRoot);
            StatusText = "Activated.";
            await LoadLiveriesForSelectedCarAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Activate failed: {ex.Message}";
        }
    }

    private async Task DisableAsync()
    {
        var pkg = GetSelectedPackage();
        if (pkg is null)
        {
            StatusText = "Invalid livery selection.";
            return;
        }

        try
        {
            StatusText = "Disabling...";
            await _installer.DisableAsync(pkg, GameRoot);
            StatusText = "Disabled.";
            await LoadLiveriesForSelectedCarAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Disable failed: {ex.Message}";
        }
    }

    private async Task UninstallAsync()
    {
        var pkg = GetSelectedPackage();
        if (pkg is null)
        {
            StatusText = "Invalid livery selection.";
            return;
        }

        try
        {
            StatusText = "Uninstalling...";
            await _installer.UninstallAsync(pkg, GameRoot);
            StatusText = "Uninstalled.";
            await LoadLiveriesForSelectedCarAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Uninstall failed: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    
    private async Task LoadSettingsAsync()
    {
        try
        {
            var s = await _settingsService.LoadAsync();
            GameRoot = s.GameRoot;
            InstallerRoot = s.InstallerRoot;

            if (s.AutoRescanOnStartup && Directory.Exists(InstallerRoot))
                await RescanAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load settings: {ex.Message}";
        }
    }

    private async Task SaveSettingsAsync()
    {
        var s = await _settingsService.LoadAsync();
        s.GameRoot = GameRoot;
        s.InstallerRoot = InstallerRoot;
        await _settingsService.SaveAsync(s);
    }
    
    private void BrowseGameRoot()
    {
        var selected = _folderPicker.PickFolder("Select Assetto Corsa Rally game root", GameRoot);
        if (string.IsNullOrWhiteSpace(selected)) return;

        GameRoot = selected;
        _ = SaveSettingsAsync();
    }

    private void BrowseInstallerRoot()
    {
        var selected = _folderPicker.PickFolder("Select livery installer root", InstallerRoot);
        if (string.IsNullOrWhiteSpace(selected)) return;

        InstallerRoot = selected;
        _ = SaveSettingsAsync();
    }
}