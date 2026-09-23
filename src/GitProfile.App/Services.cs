using System.IO;
using GitProfile.Core;

namespace GitProfile.App;

/// <summary>Wires the real tools into the domain objects. Built once per process.</summary>
internal sealed class Services
{
    private Services(ProfileService profile, GcmProfileSource accounts, IdentityStore identities)
    {
        Profile = profile;
        Accounts = accounts;
        Identities = identities;
    }

    public ProfileService Profile { get; }
    public GcmProfileSource Accounts { get; }
    public IdentityStore Identities { get; }

    public static Services Build()
    {
        var tools = ToolLocator.FindCurrent()
            ?? throw new GitProfileSetupException(
                "Git and Git Credential Manager were not found. Install Git for Windows from https://git-scm.com/downloads");

        var runner = new SystemProcessRunner();
        var accounts = new GcmProfileSource(runner, tools);
        var identities = new IdentityStore(AppPaths.IdentityFile());
        var profile = new ProfileService(
            accounts,
            new GitConfig(runner, tools),
            new GitRepository(runner, tools),
            identities);

        return new Services(profile, accounts, identities);
    }

    public CliRunner Cli(TextWriter output) => new(Profile, Accounts, Identities, output);
}

/// <summary>Something is missing before GitProfile can do anything at all.</summary>
internal sealed class GitProfileSetupException(string message) : Exception(message);
