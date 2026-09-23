using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

public class GcmProfileSourceTests
{
    private static readonly ToolPaths Tools = new("git", "git-credential-manager");

    private static (GcmProfileSource Source, FakeProcessRunner Runner) Create(string listOutput, int exitCode = 0)
    {
        var runner = new FakeProcessRunner
        {
            Responder = call => call.Arguments.Count > 0 && call.Arguments[0] == "github"
                ? new ProcessResult(exitCode, listOutput, exitCode == 0 ? "" : "boom")
                : new ProcessResult(0, "", "")
        };
        return (new GcmProfileSource(runner, Tools), runner);
    }

    [Fact]
    public void ListAccounts_returns_one_profile_per_line()
    {
        var (source, _) = Create("aojha111\nabhijitojha7\n");

        var accounts = source.ListAccounts();

        Assert.Equal(new[] { "aojha111", "abhijitojha7" }, accounts);
    }

    [Fact]
    public void ListAccounts_invokes_github_list_on_the_credential_manager()
    {
        var (source, runner) = Create("");

        source.ListAccounts();

        var call = Assert.Single(runner.Calls);
        Assert.Equal("git-credential-manager", call.FileName);
        Assert.Equal(new[] { "github", "list" }, call.Arguments);
    }

    [Fact]
    public void ListAccounts_drops_blank_and_padded_lines()
    {
        var (source, _) = Create("  \naojha111  \n\n");

        Assert.Equal(new[] { "aojha111" }, source.ListAccounts());
    }

    [Fact]
    public void ListAccounts_lists_the_same_login_once()
    {
        var (source, _) = Create("aojha111\nAOJHA111\n");

        Assert.Equal(new[] { "aojha111" }, source.ListAccounts());
    }

    [Fact]
    public void ListAccounts_throws_when_the_credential_manager_fails()
    {
        var (source, _) = Create("", exitCode: 1);

        var error = Assert.Throws<ToolFailedException>(source.ListAccounts);
        Assert.Contains("boom", error.Message);
    }

    [Fact]
    public void Logout_removes_the_named_account()
    {
        var (source, runner) = Create("");

        source.Logout("abhijitojha7");

        var call = Assert.Single(runner.Calls);
        Assert.Equal(new[] { "github", "logout", "abhijitojha7" }, call.Arguments);
    }

    [Fact]
    public void Login_runs_the_interactive_credential_manager_flow()
    {
        var (source, runner) = Create("");

        source.Login();

        var call = Assert.Single(runner.Calls);
        Assert.Equal(new[] { "github", "login" }, call.Arguments);
    }
}
