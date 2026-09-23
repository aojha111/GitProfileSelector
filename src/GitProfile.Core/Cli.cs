namespace GitProfile.Core;

/// <summary>What a parsed command line turned out to be.</summary>
public abstract record ParseOutcome
{
    public sealed record Help(string? Verb) : ParseOutcome;

    public sealed record Request(
        string Verb,
        string? Account,
        string? Path,
        bool DryRun,
        string? Name,
        string? Email) : ParseOutcome;

    public sealed record Rejected(string Reason) : ParseOutcome;
}

/// <summary>Exit codes the command line returns, so scripts can branch on them.</summary>
public static class CliExit
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int Usage = 2;
    public const int UnknownAccount = 3;
    public const int NotARepository = 4;
    public const int ToolFailed = 5;
}

public static class CommandLine
{
    /// <summary>Verbs in the order the help screen lists them.</summary>
    public static readonly IReadOnlyList<string> Verbs =
        ["list", "current", "use", "pin", "unpin", "add", "remove", "set-identity", "help"];

    public const string HelpText = """
        GitProfile — choose which GitHub account git pushes as, so Git Credential Manager stops asking.

          GitProfile list                       signed-in accounts
          GitProfile current                    what git would use here
          GitProfile use <account>              make it the default for this Windows user
          GitProfile pin <account> [path]       use it for one repository only
          GitProfile unpin [path]               stop pinning a repository
          GitProfile add                        sign in another GitHub account
          GitProfile remove <account>           forget an account's saved sign-in
          GitProfile set-identity <account> --name <name> --email <email>
                                                how that account signs commits
          GitProfile help                       this screen
          GitProfile <verb> --dry-run           show the git config changes without making them

        A repository pin overrides the user default, so repositories from different accounts
        stop overwriting each other's cached credential.
        """;

    private static readonly HashSet<string> AccountVerbs = ["use", "remove", "set-identity", "pin"];

    public static ParseOutcome Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return new ParseOutcome.Help(null);

        var verb = Normalise(args[0]);
        if (verb is null)
            return new ParseOutcome.Rejected(
                $"Unknown command '{args[0]}'. Valid commands: {string.Join(", ", Verbs)}.");

        if (verb == "help")
            return new ParseOutcome.Help(null);

        var positional = new List<string>();
        bool dryRun = false;
        string? name = null, email = null;

        for (var i = 1; i < args.Count; i++)
        {
            var token = args[i];
            switch (token)
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--name" or "--email":
                    if (verb != "set-identity")
                        return new ParseOutcome.Rejected($"{token} is only meaningful for set-identity.");
                    if (i + 1 >= args.Count)
                        return new ParseOutcome.Rejected($"Missing value after {token}.");
                    if (token == "--name") name = args[++i]; else email = args[++i];
                    break;
                default:
                    if (token.StartsWith('-'))
                        return new ParseOutcome.Rejected($"Unknown option {token}.");
                    positional.Add(token);
                    break;
            }
        }

        var maxPositional = verb switch
        {
            "use" or "remove" or "set-identity" => 1,
            "pin" => 2,
            "unpin" => 1,
            _ => 0,
        };
        if (positional.Count > maxPositional)
            return new ParseOutcome.Rejected($"'{verb}' takes at most {maxPositional} argument(s).");

        string? account = null, path = null;
        if (AccountVerbs.Contains(verb))
        {
            if (positional.Count == 0)
                return new ParseOutcome.Rejected($"'{verb}' needs an account name.");
            account = positional[0];
        }
        path = verb switch
        {
            "pin" when positional.Count > 1 => positional[1],
            "unpin" when positional.Count > 0 => positional[0],
            _ => null,
        };

        return new ParseOutcome.Request(verb, account, path, dryRun, name, email);
    }

    private static string? Normalise(string token)
    {
        var verb = token.ToLowerInvariant();
        if (verb is "--help" or "-h")
            return "help";
        if (verb == "status")
            return "current";
        return Verbs.Contains(verb) ? verb : null;
    }
}

