namespace GitProfile.Core;

/// <summary>Locates git and Git Credential Manager on this machine.</summary>
public static class ToolLocator
{
    public const string GitExecutable = "git.exe";
    public const string CredentialManagerExecutable = "git-credential-manager.exe";

    /// <summary>Resolves both tools, or null when either is missing. Pure so it can be tested without a disk.</summary>
    public static ToolPaths? Find(IReadOnlyList<string> directories, Func<string, bool> exists)
    {
        var git = FirstExisting(directories, GitExecutable, exists);
        if (git is null)
            return null;

        var credentialManager = FirstExisting(directories, CredentialManagerExecutable, exists)
                                ?? FirstExisting(NearGit(git), CredentialManagerExecutable, exists);

        return credentialManager is null ? null : new ToolPaths(git, credentialManager);
    }

    /// <summary>Resolves the tools using this machine's PATH and common Git install locations.</summary>
    public static ToolPaths? FindCurrent() => Find(SearchPaths(), File.Exists);

    /// <summary>PATH plus the usual Git for Windows folders, so a tool missing from PATH is still found.</summary>
    public static IReadOnlyList<string> SearchPaths()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var programFiles in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                             Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            if (programFiles.Length == 0)
                continue;
            var gitRoot = Path.Combine(programFiles, "Git");
            directories.Add(Path.Combine(gitRoot, "cmd"));
            directories.Add(Path.Combine(gitRoot, "mingw64", "bin"));
        }

        return directories;
    }

    private static string? FirstExisting(IEnumerable<string> directories, string executable, Func<string, bool> exists)
    {
        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory, executable);
            if (exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Folders within the Git installation that holds <paramref name="gitPath"/>. Git itself sits in
    /// &lt;root&gt;\cmd while Git Credential Manager ships in &lt;root&gt;\mingw64\bin.
    /// </summary>
    private static IEnumerable<string> NearGit(string gitPath)
    {
        var folder = Path.GetDirectoryName(gitPath);
        if (folder is null)
            yield break;

        yield return folder;

        var root = Path.GetDirectoryName(folder);
        if (root is null)
            yield break;

        yield return root;
        yield return Path.Combine(root, "cmd");
        yield return Path.Combine(root, "bin");
        yield return Path.Combine(root, "mingw64", "bin");
        yield return Path.Combine(root, "usr", "bin");
    }
}
