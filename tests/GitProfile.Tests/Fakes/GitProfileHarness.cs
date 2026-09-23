using GitProfile.Core;

namespace GitProfile.Tests.Fakes;

/// <summary>
/// A scripted stand-in for git and Git Credential Manager that keeps real state, so scope
/// precedence (local overriding global) is exercised rather than asserted from a mock's memory.
/// </summary>
public sealed class GitProfileHarness
{
    public List<string> Accounts { get; } = ["aojha111", "abhijitojha7"];
    public string? RepoRoot { get; set; }
    public string? Origin { get; set; }
    public Dictionary<string, string> GlobalConfig { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> LocalConfig { get; } = new(StringComparer.Ordinal);
    public List<IReadOnlyList<string>> GitCalls { get; } = [];
    public List<RecordedCall> GcmCalls { get; } = [];
    public (string Message, int ExitCode)? GitFailure { get; set; }
    public string IdentityFile { get; } =
        Path.Combine(Path.GetTempPath(), $"gitprofile-{Guid.NewGuid():N}.json");

    public GitProfileHarness()
    {
        var runner = new FakeProcessRunner { Responder = Answer };
        var tools = new ToolPaths("git", "gcm");
        AccountsSource = new GcmProfileSource(runner, tools);
        Config = new GitConfig(runner, tools);
        Repository = new GitRepository(runner, tools);
        Identities = new IdentityStore(IdentityFile);
        Service = new ProfileService(AccountsSource, Config, Repository, Identities);
    }

    public GcmProfileSource AccountsSource { get; }
    public GitConfig Config { get; }
    public GitRepository Repository { get; }
    public IdentityStore Identities { get; }
    public ProfileService Service { get; }

    public void SetIdentity(string login, string? name, string? email) => Identities.Save(login, name, email);

    private ProcessResult Answer(RecordedCall call) =>
        call.FileName == "gcm" ? AnswerGcm(call.Arguments) : AnswerGit(call);

    private ProcessResult AnswerGcm(IReadOnlyList<string> args)
    {
        GcmCalls.Add(new RecordedCall("gcm", args, null));
        if (args.Count > 0 && args[0] == "github" && args.Count > 1 && args[1] == "list")
            return Ok(string.Join('\n', Accounts) + "\n");
        return Ok("");
    }

    private ProcessResult AnswerGit(RecordedCall call)
    {
        var args = call.Arguments;
        GitCalls.Add(args);
        if (GitFailure is { } failure)
            return Fail(failure.ExitCode, failure.Message);
        return args[0] switch
        {
            "config" => AnswerConfig(args, call.WorkingDirectory),
            "rev-parse" => IsInsideRepo(call.WorkingDirectory) ? Ok(RepoRoot! + "\n") : Fail(128, "fatal: not a git repository"),
            "remote" => Origin is null ? Fail(2, "error: No such remote 'origin'") : Ok(Origin + "\n"),
            _ => Ok(""),
        };
    }

    /// <summary>A repository only exists, to git, where the caller actually is standing.</summary>
    private bool IsInsideRepo(string? where)
    {
        if (RepoRoot is null || where is null)
            return false;
        var root = RepoRoot.Replace('\\', '/').TrimEnd('/');
        var target = where.Replace('\\', '/').TrimEnd('/');
        return target.Equals(root, StringComparison.OrdinalIgnoreCase)
               || target.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }

    private ProcessResult AnswerConfig(IReadOnlyList<string> args, string? workingDirectory)
    {
        var store = args[1] switch
        {
            "--global" => GlobalConfig,
            "--local" when workingDirectory is not null => LocalConfig,
            "--local" => null,
            _ => null,
        };
        if (store is null)
            return Fail(128, "fatal: --local can only be used inside a git repository");

        var tail = args.Skip(2).ToList();

        switch (tail[0])
        {
            case "--get":
                return store.TryGetValue(tail[1], out var found) ? Ok(found + "\n") : Fail(1, "");
            case "--unset":
                return store.Remove(tail[1]) ? Ok("") : Fail(5, "error: key does not exist");
            default:
                store[tail[0]] = tail[1];
                return Ok("");
        }
    }

    private static ProcessResult Ok(string output) => new(0, output, "");

    private static ProcessResult Fail(int exitCode, string error) => new(exitCode, "", error);
}
