using System.Text.Json;

namespace GitProfile.Core;

/// <summary>Which colour scheme GitProfile should show.</summary>
public enum AppTheme
{
    /// <summary>Follow the Windows light or dark setting.</summary>
    System,
    Light,
    Dark,
}

/// <summary>The user's theme choice, kept beside the profile file so it survives restarts.</summary>
public sealed class ThemeSettings(string filePath)
{
    private static readonly JsonSerializerOptions Writing = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The saved mode, or <see cref="AppTheme.System"/> when nothing usable is stored.</summary>
    public AppTheme Load()
    {
        if (!File.Exists(filePath))
            return AppTheme.System;

        try
        {
            return JsonSerializer.Deserialize<Data>(File.ReadAllText(filePath), Reading)?.Theme switch
            {
                "light" => AppTheme.Light,
                "dark" => AppTheme.Dark,
                _ => AppTheme.System,
            };
        }
        catch (JsonException)
        {
            return AppTheme.System;
        }
    }

    public void Save(AppTheme mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var data = new Data { Theme = mode.ToString().ToLowerInvariant() };
        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Writing));
    }

    private sealed class Data
    {
        public string? Theme { get; set; }
    }
}
