using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using GitProfile.Core;
using MessageBox = System.Windows.MessageBox;


namespace GitProfile.App;

public partial class MainWindow : Window
{
    private const int UseImmersiveDarkTitleBar = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private readonly Services _services;
    private readonly TrayController _tray;
    private readonly AccountPickerModel _model;
    private bool _quitting;
    private bool _loadingThemeChoice;

    internal MainWindow(Services services)
    {
        _services = services;
        _model = new AccountPickerModel(services.Profile, services.Accounts, services.Identities, Environment.CurrentDirectory);

        InitializeComponent();
        DataContext = _model;
        Title = $"GitProfile - {Environment.CurrentDirectory}";

        _loadingThemeChoice = true;
        (App.Self.Mode switch
        {
            AppTheme.Light => ThemeLightChip,
            AppTheme.Dark => ThemeDarkChip,
            _ => ThemeSystemChip,
        }).IsChecked = true;
        _loadingThemeChoice = false;
        App.Self.ThemeChanged += UpdateTitleBar;
        Closed += (_, _) => App.Self.ThemeChanged -= UpdateTitleBar;

        _tray = new TrayController(ShowFromTray, Quit, () => _model.Accounts, () => _model.DefaultLogin, _model.MakeDefault);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UpdateTitleBar();
    }

    /// <summary>Makes the Windows-drawn title bar match whichever palette is on screen.</summary>
    private void UpdateTitleBar()
    {
        var dark = App.EffectivePrefersDark() ? 1 : 0;
        DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle,
            UseImmersiveDarkTitleBar, ref dark, sizeof(int));
    }

    private void OnThemeChosen(object sender, RoutedEventArgs e)
    {
        if (_loadingThemeChoice || sender is not System.Windows.Controls.RadioButton { Tag: string tag } chip)
            return;

        if (!chip.IsChecked.GetValueOrDefault())
            return;

        App.Self.SetTheme(Enum.Parse<AppTheme>(tag));
    }

    private void OnApply(object sender, RoutedEventArgs e) => Apply(_model.Apply);

    private void OnUnpin(object sender, RoutedEventArgs e) => Apply(_model.Unpin);

    private void OnSaveIdentity(object sender, RoutedEventArgs e) => Apply(_model.SaveIdentity);

    private void OnRefresh(object sender, RoutedEventArgs e) => Apply(_model.Refresh);

    private void OnAddAccount(object sender, RoutedEventArgs e) => Apply(_model.AddAccount);

    private void OnRemoveAccount(object sender, RoutedEventArgs e) => Apply(_model.RemoveSelectedAccount);

    private void OnChooseRepository(object sender, RoutedEventArgs e)
    {
        using var picker = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Pick the repository GitProfile should work on",
            UseDescriptionForTitle = true,
        };
        if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        _model.UseRepository(picker.SelectedPath);
        Title = $"GitProfile - {picker.SelectedPath}";
        _tray.RefreshMenu();
    }

    /// <summary>Every action leaves the tray menu agreeing with the window, since both edit the same thing.</summary>
    private void Apply(Action action)
    {
        action();
        _tray.RefreshMenu();
    }

    /// <summary>Coming back from the tray re-reads git, which may have been used elsewhere meanwhile.</summary>
    private void ShowFromTray()
    {
        Apply(() => _model.Refresh());
        Show();
        Activate();
    }

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    /// <summary>Closing hides the window to the notification area; Exit there is what ends the program.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_quitting)
        {
            e.Cancel = true;
            Hide();
            _tray.NotifyHidden();
            return;
        }

        _tray.Dispose();
        base.OnClosing(e);
    }
}

