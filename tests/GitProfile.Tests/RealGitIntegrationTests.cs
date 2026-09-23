using GitProfile.Core;

namespace GitProfile.Tests;

/// <summary>
/// Exercises the whole stack against a real git, in a throwaway repository, with the real user and
/// system config files redirected away. This is what proves a repository pin overrides a user pin —
/// git itself makes that decision, not our code.
/// </summary>
[Collection("RealGit")]
public sealed class RealGitIntegrationTests : IDisposable
{
    private const string PinKey = GitConfigKeys.GitHubAccount;

    private readonly string _root;
    private readonly string _repo;
    private readonly ToolPaths _tools;
    private readonly IProcessRunner _real;

    public RealGitIntegrationTests()
    {
        _tools = ToolLocator.FindCurrent()
                 ?? throw new InvalidOperationException("git and Git Credential Manager are required for these tests.");
        _real = new SystemProcessRunner();

        _root = Path.Combine(Path.GetTempPath(), $"gitprofile-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "empty.gitconfig"), "");

        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", Path.Combine(_root, "global.gitconfig"));
        Environment.SetEnvironmentVariable("GIT_CONFIG_SYSTEM", Path.Combine(_root, "empty.gitconfig"));

        _repo = Path.Combine(_root, "repo");
        Directory.CreateDirectory(_repo);
        var init = _real.Run(_tools.Git, ["init", "--quiet"], _repo);
        if (!init.Success)
            throw new InvalidOperationException($"git init failed: {init.StandardError}");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", null);
        Environment.SetEnvironmentVariable("GIT_CONFIG_SYSTEM", null);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Leftover temp directory is not worth failing a run over.
        }
    }

    private GitConfig Config => new(_real, _tools);

    /// <summary>Asks real git what value it would use, exactly as a credential helper would see it.</summary>
    private string? GitSees(string workingDirectory)
    {
        var result = _real.Run(_tools.Git, ["config", "--get", PinKey], workingDirectory);
        var value = result.StandardOutput.Trim();
        return value.Length == 0 ? null : value;
    }

    [Fact]
    public void A_user_scope_pin_is_written_to_the_user_config_file()
    {
        Config.Set(ConfigScope.Global, PinKey, "aojha111");

        Assert.Equal("aojha111", GitSees(_repo));
        Assert.Contains("https://github.com", File.ReadAllText(Path.Combine(_root, "global.gitconfig")));
    }

    [Fact]
    public void Reading_back_a_user_pin_returns_the_same_value()
    {
        Config.Set(ConfigScope.Global, PinKey, "aojha111");

        Assert.Equal("aojha111", Config.Get(ConfigScope.Global, PinKey));
    }

    [Fact]
    public void A_repository_pin_overrides_the_user_pin_where_git_is_concerned()
    {
        Config.Set(ConfigScope.Global, PinKey, "aojha111");
        Config.Set(ConfigScope.Local, PinKey, "abhijitojha7", _repo);

        Assert.Equal("abhijitojha7", GitSees(_repo));
    }

    [Fact]
    public void The_user_pin_still_applies_outside_that_repository()
    {
        Config.Set(ConfigScope.Global, PinKey, "aojha111");
        Config.Set(ConfigScope.Local, PinKey, "abhijitojha7", _repo);

        Assert.Equal("aojha111", GitSees(_root));
    }

    [Fact]
    public void Removing_the_repository_pin_restores_the_user_default()
    {
        Config.Set(ConfigScope.Global, PinKey, "aojha111");
        Config.Set(ConfigScope.Local, PinKey, "abhijitojha7", _repo);

        Config.Unset(ConfigScope.Local, PinKey, _repo);

        Assert.Equal("aojha111", GitSees(_repo));
    }

    [Fact]
    public void Unsetting_a_key_that_was_never_set_is_not_an_error()
    {
        Config.Unset(ConfigScope.Local, "user.email", _repo);
    }

    [Fact]
    public void A_key_that_was_never_set_reads_back_as_absent()
    {
        Assert.Null(Config.Get(ConfigScope.Global, PinKey));
    }

    [Fact]
    public void Real_git_reports_the_repository_root_and_we_can_pin_through_the_service()
    {
        var service = BuildService();

        var status = service.Status(_repo);

        Assert.Equal(Path.GetFullPath(_repo).Replace('\\', '/').TrimEnd('/'),
            status.RepoRoot!.Replace('\\', '/').TrimEnd('/'), ignoreCase: true);
    }

    [Fact]
    public void The_service_pinning_a_real_repository_changes_what_git_would_use()
    {
        var service = BuildService();
        service.Apply(service.PlanSetDefault("aojha111"));
        service.Apply(service.PlanPin("abhijitojha7", _repo), _repo);

        Assert.Equal("abhijitojha7", GitSees(_repo));
        Assert.Equal("aojha111", GitSees(_root));
    }

    [Fact]
    public void The_service_writes_the_commit_identity_into_the_repository_config()
    {
        var service = BuildService();
        service.Apply(service.PlanPin("abhijitojha7", _repo), _repo);

        var email = _real.Run(_tools.Git, ["config", "--local", "--get", "user.email"], _repo);

        Assert.Equal("abhijitojha7@users.noreply.github.com", email.StandardOutput.Trim());
    }

    /// <summary>Real git, real config files; only the credential-manager account list is scripted.</summary>
    private ProfileService BuildService()
    {
        var runner = new RoutingProcessRunner(_real, _tools.CredentialManager);
        var identities = new IdentityStore(Path.Combine(_root, "profiles.json"));
        identities.Save("abhijitojha7", "Abhijit Ojha", "abhijitojha7@users.noreply.github.com");
        return new ProfileService(
            new GcmProfileSource(runner, _tools),
            new GitConfig(runner, _tools),
            new GitRepository(runner, _tools),
            identities);
    }

    private sealed class RoutingProcessRunner(IProcessRunner real, string credentialManager) : IProcessRunner
    {
        public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
        {
            if (Path.GetFileNameWithoutExtension(fileName)
                .Equals(Path.GetFileNameWithoutExtension(credentialManager), StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessResult(0, "aojha111\nabhijitojha7\n", "");
            }
            return real.Run(fileName, arguments, workingDirectory);
        }
    }
}
