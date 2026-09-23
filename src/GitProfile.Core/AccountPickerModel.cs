using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GitProfile.Core;

/// <summary>
/// Everything the picker window shows and everything its buttons do, kept free of user interface
/// types so the two stay in step and the behaviour can be tested without a screen.
/// </summary>
public sealed class AccountPickerModel : INotifyPropertyChanged
{
    private readonly ProfileService _service;
    private readonly GcmProfileSource _accounts;
    private readonly IdentityStore _identities;
    private string _workingDirectory;

    private ProfileStatus _status = new(null, null, null, null, null);
    private IReadOnlyList<string> _accountList = [];
    private string? _selected;
    private bool _asDefault;
    private bool _pinRepository;
    private string _commitName = "";
    private string _commitEmail = "";
    private string? _message;

    public AccountPickerModel(
        ProfileService service,
        GcmProfileSource accounts,
        IdentityStore identities,
        string workingDirectory)
    {
        _service = service;
        _accounts = accounts;
        _identities = identities;
        _workingDirectory = workingDirectory;
        LoadState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Accounts => _accountList;

    /// <summary>The accounts drawn in the list, each labelled with what it currently does.</summary>
    public IReadOnlyList<AccountRow> Rows => _accountList
        .Select(login => new AccountRow(
            login,
            AccountRow.InitialsOf(login),
            AccountRow.AvatarOf(login),
            RoleOf(login),
            IsDefaultFor(login),
            _status.InRepository && IsPinnedFor(login)))
        .ToList();

    public bool HasAccounts => _accountList.Count > 0;

    public string? Selected
    {
        get => _selected;
        set
        {
            if (string.Equals(_selected, value, StringComparison.Ordinal))
                return;
            _selected = value;
            _message = null;
            FollowSelection();
            RaiseAll();
        }
    }

    public bool AsDefault
    {
        get => _asDefault;
        set
        {
            if (_asDefault == value)
                return;
            _asDefault = value;
            _message = null;
            Raise();
            RaiseAll();
        }
    }

    public bool PinRepository
    {
        get => _pinRepository;
        set
        {
            if (_pinRepository == value)
                return;
            _pinRepository = value;
            _message = null;
            Raise();
            RaiseAll();
        }
    }

    public string CommitName
    {
        get => _commitName;
        set => SetField(ref _commitName, value);
    }

    public string CommitEmail
    {
        get => _commitEmail;
        set => SetField(ref _commitEmail, value);
    }

    public bool InRepository => _status.InRepository;
    public bool CanPinRepository => _status.InRepository;
    public bool HasSelection => _selected is not null;
    public bool CanUnpin => _status.PinnedLogin is not null;
    public string? RepoRoot => _status.RepoRoot;
    public string? OriginOwner => _status.OriginOwner;
    public string? OriginUrl => _status.OriginUrl;
    public string? DefaultLogin => _status.DefaultLogin;
    public string? PinnedLogin => _status.PinnedLogin;
    public string? EffectiveLogin => _status.EffectiveLogin;

    /// <summary>The exact git commands the current choices would run, shown before they run.</summary>
    public IReadOnlyList<string> Preview => PlannedEdits().Select(e => e.Describe()).ToList();

    public bool CanApply => PlannedEdits().Count > 0;

    /// <summary>Where things stand, or what the last action achieved or could not do.</summary>
    public string Headline => _message ?? ComputedHeadline();

    public void Refresh()
    {
        _message = null;
        LoadState();
        RaiseAll();
    }

    /// <summary>Moves the repository half of the window to another folder, for a double-clicked start.</summary>
    public void UseRepository(string path)
    {
        _workingDirectory = path;
        Refresh();
    }

    public bool HasEffectiveLogin => _status.EffectiveLogin is not null;

    public void Apply()
    {
        var edits = PlannedEdits();
        if (edits.Count == 0)
            return;

        var account = _selected;
        var wantedDefault = _asDefault;
        var wantedPin = _pinRepository && CanPinRepository;

        try
        {
            _service.Apply(edits, _status.RepoRoot);
        }
        catch (Exception ex) when (ex is ToolFailedException or ProfileNotFoundException or NotInRepositoryException)
        {
            _asDefault = false;
            _pinRepository = false;
            _message = ex.Message;
            RaiseAll();
            return;
        }

        LoadState();
        _message = wantedPin && wantedDefault ? $"set {account} as the default and pinned it to {RepoRoot}"
                 : wantedPin ? $"pinned {account} to {RepoRoot}"
                 : $"default account is now {account}";
        RaiseAll();
    }

    /// <summary>Sets the user default straight away, which is what the tray shortcut asks for.</summary>
    public void MakeDefault(string login)
    {
        try
        {
            _service.Apply(_service.PlanSetDefault(login), _status.RepoRoot);
        }
        catch (Exception ex) when (ex is ToolFailedException or ProfileNotFoundException or NotInRepositoryException)
        {
            _message = ex.Message;
            RaiseAll();
            return;
        }

        LoadState();
        _message = $"default account is now {login}";
        RaiseAll();
    }

    public void Unpin()
    {
        if (!_status.InRepository)
            return;
        try
        {
            _service.Apply(_service.PlanClearPin(_status.RepoRoot!), _status.RepoRoot);
            _message = "this repository follows the default account again";
        }
        catch (Exception ex) when (ex is ToolFailedException or NotInRepositoryException)
        {
            _message = ex.Message;
        }
        LoadState();
        RaiseAll();
    }

    public void SaveIdentity()
    {
        if (_selected is null)
            return;
        try
        {
            _identities.Save(_selected, NullIfBlank(_commitName), NullIfBlank(_commitEmail));
            _message = $"saved the commit identity for {_selected}";
            FollowSelection();
        }
        catch (Exception ex) when (ex is IOException or IdentityStoreCorruptException)
        {
            _message = ex.Message;
        }
        RaiseAll();
    }

    public void AddAccount()
    {
        try
        {
            _accounts.Login();
        }
        catch (Exception ex) when (ex is ToolFailedException)
        {
            _message = ex.Message;
            Refresh();
            return;
        }
        Refresh();
    }

    public void RemoveSelectedAccount()
    {
        if (_selected is null)
            return;
        try
        {
            _accounts.Logout(_selected);
        }
        catch (Exception ex) when (ex is ToolFailedException)
        {
            _message = ex.Message;
        }
        Refresh();
    }

    /// <summary>Whether this account is the user default / this repository's pin / the winner here.</summary>
    public bool IsDefaultFor(string account) =>
        string.Equals(_status.DefaultLogin, account, StringComparison.OrdinalIgnoreCase);

    public bool IsPinnedFor(string account) =>
        string.Equals(_status.PinnedLogin, account, StringComparison.OrdinalIgnoreCase);

    public bool IsEffectiveFor(string account) =>
        string.Equals(_status.EffectiveLogin, account, StringComparison.OrdinalIgnoreCase);

    private string RoleOf(string login)
    {
        var isDefault = IsDefaultFor(login);
        var isPinned = _status.InRepository && IsPinnedFor(login);
        if (isDefault && isPinned)
            return "default account, pinned here";
        if (isDefault)
            return "default account";
        if (isPinned)
            return "pinned to this repository";
        return _status.DefaultLogin is null ? "signed in, but nothing chosen yet" : "signed in on this machine";
    }

    private IReadOnlyList<ConfigEdit> PlannedEdits()
    {
        if (_selected is null)
            return [];

        var edits = new List<ConfigEdit>();
        if (_asDefault)
            edits.AddRange(_service.PlanSetDefault(_selected));
        if (_pinRepository && CanPinRepository)
            edits.AddRange(_service.PlanPin(_selected, _status.RepoRoot!));
        return edits;
    }

    private void LoadState()
    {
        ProfileStatus status;
        IReadOnlyList<string> accountList;
        try
        {
            accountList = _service.ListProfiles();
            status = _service.Status(_workingDirectory);
        }
        catch (Exception ex) when (ex is ToolFailedException or IdentityStoreCorruptException)
        {
            // A broken git config or a missing tool must leave the window readable, not throw.
            _message = ex.Message;
            return;
        }

        _accountList = accountList;
        _status = status;
        if (_selected is null || !_accountList.Contains(_selected, StringComparer.OrdinalIgnoreCase))
            _selected = status.EffectiveLogin;
        FollowSelection();
    }

    /// <summary>Brings the tick boxes and commit fields in line with whoever is selected.</summary>
    private void FollowSelection()
    {
        _asDefault = _selected is not null && IsDefaultFor(_selected);
        _pinRepository = _selected is not null && IsPinnedFor(_selected);

        var identity = _selected is null ? null : _identities.Find(_selected);
        _commitName = identity?.Name ?? "";
        _commitEmail = identity?.Email ?? (_selected is null ? "" : GitHubIdentity.SuggestedEmail(_selected));
    }

    private string ComputedHeadline()
    {
        if (!HasAccounts)
            return "no GitHub account is signed in on this machine - add one to begin";
        if (_status.DefaultLogin is null)
            return "no default account: Git Credential Manager will ask which account to use";
        return _status.PinnedLogin is null
            ? $"default account is {_status.DefaultLogin}"
            : $"default account is {_status.DefaultLogin}, and this repository is pinned to {_status.PinnedLogin}";
    }

    private static string? NullIfBlank(string value) => value.Trim().Length == 0 ? null : value.Trim();

    /// <summary>Everything derived from status or selection, raised together after a state change.</summary>
    private void RaiseAll()
    {
        Raise(nameof(Accounts));
        Raise(nameof(Rows));
        Raise(nameof(HasAccounts));
        Raise(nameof(Selected));
        Raise(nameof(AsDefault));
        Raise(nameof(PinRepository));
        Raise(nameof(CommitName));
        Raise(nameof(CommitEmail));
        Raise(nameof(InRepository));
        Raise(nameof(CanPinRepository));
        Raise(nameof(HasSelection));
        Raise(nameof(CanUnpin));
        Raise(nameof(RepoRoot));
        Raise(nameof(OriginOwner));
        Raise(nameof(OriginUrl));
        Raise(nameof(DefaultLogin));
        Raise(nameof(PinnedLogin));
        Raise(nameof(EffectiveLogin));
        Raise(nameof(HasEffectiveLogin));
        Raise(nameof(Preview));
        Raise(nameof(CanApply));
        Raise(nameof(Headline));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Raise(name);
    }

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
