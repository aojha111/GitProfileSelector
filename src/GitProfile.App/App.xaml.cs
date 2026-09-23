using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GitProfile.Core;
using Microsoft.Win32;
using Wpf = System.Windows;


namespace GitProfile.App;

public partial class App : Wpf.Application
{
    private const uint AttachParentProcess = 0x0000_0003;

    private readonly ThemeSettings _settings = new(AppPaths.SettingsFile());
    private AppTheme _mode;

    /// <summary>Raised after the palette has been swapped, so open windows can match their chrome to it.</summary>
    internal event Action? ThemeChanged;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint dwProcessId);

    protected override void OnStartup(Wpf.StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0)
        {
            Shutdown(CommandLineHost.Run(e.Args));
            return;
        }

        Services services;
        try
        {
            services = Services.Build();
        }
        catch (GitProfileSetupException ex)
        {
            Wpf.MessageBox.Show(ex.Message, "GitProfile", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }

        _mode = _settings.Load();
        ApplyTheme();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Exit += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        var window = new MainWindow(services);
        MainWindow = window;
        window.Show();
    }

    public static App Self => (App)Current;

    internal AppTheme Mode => _mode;

    /// <summary>Stores a new choice, swaps the palette and lets windows follow.</summary>
    internal void SetTheme(AppTheme mode)
    {
        if (_mode == mode)
            return;

        _mode = mode;
        _settings.Save(mode);
        ApplyTheme();
        ThemeChanged?.Invoke();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General && _mode == AppTheme.System)
            ApplyTheme();
    }

    /// <summary>Swaps the colour dictionary to match the chosen theme, or the Windows one when set to follow.</summary>
    private void ApplyTheme()
    {
        var palette = new Wpf.ResourceDictionary
        {
            Source = Pack(EffectivePrefersDark() ? "Palette.Dark.xaml" : "Palette.Light.xaml"),
        };

        if (Resources.MergedDictionaries.Count == 0)
        {
            Resources.MergedDictionaries.Add(palette);
            Resources.MergedDictionaries.Add(new Wpf.ResourceDictionary { Source = Pack("Theme.xaml") });
            return;
        }

        Resources.MergedDictionaries[0] = palette;
    }

    /// <summary>The palette in use right now: GITPROFILE_THEME first, then the user's choice, then Windows.</summary>
    internal static bool EffectivePrefersDark()
    {
        var forced = Environment.GetEnvironmentVariable("GITPROFILE_THEME");
        if (forced is not null)
            return forced.Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);

        return Self._mode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => SystemPrefersDark(),
        };
    }

    /// <summary>What the Windows light or dark app setting says.</summary>
    private static bool SystemPrefersDark()
    {
        var value = Registry.CurrentUser
            .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
            ?.GetValue("AppsUseLightTheme");
        return value is int light && light == 0;
    }

    private static Uri Pack(string file) => new($"pack://application:,,,/{file}");

    /// <summary>
    /// A windowed exe gets no console, so a command line run adopts the terminal it was launched from
    /// and reopens the output streams - .NET already decided they were absent when the process started.
    /// </summary>
    internal static void AttachToTerminal()
    {
        AttachConsole(AttachParentProcess);
        var encoding = new UTF8Encoding(false);
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), encoding) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), encoding) { AutoFlush = true });
        }
        catch (IOException)
        {
            // Launched with nothing attached; the exit code still tells a script what happened.
        }
    }
}
