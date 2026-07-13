using System;

namespace Zaide.Services;

/// <summary>
/// Locked Phase 10 M4 hover trigger policy.
/// <list type="number">
/// <item>Hover schedules after caret movement stops for <see cref="DwellDelay"/>.</item>
/// <item>Any caret, document, tab, or session change cancels or replaces the pending request.</item>
/// <item>Hover never blocks typing; failures dismiss silently.</item>
/// </list>
/// </summary>
public static class LanguageHoverTriggerPolicy
{
    /// <summary>Bounded dwell before issuing <c>textDocument/hover</c>.</summary>
    public static readonly TimeSpan DwellDelay = TimeSpan.FromMilliseconds(450);
}
