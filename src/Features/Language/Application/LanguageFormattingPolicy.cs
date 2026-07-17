using System.Collections.Generic;
using Zaide.Features.Editor.Domain;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Locked M6 policies for whole-document formatting and Format on Save.
/// </summary>
public static class LanguageFormattingPolicy
{
    /// <summary>Stable command id for Format Document.</summary>
    public const string FormatDocumentCommandId = "editor.formatDocument";

    /// <summary>Default gesture: Ctrl+Shift+I.</summary>
    public static IReadOnlyList<string> FormatDocumentDefaultGestures { get; } =
        new[] { "Ctrl+Shift+I" };

    /// <summary>Truthful feedback when formatting is not available.</summary>
    public const string UnavailableMessage = "Format Document is not available.";

    /// <summary>Truthful feedback when the server does not support formatting.</summary>
    public const string UnsupportedMessage = "Document formatting is not supported.";

    /// <summary>Truthful feedback when the request failed.</summary>
    public const string FailedMessage = "Format Document failed.";

    /// <summary>Truthful feedback when returned edits are unsafe.</summary>
    public const string InvalidMessage = "Formatting result is invalid.";

    /// <summary>Truthful feedback when the request was cancelled or went stale.</summary>
    public const string CancelledMessage = "Format Document cancelled.";

    /// <summary>Truthful feedback when there is nothing to change.</summary>
    public const string NoEditsMessage = "Document is already formatted.";

    /// <summary>Truthful feedback after a successful format apply.</summary>
    public const string AppliedMessage = "Document formatted.";
}
