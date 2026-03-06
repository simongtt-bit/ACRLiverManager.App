using System.Reflection;

namespace ACRLiverManager.App.Services;

public static class AppVersionProvider
{
    public static string GetInformationalVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        return assembly.GetName().Version?.ToString() ?? "0.1.0";
    }
}