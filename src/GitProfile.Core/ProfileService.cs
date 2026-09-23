namespace GitProfile.Core;

/// <summary>One requested change to a git config key. A null <see cref="Value"/> removes the key.</summary>
public sealed record ConfigEdit(ConfigScope Scope, string Key, string? Value)
{
    public string Describe() =>
        $"git config {Scope.Flag()}"
        + (Value is null ? " --unset" : "")
        + $" {Key}"
        + (Value is null ? "" : $" {Value}");
}

/// <summary>What git would use right now, per scope.</summary>
public sealed record ProfileStatus(
    string? DefaultLogin,
    string? RepoRoot,
    string? PinnedLogin,
    string? OriginUrl,
    string? OriginOwner)
{
    /// <summary>The account an operation in this repository would actually authenticate as.</summary>
    public string? EffectiveLogin => PinnedLogin ?? DefaultLogin;

    public bool InRepository => RepoRoot is not null;
}

/// <summary>Asked to switch to an account that is not signed in on this machine.</summary>
public sealed class ProfileNotFoundException(string login)
    : Exception($"'{login}' is not signed in on this machine. Add it with: GitProfile add");

/// <summary>Asked for a repository-level change at a path that is not a repository.</summary>
public sealed class NotInRepositoryException(string path)
    : Exception($"'{path}' is not a git repository.");

/// <summary>Decides which GitHub account git should use, and where that decision is recorded.</summary>
public sealed class ProfileService(
    GcmProfileSource accounts,
    GitConfig config,
    GitRepository repository,
    IdentityStore identities)
{
    public IReadOnlyList<string> ListProfiles() => accounts.ListAccounts();

    public ProfileStatus Status(string startPath)
    {
        var defaultLogin = config.Get(ConfigScope.Global, GitConfigKeys.GitHubAccount);
        var repoRoot = repository.FindRoot(startPath);
        var pinnedLogin = repoRoot is null ? null : config.Get(ConfigScope.Local, GitConfigKeys.GitHubAccount, repoRoot);
        var originUrl = repoRoot is null ? null : repository.OriginUrl(repoRoot);

        return new ProfileStatus(defaultLogin, repoRoot, pinnedLogin, originUrl, GitHubUrl.OwnerOf(originUrl));
    }

    public IReadOnlyList<ConfigEdit> PlanSetDefault(string login)
    {
        var account = RequireSignedIn(login);
        return EditsFor(ConfigScope.Global, account);
    }

    public IReadOnlyList<ConfigEdit> PlanPin(string login, string repoPath)
    {
        var account = RequireSignedIn(login);
        RequireRepository(repoPath);
        return EditsFor(ConfigScope.Local, account);
    }

    public IReadOnlyList<ConfigEdit> PlanClearPin(string repoPath)
    {
        RequireRepository(repoPath);
        return
        [
            new(ConfigScope.Local, GitConfigKeys.GitHubAccount, null),
            new(ConfigScope.Local, GitConfigKeys.CommitName, null),
            new(ConfigScope.Local, GitConfigKeys.CommitEmail, null),
        ];
    }

    public void Apply(IReadOnlyList<ConfigEdit> edits, string? repoPath = null)
    {
        foreach (var edit in edits)
        {
            var workingDirectory = edit.Scope == ConfigScope.Local ? repoPath : null;
            if (edit.Value is null)
                config.Unset(edit.Scope, edit.Key, workingDirectory);
            else
                config.Set(edit.Scope, edit.Key, edit.Value, workingDirectory);
        }
    }

    private IReadOnlyList<ConfigEdit> EditsFor(ConfigScope scope, string account)
    {
        var edits = new List<ConfigEdit> { new(scope, GitConfigKeys.GitHubAccount, account) };
        var identity = identities.Find(account);
        if (identity?.Name is { Length: > 0 } name)
            edits.Add(new(scope, GitConfigKeys.CommitName, name));
        if (identity?.Email is { Length: > 0 } email)
            edits.Add(new(scope, GitConfigKeys.CommitEmail, email));
        return edits;
    }

    /// <summary>Git logins are case-insensitive, so keep whichever spelling the credential manager reports.</summary>
    private string RequireSignedIn(string login)
    {
        var match = accounts.ListAccounts()
            .FirstOrDefault(a => string.Equals(a, login, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ProfileNotFoundException(login);
    }

    private void RequireRepository(string path)
    {
        if (repository.FindRoot(path) is null)
            throw new NotInRepositoryException(path);
    }
}
