namespace GitProfile.Core;

/// <summary>Reads and writes the GitHub accounts Git Credential Manager knows about.</summary>
public sealed class GcmProfileSource(IProcessRunner runner, ToolPaths tools)
{
    public IReadOnlyList<string> ListAccounts()
    {
        var result = Run("github", "list");
        var accounts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in result.StandardOutput.Split('\n'))
        {
            var account = line.Trim();
            if (account.Length > 0 && seen.Add(account))
                accounts.Add(account);
        }
        return accounts;
    }

    public void Login() => Run("github", "login");

    public void Logout(string account) => Run("github", "logout", account);

    private ProcessResult Run(params string[] arguments)
    {
        var result = runner.Run(tools.CredentialManager, arguments);
        if (!result.Success)
            throw new ToolFailedException(tools.CredentialManager, arguments, result);
        return result;
    }
}
