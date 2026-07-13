using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Locked M5 policies for Go to Definition commands and multi-result ordering.
/// </summary>
public static class LanguageNavigationPolicy
{
    /// <summary>Stable command id for Go to Definition.</summary>
    public const string GoToDefinitionCommandId = "editor.goToDefinition";

    /// <summary>Default gesture: F12.</summary>
    public static IReadOnlyList<string> GoToDefinitionDefaultGestures { get; } = new[] { "F12" };

    /// <summary>Truthful feedback when zero definitions are returned.</summary>
    public const string NotFoundMessage = "No definition found.";

    /// <summary>Truthful feedback when definition is unsupported or session is not ready.</summary>
    public const string UnavailableMessage = "Go to Definition is not available.";

    /// <summary>Truthful feedback when the response is invalid or unusable.</summary>
    public const string InvalidMessage = "Definition location is invalid.";

    /// <summary>Truthful feedback when the request failed.</summary>
    public const string FailedMessage = "Go to Definition failed.";
}
