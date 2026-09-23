using GitProfile.Core;

namespace GitProfile.Tests.Fakes;

public sealed record RecordedCall(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory)
{
    public string CommandLine => FileName + " " + string.Join(' ', Arguments);
}

/// <summary>Records every invocation and answers from <see cref="Responder"/>. No real processes are started.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public List<RecordedCall> Calls { get; } = [];

    public Func<RecordedCall, ProcessResult> Responder { get; set; } =
        _ => new ProcessResult(0, "", "");

    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var call = new RecordedCall(fileName, arguments, workingDirectory);
        Calls.Add(call);
        return Responder(call);
    }
}
