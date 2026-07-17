namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Locked default argv and working directory for one workflow operation.
/// </summary>
/// <param name="FileName">Executable file name, typically <c>dotnet</c>.</param>
/// <param name="Arguments">Arguments excluding the executable name.</param>
/// <param name="WorkingDirectory">Child process working directory.</param>
public sealed record ProjectExecutionProfile(
    string FileName,
    string Arguments,
    string WorkingDirectory);
