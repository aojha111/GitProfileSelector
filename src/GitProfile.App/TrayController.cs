using GitProfile.Core;

namespace GitProfile.App;

/// <summary>
/// The notification-area shortcut: pick an account, git uses it. The window and this menu read and
/// write the same state, so whichever you use last is what the other shows next time it refreshes.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly Action _show;
    private readonly Action _quit;
    private readonly Func<IReadOnlyList<string>> _accounts;
    private readonly Func<string?> _current;
    private readonly Action<string> _makeDefault;
    private readonly System.Windows.Forms.ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private bool _hinted;

    public TrayController(
        Action show,
        Action quit,
        Func<IReadOnlyList<string>> accounts,
        Func<string?> current,
        Action<string> makeDefault)
    {
        _show = show;
        _quit = quit;
        _accounts = accounts;
        _current = current;
        _makeDefault = makeDefault;

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "GitProfile - GitHub account for git",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _show();
        RefreshMenu();
    }

    /// <summary>The same icon file the exe and the title bar use, read out of this assembly.</summary>
    private static System.Drawing.Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/GitProfile.ico"))
            ?? throw new InvalidOperationException("GitProfile.ico is missing from this build.");
        return new System.Drawing.Icon(resource.Stream);
    }

    /// <summary>Rebuilds the menu so the tick marks the account git currently defaults to.</summary>
    public void RefreshMenu()
    {
        _menu.Items.Clear();

        var current = _current();
        _menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
            $"Default account: {(current ?? "(none - you get asked every push)")}")
        {
            Enabled = false,
        });
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        foreach (var account in _accounts())
        {
            var chosen = account;
            var item = new System.Windows.Forms.ToolStripMenuItem(chosen)
            {
                Checked = string.Equals(chosen, current, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) =>
            {
                _makeDefault(chosen);
                RefreshMenu();
            };
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add("Open GitProfile", null, (_, _) => _show());
        _menu.Items.Add("Exit GitProfile", null, (_, _) => _quit());
    }

    /// <summary>Tells the user once that hiding the window keeps the tray shortcut alive.</summary>
    public void NotifyHidden()
    {
        if (_hinted)
            return;
        _hinted = true;
        _icon.BalloonTipTitle = "GitProfile is still running";
        _icon.BalloonTipText = "Right-click the icon to switch accounts, or double-click to open the window.";
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
