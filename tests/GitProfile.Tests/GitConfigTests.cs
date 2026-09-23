using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

public class GitConfigTests
{
    private const string PinKey = "credential.https://github.com.username";

    private static (GitConfig Config, FakeProcessRunner Runner) Create(ProcessResult? answer = null)
    {
        var runner = new FakeProcessRunner
        {
            Responder = _ => answer ?? new ProcessResult(0, "", "")
        };
        return (new GitConfig(runner, new ToolPaths("git", "gcm")), runner);
    }

    [Fact]
    public void Set_global_writes_the_key_at_user_scope()
    {
        var (config, runner) = Create();

        config.Set(ConfigScope.Global, PinKey, "aojha111");

        var call = Assert.Single(runner.Calls);
        Assert.Equal("git", call.FileName);
        Assert.Equal(["config", "--global", PinKey, "aojha111"], call.Arguments);
        Assert.Null(call.WorkingDirectory);
    }

    [Fact]
    public void Set_local_writes_the_key_inside_the_repository()
    {
        var (config, runner) = Create();

        config.Set(ConfigScope.Local, PinKey, "abhijitojha7", repoPath: @"C:\repos\app");

        var call = Assert.Single(runner.Calls);
        Assert.Equal(["config", "--local", PinKey, "abhijitojha7"], call.Arguments);
        Assert.Equal(@"C:\repos\app", call.WorkingDirectory);
    }

    [Fact]
    public void Get_returns_the_stored_value_without_trailing_newline()
    {
        var (config, _) = Create(new ProcessResult(0, "aojha111\n", ""));

        Assert.Equal("aojha111", config.Get(ConfigScope.Global, PinKey));
    }

    [Fact]
    public void Get_returns_null_when_the_key_is_not_set()
    {
        var (config, _) = Create(new ProcessResult(1, "", ""));

        Assert.Null(config.Get(ConfigScope.Global, PinKey));
    }

    [Fact]
    public void Get_throws_when_git_itself_errors()
    {
        var (config, _) = Create(new ProcessResult(128, "", "fatal: bad config file line 7"));

        Assert.Throws<ToolFailedException>(() => config.Get(ConfigScope.Global, PinKey));
    }

    [Fact]
    public void Get_returns_null_when_the_key_is_set_to_an_empty_value()
    {
        var (config, _) = Create(new ProcessResult(0, "\n", ""));

        Assert.Null(config.Get(ConfigScope.Global, PinKey));
    }

    [Fact]
    public void Unset_removes_the_key()
    {
        var (config, runner) = Create();

        config.Unset(ConfigScope.Local, "user.email", repoPath: @"C:\repos\app");

        Assert.Equal(["config", "--local", "--unset", "user.email"], Assert.Single(runner.Calls).Arguments);
    }

    [Fact]
    public void Unset_is_quiet_when_the_key_was_never_set()
    {
        var (config, _) = Create(new ProcessResult(5, "", "error: key does not exist"));

        config.Unset(ConfigScope.Global, PinKey);
    }
}
