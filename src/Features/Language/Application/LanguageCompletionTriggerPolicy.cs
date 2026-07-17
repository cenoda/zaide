using System;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Locked Phase 10 M4 completion trigger policy.
/// <list type="number">
/// <item><b>Explicit:</b> <c>editor.triggerSuggest</c> (default <c>Ctrl+Space</c>) schedules
/// a request immediately when the session and active C# document are eligible.</item>
/// <item><b>Automatic:</b> only when the typed character is advertised in
/// <see cref="LanguageServerCapabilities.CompletionTriggerCharacters"/>; debounced and cancellable.</item>
/// <item><b>Empty / unsupported / failed:</b> dismiss or do not open the popup.</item>
/// <item><b>Retrigger:</b> a newer explicit or automatic request cancels the prior in-flight request.</item>
/// </list>
/// No speculative prefix heuristics or additional trigger characters are invented client-side.
/// </summary>
public static class LanguageCompletionTriggerPolicy
{
    /// <summary>Command id for explicit completion trigger.</summary>
    public const string ExplicitCommandId = "editor.triggerSuggest";

    /// <summary>Default explicit keyboard gesture.</summary>
    public static readonly string[] ExplicitDefaultGestures = { "Ctrl+Space" };

    /// <summary>Debounce for automatic trigger-character requests.</summary>
    public static readonly TimeSpan AutomaticDebounce = TimeSpan.FromMilliseconds(200);
}
