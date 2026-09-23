using System.Diagnostics;
using System.Text;

namespace GitProfile.Core;

/// <summary>Starts real processes.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // ArgumentList quotes each value itself, so a path or name with spaces cannot split or inject.
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (workingDirectory is not null)
            startInfo.WorkingDirectory = workingDirectory;

        using var process = Process.Start(startInfo)
            ?? throw new ToolLaunchException(fileName, $"Could not start {fileName}.");

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output.Result, error.Result);
    }
}

/// <summary>The tool could not be started at all.</summary>
public sealed class ToolLaunchException(string tool, string message) : Exception($"{tool}: {message}");
