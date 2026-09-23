namespace GitProfile.Core;

/// <summary>Reads and writes git configuration keys at user or repository scope.</summary>
public sealed class GitConfig(IProcessRunner runner, ToolPaths tools)
{
    public string? Get(ConfigScope scope, string key, string? repoPath = null)
    {
        var call = Invoke(scope, ["--get", key], repoPath);
        // git exits 1 for "no matching key", which is an ordinary answer rather than a failure.
        if (call.Result.ExitCode == 1)
            return null;
        if (!call.Result.Success)
            throw Failed(call);
        var value = call.Result.StandardOutput.Trim();
        return value.Length == 0 ? null : value;
    }

    public void Set(ConfigScope scope, string key, string value, string? repoPath = null)
    {
        var call = Invoke(scope, [key, value], repoPath);
        if (!call.Result.Success)
            throw Failed(call);
    }

    public void Unset(ConfigScope scope, string key, string? repoPath = null)
    {
        var call = Invoke(scope, ["--unset", key], repoPath);
        // git exits 5 when the key was never set — the state we asked for either way.
        if (call.Result.ExitCode is not (0 or 5))
            throw Failed(call);
    }

    private (IReadOnlyList<string> Arguments, ProcessResult Result) Invoke(
        ConfigScope scope, IReadOnlyList<string> tail, string? repoPath)
    {
        var arguments = new List<string> { "config", scope.Flag() };
        arguments.AddRange(tail);
        return (arguments, runner.Run(tools.Git, arguments, repoPath));
    }

    private ToolFailedException Failed((IReadOnlyList<string> Arguments, ProcessResult Result) call) =>
        new(tools.Git, call.Arguments, call.Result);
}
