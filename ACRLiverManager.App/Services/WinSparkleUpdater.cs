using System.Runtime.InteropServices;

namespace ACRLiverManager.App.Services;

public static class WinSparkleUpdater
{
    private const string DllName = "WinSparkle.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void win_sparkle_set_appcast_url(string url);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int win_sparkle_set_eddsa_public_key(string publicKey);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void win_sparkle_set_app_details(string companyName, string appName, string appVersion);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void win_sparkle_init();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void win_sparkle_cleanup();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void win_sparkle_check_update_with_ui();

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;

        var version = AppVersionProvider.GetInformationalVersion();

        win_sparkle_set_app_details(
            "Simon Roberts",
            "AcrLiveryManager",
            version);

        win_sparkle_set_appcast_url(
            "https://simongtt-bit.github.io/ACRLiverManager.App/appcast.xml");

        var publicKey = "EBp0pfeZWuyVowoH26ChvaVnAYPPOFyMq0Uf/lO+5aU=";
        var ok = win_sparkle_set_eddsa_public_key(publicKey);
        if (ok != 1)
            throw new InvalidOperationException("Invalid WinSparkle EdDSA public key.");

        win_sparkle_init();
        _initialized = true;
    }

    public static void Cleanup()
    {
        if (!_initialized)
            return;

        win_sparkle_cleanup();
        _initialized = false;
    }

    public static void CheckForUpdates()
    {
        if (!_initialized)
            return;

        win_sparkle_check_update_with_ui();
    }
}