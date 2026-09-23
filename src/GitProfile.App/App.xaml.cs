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

        ApplyTheme();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Exit += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        var window = new MainWindow(services);
        MainWindow = window;
        window.Show();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General)
            ApplyTheme();
    }

    /// <summary>Swaps the colour dictionary so the window follows the Windows light or dark setting.</summary>
    private void ApplyTheme()
    {
        var palette = new Wpf.ResourceDictionary
        {
            Source = Pack(ThemePrefersDark() ? "Palette.Dark.xaml" : "Palette.Light.xaml"),
        };

        if (Resources.MergedDictionaries.Count == 0)
        {
            Resources.MergedDictionaries.Add(palette);
            Resources.MergedDictionaries.Add(new Wpf.ResourceDictionary { Source = Pack("Theme.xaml") });
            return;
        }

        Resources.MergedDictionaries[0] = palette;
    }

    /// <summary>Follows the Windows app theme, unless GITPROFILE_THEME says light or dark.</summary>
    internal static bool ThemePrefersDark()
    {
        var forced = Environment.GetEnvironmentVariable("GITPROFILE_THEME");
        if (forced is not null)
            return forced.Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);

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
