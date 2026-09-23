namespace GitProfile.Core;

/// <summary>An external tool (git / Git Credential Manager) reported a failure.</summary>
public sealed class ToolFailedException(string tool, IReadOnlyList<string> arguments, ProcessResult result)
    : Exception($"{tool} {string.Join(' ', arguments)} failed with exit code {result.ExitCode}."
                + Environment.NewLine
                + FirstLine(result.StandardError, result.StandardOutput))
{
    public string Tool { get; } = tool;
    public ProcessResult Result { get; } = result;

    private static string FirstLine(params string[] streams) =>
        streams.Where(s => s is not null)
               .SelectMany(s => s!.Split('\n'))
               .Select(l => l.Trim())
               .FirstOrDefault(l => l.Length > 0) ?? "(no output)";
}
