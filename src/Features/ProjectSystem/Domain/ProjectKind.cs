namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Supported project kinds that Zaide can recognise and work with.
/// Derived from the file extension during discovery.
/// </summary>
public enum ProjectKind
{
    /// <summary>Visual Studio solution (.sln)</summary>
    Solution,

    /// <summary>Visual Studio solution model (.slnx)</summary>
    SolutionX,

    /// <summary>C# project (.csproj)</summary>
    CSharpProject,
}
