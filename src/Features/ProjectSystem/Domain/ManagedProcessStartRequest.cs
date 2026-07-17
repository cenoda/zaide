namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Arguments required to start one redirected managed process.
/// </summary>
/// <param name="FileName">Executable file name, typically <c>dotnet</c>.</param>
/// <param name="Arguments">Process arguments excluding the executable name.</param>
/// <param name="WorkingDirectory">Child process working directory.</param>
/// <param name="Generation">
/// Workflow operation generation used to tag streamed output lines.
/// </param>
public sealed record ManagedProcessStartRequest(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    long Generation);
