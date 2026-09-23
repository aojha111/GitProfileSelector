using System.IO;
using GitProfile.Core;

namespace GitProfile.App;

/// <summary>
/// The same tool without a window: when the exe gets arguments it behaves like a command line program
/// and never opens one, so it can be used from a terminal or a script.
/// </summary>
internal static class CommandLineHost
{
    public static int Run(IReadOnlyList<string> args)
    {
        App.AttachToTerminal();
        var output = Console.Out;

        return CommandLine.Parse(args) switch
        {
            ParseOutcome.Help => Help(output, CliExit.Ok),
            ParseOutcome.Rejected rejected => Usage(output, rejected.Reason),
            ParseOutcome.Request request => Execute(output, request),
            _ => Usage(output, $"'{string.Join(' ', args)}' is not a GitProfile command."),
        };
    }

    private static int Execute(TextWriter output, ParseOutcome.Request request)
    {
        Services services;
        try
        {
            services = Services.Build();
        }
        catch (GitProfileSetupException ex)
        {
            output.WriteLine(ex.Message);
            return CliExit.ToolFailed;
        }

        return services.Cli(output).Run(request, Environment.CurrentDirectory);
    }

    private static int Help(TextWriter output, int code)
    {
        output.WriteLine(CommandLine.HelpText);
        return code;
    }

    private static int Usage(TextWriter output, string reason)
    {
        output.WriteLine(reason);
        output.WriteLine();
        output.WriteLine(CommandLine.HelpText);
        return CliExit.Usage;
    }
}
