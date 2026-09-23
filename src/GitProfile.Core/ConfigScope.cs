namespace GitProfile.Core;

/// <summary>Which git config file a key lives in.</summary>
public enum ConfigScope
{
    /// <summary>~/.gitconfig — applies to every repository for this Windows user.</summary>
    Global,

    /// <summary>&lt;repo&gt;/.git/config — overrides <see cref="Global"/> for one repository.</summary>
    Local,
}

public static class ConfigScopeExtensions
{
    /// <summary>The git command line switch naming this scope.</summary>
    public static string Flag(this ConfigScope scope) => scope == ConfigScope.Global ? "--global" : "--local";
}

/// <summary>The keys GitProfile owns.</summary>
public static class GitConfigKeys
{
    /// <summary>
    /// Tells Git Credential Manager which GitHub account to authenticate as, so it never has to ask.
    /// Without it GCM shows its "Select an account" dialog every time two accounts interleave.
    /// </summary>
    public const string GitHubAccount = "credential.https://github.com.username";

    public const string CommitName = "user.name";
    public const string CommitEmail = "user.email";
}
