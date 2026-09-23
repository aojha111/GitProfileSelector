using GitProfile.Core;

namespace GitProfile.Tests;

public class ToolLocatorTests
{
    private static ToolPaths? Find(IReadOnlyList<string> directories, params string[] present)
    {
        var onDisk = new HashSet<string>(present, StringComparer.OrdinalIgnoreCase);
        return ToolLocator.Find(directories, onDisk.Contains);
    }

    [Fact]
    public void Finds_git_and_the_credential_manager_on_the_search_path()
    {
        var dirs = new[] { @"C:\Program Files\Git\cmd", @"C:\Program Files\Git\mingw64\bin" };
        var present = new[]
        {
            @"C:\Program Files\Git\cmd\git.exe",
            @"C:\Program Files\Git\mingw64\bin\git-credential-manager.exe",
        };

        var tools = Find(dirs, present);

        Assert.Equal(@"C:\Program Files\Git\cmd\git.exe", tools!.Git);
        Assert.Equal(@"C:\Program Files\Git\mingw64\bin\git-credential-manager.exe", tools.CredentialManager);
    }

    [Fact]
    public void Looks_beside_git_for_the_credential_manager_when_it_is_not_on_the_path()
    {
        var dirs = new[] { @"C:\Program Files\Git\cmd" };
        var present = new[]
        {
            @"C:\Program Files\Git\cmd\git.exe",
            @"C:\Program Files\Git\mingw64\bin\git-credential-manager.exe",
        };

        var tools = Find(dirs, present);

        Assert.Equal(@"C:\Program Files\Git\mingw64\bin\git-credential-manager.exe", tools!.CredentialManager);
    }

    [Fact]
    public void Reports_nothing_when_git_is_absent()
    {
        Assert.Null(Find(new[] { @"C:\somewhere" }, @"C:\somewhere\git-credential-manager.exe"));
    }

    [Fact]
    public void Reports_nothing_when_the_credential_manager_is_absent()
    {
        Assert.Null(Find(new[] { @"C:\Program Files\Git\cmd" }, @"C:\Program Files\Git\cmd\git.exe"));
    }

    [Fact]
    public void Prefers_the_first_directory_that_has_git()
    {
        var dirs = new[] { @"C:\first", @"C:\second" };
        var present = new[]
        {
            @"C:\second\git.exe",
            @"C:\second\git-credential-manager.exe",
        };

        Assert.Equal(@"C:\second\git.exe", Find(dirs, present)!.Git);
    }
}
