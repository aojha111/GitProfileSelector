namespace GitProfile.Core;

/// <summary>Outcome of running an external tool.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

/// <summary>Runs an external process and captures its output. Seam for tests.</summary>
public interface IProcessRunner
{
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);
}

/// <summary>Where the external tools live.</summary>
public sealed record ToolPaths(string Git, string CredentialManager);
