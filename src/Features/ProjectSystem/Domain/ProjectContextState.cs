namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// All possible states of the project-context service's current snapshot.
/// </summary>
public enum ProjectContextState
{
    /// <summary>No workspace is open. <c>WorkspaceRoot</c> is null.</summary>
    Unloaded,

    /// <summary>Discovery is in progress (transient).</summary>
    Loading,

    /// <summary>The root contains no project-like file.</summary>
    NoProject,

    /// <summary>The root contains known project files, but none are supported.</summary>
    Unsupported,

    /// <summary>Exactly one supported candidate exists and is auto-selected.</summary>
    SingleProject,

    /// <summary>Multiple supported candidates exist; user selection is required.</summary>
    Ambiguous,

    /// <summary>The user explicitly selected a candidate from the current snapshot.</summary>
    Selected,

    /// <summary>Discovery failed because of an I/O or permission error.</summary>
    Failed,
}
