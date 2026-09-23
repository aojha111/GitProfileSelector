using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

public class CliRunnerTests : IDisposable
{
    private const string Repo = "C:/repos/app";

    private readonly GitProfileHarness _h = new();
    private readonly StringWriter _output = new() { NewLine = "\n" };

    public void Dispose()
    {
        if (File.Exists(_h.IdentityFile)) File.Delete(_h.IdentityFile);
    }

    private int Run(out string output, params string[] args)
    {
        var request = (ParseOutcome.Request)CommandLine.Parse(args);
        var runner = new CliRunner(_h.Service, _h.AccountsSource, _h.Identities, _output);
        var code = runner.Run(request, Repo);
        output = _output.ToString().TrimEnd();
        return code;
    }

    private int RunInRepo(out string output, params string[] args)
    {
        _h.RepoRoot = Repo;
        return Run(out output, args);
    }

    [Fact]
    public void List_prints_every_signed_in_account()
    {
        Assert.Equal(CliExit.Ok, Run(out var output, "list"));

        Assert.Equal("aojha111\nabhijitojha7", output);
    }

    [Fact]
    public void Current_names_the_user_default()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));

        Run(out var output, "current");

        Assert.Contains("default account: abhijitojha7", output);
    }

    [Fact]
    public void Current_explains_that_no_default_means_being_asked_every_time()
    {
        Run(out var output, "current");

        Assert.Contains("no default account", output);
        Assert.Contains("will ask which account to use", output);
    }

    [Fact]
    public void Current_shows_the_repository_pin_and_which_account_wins()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("aojha111"));
        _h.RepoRoot = Repo;
        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);

        RunInRepo(out var output, "current");

        Assert.Contains("pinned account: abhijitojha7", output);
        Assert.Contains("effective account: abhijitojha7", output);
    }

    [Fact]
    public void Current_reports_when_the_folder_is_not_a_repository()
    {
        _h.RepoRoot = null;

        Run(out var output, "current");

        Assert.Contains("not inside a git repository", output);
    }

    [Fact]
    public void Use_makes_the_account_the_user_default()
    {
        Assert.Equal(CliExit.Ok, Run(out var output, "use", "abhijitojha7"));

        Assert.Contains("default account is now abhijitojha7", output);
        Assert.Equal("abhijitojha7", _h.GlobalConfig[GitConfigKeys.GitHubAccount]);
    }

    [Fact]
    public void Use_shows_every_git_config_change_it_made()
    {
        Run(out var output, "use", "abhijitojha7");

        Assert.Contains($"git config --global {GitConfigKeys.GitHubAccount} abhijitojha7", output);
    }

    [Fact]
    public void Dry_run_reports_the_changes_without_making_them()
    {
        Assert.Equal(CliExit.Ok, Run(out var output, "use", "abhijitojha7", "--dry-run"));

        Assert.Contains("would run:", output);
        Assert.Empty(_h.GlobalConfig);
    }

    [Fact]
    public void Use_of_an_account_that_is_not_signed_in_is_refused()
    {
        Assert.Equal(CliExit.UnknownAccount, Run(out var output, "use", "stranger"));

        Assert.Contains("not signed in", output);
        Assert.Contains("GitProfile add", output);
    }

    [Fact]
    public void Pin_refuses_a_folder_that_is_not_a_repository()
    {
        _h.RepoRoot = null;

        Assert.Equal(CliExit.NotARepository, Run(out var output, "pin", "abhijitojha7"));

        Assert.Contains("not a git repository", output);
    }

    [Fact]
    public void Pin_leaves_the_user_default_alone()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("aojha111"));
        _h.RepoRoot = Repo;

        RunInRepo(out var output, "pin", "abhijitojha7");

        Assert.Contains("pinned abhijitojha7 to", output);
        Assert.Equal("abhijitojha7", _h.LocalConfig[GitConfigKeys.GitHubAccount]);
        Assert.Equal("aojha111", _h.GlobalConfig[GitConfigKeys.GitHubAccount]);
    }

    [Fact]
    public void Unpin_removes_the_repository_override()
    {
        RunInRepo(out _, "pin", "abhijitojha7");

        Assert.Equal(CliExit.Ok, RunInRepo(out var output, "unpin"));

        Assert.Empty(_h.LocalConfig);
        Assert.Contains("unpinned", output);
    }

    [Fact]
    public void Unpin_outside_a_repository_is_refused()
    {
        _h.RepoRoot = null;

        Assert.Equal(CliExit.NotARepository, Run(out _, "unpin"));
    }

    [Fact]
    public void Set_identity_is_picked_up_by_the_next_switch()
    {
        Run(out _, "set-identity", "abhijitojha7", "--name", "Abhijit Ojha", "--email", "ojha@example.com");
        Run(out _, "use", "abhijitojha7");

        Assert.Equal("Abhijit Ojha", _h.GlobalConfig[GitConfigKeys.CommitName]);
        Assert.Equal("ojha@example.com", _h.GlobalConfig[GitConfigKeys.CommitEmail]);
    }

    [Fact]
    public void Set_identity_without_either_value_says_what_it_needs()
    {
        Assert.Equal(CliExit.Usage, Run(out var output, "set-identity", "abhijitojha7"));

        Assert.Contains("--name or --email", output);
    }

    [Fact]
    public void Remove_logs_the_account_out_of_the_credential_manager()
    {
        Assert.Equal(CliExit.Ok, Run(out var output, "remove", "abhijitojha7"));

        Assert.Contains("abhijitojha7", output);
        Assert.Equal(["github", "logout", "abhijitojha7"], Assert.Single(_h.GcmCalls).Arguments);
    }

    [Fact]
    public void Add_starts_the_sign_in_flow_and_then_lists_what_is_available()
    {
        Assert.Equal(CliExit.Ok, Run(out var output, "add"));

        Assert.Equal("login", _h.GcmCalls[0].Arguments[1]);
        Assert.Contains("aojha111", output);
    }

    [Fact]
    public void A_failure_from_git_is_reported_rather_than_left_as_a_stack_trace()
    {
        _h.GitFailure = ("fatal: The configuration file has an invalid entry", 128);

        var code = Run(out var output, "use", "abhijitojha7");

        Assert.Equal(CliExit.ToolFailed, code);
        Assert.Contains("invalid entry", output);
    }
}
