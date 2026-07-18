using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Narrow deterministic mention parser for M2.
/// Supports zero or one explicit @AgentName target only.
/// Case-insensitive exact match on visible agent names supplied by the caller.
/// Strips matched mention token; returns explicit failure for all error cases.
/// No mention present → direct-send intent (IsDirectSend = true).
/// </summary>
public sealed class MentionParser
{
    public RouteResult Parse(
        string sourcePanelId,
        string rawInput,
        IReadOnlyList<string> visibleAgentNames)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return new RouteResult(false, null, "Empty input");
        }

        var tokens = rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mentionTokens = tokens.Where(t => t.StartsWith("@")).ToList();

        if (mentionTokens.Count == 0)
        {
            // Direct send intent
            return new RouteResult(true, new RouteRequest(sourcePanelId, null, rawInput.Trim(), true), null);
        }

        if (mentionTokens.Count > 1)
        {
            return new RouteResult(false, null, "Multiple mentions");
        }

        var mention = mentionTokens[0];
        var name = mention.Substring(1);
        if (string.IsNullOrEmpty(name))
        {
            return new RouteResult(false, null, "Empty mention target");
        }

        var matchingNames = visibleAgentNames
            .Where(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingNames.Count == 0)
        {
            return new RouteResult(false, null, "Unknown target");
        }

        if (matchingNames.Count > 1)
        {
            return new RouteResult(false, null, "Ambiguous target");
        }

        var targetName = matchingNames[0];
        var stripped = System.Text.RegularExpressions.Regex.Replace(
            rawInput.Replace(mention, " "), @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(stripped))
        {
            return new RouteResult(false, null, "Empty content after stripping");
        }

        var request = new RouteRequest(sourcePanelId, targetName, stripped, false);
        return new RouteResult(true, request, null);
    }
}
