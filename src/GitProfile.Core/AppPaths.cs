namespace GitProfile.Core;

/// <summary>Where GitProfile keeps its own files.</summary>
public static class AppPaths
{
    public const string AppFolderName = "GitProfileSelector";
    public const string IdentityFileName = "profiles.json";
    public const string SettingsFileName = "settings.json";

    /// <summary>The names-and-emails file, kept per Windows user so it travels with the account.</summary>
    public static string IdentityFile(string? localApplicationData = null) =>
        Path.Combine(
            localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName,
            IdentityFileName);

    /// <summary>The app's own preferences, such as the chosen theme.</summary>
    public static string SettingsFile(string? localApplicationData = null) =>
        Path.Combine(
            localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName,
            SettingsFileName);
}
