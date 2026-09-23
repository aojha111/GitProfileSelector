using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

public class GitRepositoryTests
{
    private static (GitRepository Repo, FakeProcessRunner Runner) Create(
        Func<IReadOnlyList<string>, ProcessResult> answer)
    {
        var runner = new FakeProcessRunner { Responder = call => answer(call.Arguments) };
        return (new GitRepository(runner, new ToolPaths("git", "gcm")), runner);
    }

    private static ProcessResult Ok(string output) => new(0, output, "");

    [Fact]
    public void FindRoot_asks_git_for_the_top_level_of_the_starting_directory()
    {
        var (repo, runner) = Create(_ => Ok("C:/repos/app\n"));

        Assert.Equal("C:/repos/app", repo.FindRoot(@"C:\repos\app\src"));

        var call = Assert.Single(runner.Calls);
        Assert.Equal(["rev-parse", "--show-toplevel"], call.Arguments);
        Assert.Equal(@"C:\repos\app\src", call.WorkingDirectory);
    }

    [Fact]
    public void FindRoot_returns_null_outside_a_repository()
    {
        var (repo, _) = Create(_ => new ProcessResult(128, "", "fatal: not a git repository"));

        Assert.Null(repo.FindRoot(@"C:\Users\me"));
    }

    [Fact]
    public void OriginUrl_reads_the_origin_remote()
    {
        var (repo, _) = Create(args => args.Contains("get-url")
            ? Ok("https://github.com/abhijitojha7/knowledge-hub.git\n")
            : new ProcessResult(1, "", ""));

        Assert.Equal("https://github.com/abhijitojha7/knowledge-hub.git", repo.OriginUrl("C:/repos/app"));
    }

    [Fact]
    public void OriginUrl_returns_null_when_the_repo_has_no_origin()
    {
        var (repo, _) = Create(_ => new ProcessResult(2, "", "error: No such remote 'origin'"));

        Assert.Null(repo.OriginUrl("C:/repos/app"));
    }

    [Theory]
    [InlineData("https://github.com/abhijitojha7/knowledge-hub.git", "abhijitojha7")]
    [InlineData("https://github.com/abhijitojha7/knowledge-hub", "abhijitojha7")]
    [InlineData("git@github.com:aojha111/whiteboard.git", "aojha111")]
    [InlineData("ssh://git@github.com/aojha111/whiteboard.git", "aojha111")]
    [InlineData("https://gitlab.com/someone/thing.git", null)]
    [InlineData(null, null)]
    public void OwnerOf_names_the_account_or_org_that_owns_the_url(string? url, string? expected)
    {
        Assert.Equal(expected, GitHubUrl.OwnerOf(url));
    }
}
