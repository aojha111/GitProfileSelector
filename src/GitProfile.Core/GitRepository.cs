namespace GitProfile.Core;

/// <summary>Asks git about the repository a path belongs to.</summary>
public sealed class GitRepository(IProcessRunner runner, ToolPaths tools)
{
    public string? FindRoot(string startPath) => Read("rev-parse", "--show-toplevel", startPath);

    public string? OriginUrl(string repoRoot) => Read("remote", "get-url", "origin", repoRoot);

    /// <summary>Runs a read-only git command, treating "not found" as no answer rather than an error.</summary>
    private string? Read(params string[] argumentsIncludingCwd)
    {
        var workingDirectory = argumentsIncludingCwd[^1];
        var arguments = argumentsIncludingCwd[..^1];
        var result = runner.Run(tools.Git, arguments, workingDirectory);
        if (!result.Success)
            return null;
        var value = result.StandardOutput.Trim();
        return value.Length == 0 ? null : value;
    }
}

/// <summary>Reads the owning account or organisation out of a git remote URL.</summary>
public static class GitHubUrl
{
    public static string? OwnerOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var text = url.Trim();
        string host, path;

        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        var colon = text.IndexOf(':');
        var firstSlash = text.IndexOf('/');
        if (schemeEnd < 0 && colon > 0 && (firstSlash < 0 || colon < firstSlash))
        {
            // scp-style: git@github.com:owner/repo.git
            host = text[..colon];
            path = text[(colon + 1)..];
            var at = host.LastIndexOf('@');
            if (at >= 0)
                host = host[(at + 1)..];
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var parsed) && !string.IsNullOrEmpty(parsed.Host))
        {
            host = parsed.Host;
            path = parsed.AbsolutePath;
        }
        else
        {
            return null;
        }

        if (!host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? null : segments[0];
    }
}