/// <summary>Carries out one parsed command line verb.</summary>
public sealed class CliRunner(
    ProfileService service,
    GcmProfileSource accounts,
    IdentityStore identities,
    TextWriter output)
{
    public int Run(ParseOutcome.Request request, string workingDirectory)
    {
        try
        {
            return request.Verb switch
            {
                "list" => List(),
                "current" => Current(workingDirectory),
                "use" => Use(request),
                "pin" => Pin(request, workingDirectory),
                "unpin" => Unpin(request, workingDirectory),
                "add" => Add(),
                "remove" => Remove(request),
                "set-identity" => SetIdentity(request),
                _ => Usage($"Nothing known about '{request.Verb}'."),
            };
        }
        catch (ProfileNotFoundException ex)
        {
            output.WriteLine(ex.Message);
            return CliExit.UnknownAccount;
        }
        catch (NotInRepositoryException ex)
        {
            output.WriteLine(ex.Message);
            return CliExit.NotARepository;
        }
        catch (ToolFailedException ex)
        {
            output.WriteLine(ex.Message);
            return CliExit.ToolFailed;
        }
        catch (IdentityStoreCorruptException ex)
        {
            output.WriteLine(ex.Message);
            return CliExit.Failed;
        }
    }

    private int List()
    {
        foreach (var account in service.ListProfiles())
            output.WriteLine(account);
        return CliExit.Ok;
    }

    private int Current(string workingDirectory)
    {
        var status = service.Status(workingDirectory);

        output.WriteLine(status.DefaultLogin is null
            ? "no default account: Git Credential Manager will ask which account to use"
            : $"default account: {status.DefaultLogin}");

        if (!status.InRepository)
        {
            output.WriteLine("repository: not inside a git repository");
            return CliExit.Ok;
        }

        output.WriteLine($"repository: {status.RepoRoot}");
        if (status.PinnedLogin is not null)
            output.WriteLine($"pinned account: {status.PinnedLogin}");
        if (status.OriginOwner is not null)
            output.WriteLine($"origin owner: {status.OriginOwner}");
        output.WriteLine($"effective account: {status.EffectiveLogin ?? "(none — you will be asked)"}");
        return CliExit.Ok;
    }

    private int Use(ParseOutcome.Request request) =>
        Apply(request.DryRun, service.PlanSetDefault(request.Account!), repoPath: null,
            $"default account is now {request.Account}");

    private int Pin(ParseOutcome.Request request, string workingDirectory)
    {
        var repoRoot = RequireRepoRoot(workingDirectory);
        if (repoRoot is null)
            return CliExit.NotARepository;

        return Apply(request.DryRun, service.PlanPin(request.Account!, repoRoot), repoRoot,
            $"pinned {request.Account} to {repoRoot}");
    }

    private int Unpin(ParseOutcome.Request request, string workingDirectory)
    {
        var repoRoot = request.Path ?? workingDirectory;
        if (service.Status(repoRoot).RepoRoot is null)
        {
            output.WriteLine(new NotInRepositoryException(repoRoot).Message);
            return CliExit.NotARepository;
        }

        return Apply(request.DryRun, service.PlanClearPin(repoRoot), repoRoot,
            $"unpinned {repoRoot} — it follows the default account again");
    }

    private int Add()
    {
        accounts.Login();
        output.WriteLine("Signed-in accounts:");
        return List();
    }

    private int Remove(ParseOutcome.Request request)
    {
        accounts.Logout(request.Account!);
        output.WriteLine($"removed the saved sign-in for {request.Account}");
        return CliExit.Ok;
    }

    private int SetIdentity(ParseOutcome.Request request)
    {
        if (request.Name is null && request.Email is null)
            return Usage("set-identity needs --name or --email (or both).");

        identities.Save(request.Account!, request.Name, request.Email);
        output.WriteLine($"saved commit identity for {request.Account}");
        return CliExit.Ok;
    }

    private string? RequireRepoRoot(string workingDirectory)
    {
        var repoRoot = service.Status(workingDirectory).RepoRoot;
        if (repoRoot is null)
            output.WriteLine(new NotInRepositoryException(workingDirectory).Message);
        return repoRoot;
    }

    private int Apply(bool dryRun, IReadOnlyList<ConfigEdit> edits, string? repoPath, string done)
    {
        if (dryRun)
        {
            output.WriteLine("would run:");
            foreach (var edit in edits)
                output.WriteLine("  " + edit.Describe());
            return CliExit.Ok;
        }

        service.Apply(edits, repoPath);
        foreach (var edit in edits)
            output.WriteLine(edit.Describe());
        output.WriteLine(done);
        return CliExit.Ok;
    }

    private int Usage(string reason)
    {
        output.WriteLine(reason);
        return CliExit.Usage;
    }
}
