using System;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Pure compatibility projection from authoritative typed entries to current
/// Townhall presentation values.
/// </summary>
internal static class TownhallEntryProjection
{
    private const string ActionActivityPrefix = "zaide-action|v1|";
    private const string BackendActivityPrefix = "zaide-backend-activity|v1|";
    private const string RouteStatusPrefix = "zaide-route|v1|";
    private const string CancellationIntentPrefix = "zaide-cancellation-intent|v1|";
    public static TownhallMessageKind ToTownhallMessageKind(ConversationEntryKind kind) =>
        kind switch
        {
            ConversationEntryKind.UserChat => TownhallMessageKind.Chat,
            ConversationEntryKind.AssistantResponse => TownhallMessageKind.Chat,
            ConversationEntryKind.RoutingFailure => TownhallMessageKind.AgentError,
            ConversationEntryKind.ExecutionFailure => TownhallMessageKind.AgentError,
            ConversationEntryKind.ChannelEvent => TownhallMessageKind.ChannelEvent,
            ConversationEntryKind.SystemNotification => TownhallMessageKind.System,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static TownhallMessage ToTownhallMessage(
        ConversationEntry entry,
        IActorCatalog catalog,
        string? projectedLegacySenderId = null,
        string? projectedSenderName = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.TryGet(entry.Author, out var actor))
        {
            projectedLegacySenderId ??= actor.ProjectedLegacyId;
            projectedSenderName ??= actor.DisplayName;
        }
        else
        {
            projectedLegacySenderId ??= ResolveFallbackLegacyId(entry.Author);
            projectedSenderName ??= projectedLegacySenderId;
        }

        var avatar = string.Equals(
                projectedLegacySenderId,
                catalog.CanonicalHuman.ProjectedLegacyId,
                StringComparison.Ordinal)
            ? catalog.CanonicalHuman.AvatarResourceKey
            : "avatar-agent";

        var kind = entry.Kind == ConversationEntryKind.SystemNotification
                   && TryParseActionActivityContent(entry.Content, out _)
            ? ResolveActionActivityTownhallKind(entry.Content)
            : entry.Kind == ConversationEntryKind.SystemNotification
              && TryParseBackendActivityContent(entry.Content, out _)
                ? TownhallMessageKind.AgentAction
                : entry.Kind == ConversationEntryKind.SystemNotification
                  && TryParseRouteStatusContent(entry.Content, out _)
                    ? TownhallMessageKind.System
                    : ToTownhallMessageKind(entry.Kind);

        return new TownhallMessage
        {
            Id = entry.Id.Value,
            SenderId = projectedLegacySenderId,
            SenderName = projectedSenderName,
            SenderAvatar = avatar,
            Content = ToTownhallDisplayContent(entry),
            Timestamp = entry.Timestamp,
            Kind = kind
        };
    }

    /// <summary>
    /// Formats authoritative typed entry content into the frozen Townhall
    /// compatibility string protocol.
    /// </summary>
    public static string ToTownhallDisplayContent(ConversationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Kind == ConversationEntryKind.SystemNotification
            && TryParseActionActivityContent(entry.Content, out var actionActivity))
        {
            return FormatActionActivityDisplayContent(actionActivity);
        }

        if (entry.Kind == ConversationEntryKind.SystemNotification
            && TryParseBackendActivityContent(entry.Content, out var backendActivity))
        {
            return FormatBackendActivityDisplayContent(backendActivity);
        }

        if (entry.Kind == ConversationEntryKind.SystemNotification
            && TryParseRouteStatusContent(entry.Content, out var routeStatus))
        {
            return FormatRouteStatusDisplayContent(routeStatus);
        }

        if (entry.Kind == ConversationEntryKind.SystemNotification
            && entry.Content.StartsWith(CancellationIntentPrefix, StringComparison.Ordinal))
        {
            return "Cancellation requested.";
        }

        return entry.Kind switch
        {
            ConversationEntryKind.UserChat or
            ConversationEntryKind.ChannelEvent or
            ConversationEntryKind.SystemNotification =>
                entry.Content,
            ConversationEntryKind.AssistantResponse =>
                $"Assistant: {entry.Content}",
            ConversationEntryKind.RoutingFailure =>
                $"Routing failed: {entry.Content}",
            ConversationEntryKind.ExecutionFailure =>
                $"Error: {entry.Content}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(entry),
                entry.Kind,
                "Unsupported Townhall display projection.")
        };
    }

    public static ConversationEntry CreateTypedEntry(
        ConversationEntryKind kind,
        ActorId author,
        DateTimeOffset timestamp,
        string content)
    {
        var id = ConversationEntryId.New();
        return kind switch
        {
            ConversationEntryKind.UserChat =>
                ConversationEntry.UserChat(id, author, timestamp, content),
            ConversationEntryKind.AssistantResponse =>
                ConversationEntry.AssistantResponse(id, author, timestamp, content),
            ConversationEntryKind.RoutingFailure =>
                ConversationEntry.RoutingFailure(id, author, timestamp, content),
            ConversationEntryKind.ExecutionFailure =>
                ConversationEntry.ExecutionFailure(id, author, timestamp, content),
            ConversationEntryKind.ChannelEvent =>
                ConversationEntry.ChannelEvent(id, author, timestamp, content),
            ConversationEntryKind.SystemNotification =>
                ConversationEntry.SystemNotification(id, author, timestamp, content),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string ResolveFallbackLegacyId(ActorId author)
    {
        const string panelCustomPrefix = "panel-custom:";
        var value = author.Value;
        if (value.StartsWith(panelCustomPrefix, StringComparison.Ordinal))
        {
            return value[panelCustomPrefix.Length..];
        }

        return value;
    }

    internal static bool TryParseRouteStatusContent(
        string content,
        out RouteStatusProjection projection)
    {
        projection = default!;
        if (string.IsNullOrEmpty(content)
            || !content.StartsWith(RouteStatusPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = content.Split('|');
        if (parts.Length < 6
            || !string.Equals(parts[0], "zaide-route", StringComparison.Ordinal)
            || !string.Equals(parts[1], "v1", StringComparison.Ordinal))
        {
            return false;
        }

        projection = new RouteStatusProjection(
            parts[2],
            parts[3],
            parts[4],
            string.Join('|', parts, 5, parts.Length - 5));

        return true;
    }

    private static string FormatRouteStatusDisplayContent(RouteStatusProjection routeStatus)
    {
        return routeStatus.Outcome switch
        {
            "Completed" => $"Routed to {routeStatus.TargetDisplayName} — completed.",
            "Rejected" => $"Routed to {routeStatus.TargetDisplayName} — rejected.",
            "Cancelled" => $"Routed to {routeStatus.TargetDisplayName} — cancelled.",
            "Failed" => $"Routed to {routeStatus.TargetDisplayName} — failed.",
            _ => $"Routed to {routeStatus.TargetDisplayName}.",
        };
    }

    internal readonly struct RouteStatusProjection
    {
        public RouteStatusProjection(
            string outcome,
            string targetActorId,
            string targetConversationId,
            string targetDisplayName)
        {
            Outcome = outcome;
            TargetActorId = targetActorId;
            TargetConversationId = targetConversationId;
            TargetDisplayName = targetDisplayName;
        }

        public string Outcome { get; }

        public string TargetActorId { get; }

        public string TargetConversationId { get; }

        public string TargetDisplayName { get; }
    }

    internal static bool TryParseActionActivityContent(
        string content,
        out ActionActivityProjection projection)
    {
        projection = default!;
        if (string.IsNullOrEmpty(content)
            || !content.StartsWith(ActionActivityPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = content.Split('|');
        if (parts.Length < 9
            || !string.Equals(parts[0], "zaide-action", StringComparison.Ordinal)
            || !string.Equals(parts[1], "v1", StringComparison.Ordinal))
        {
            return false;
        }

        projection = new ActionActivityProjection(
            parts[2],
            parts[3],
            parts[4],
            parts[5],
            parts[6] == "1",
            parts[7] == "1",
            string.Join('|', parts, 8, parts.Length - 8));

        return true;
    }

    internal static bool TryParseBackendActivityContent(
        string content,
        out BackendActivityProjection projection)
    {
        projection = default!;
        if (string.IsNullOrEmpty(content)
            || !content.StartsWith(BackendActivityPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = content.Split('|');
        if (parts.Length < 6
            || !string.Equals(parts[0], "zaide-backend-activity", StringComparison.Ordinal)
            || !string.Equals(parts[1], "v1", StringComparison.Ordinal))
        {
            return false;
        }

        projection = new BackendActivityProjection(
            parts[2],
            parts[3],
            parts[4],
            string.Join('|', parts, 5, parts.Length - 5));

        return true;
    }

    private static TownhallMessageKind ResolveActionActivityTownhallKind(string content)
    {
        if (!TryParseActionActivityContent(content, out var activity))
        {
            return TownhallMessageKind.System;
        }

        return activity.ResultKind switch
        {
            "Succeeded" => TownhallMessageKind.ToolResult,
            "Denied" or "Failed" or "Revoked" or "Cancelled" or "Conflict" => TownhallMessageKind.AgentError,
            _ => TownhallMessageKind.AgentAction,
        };
    }

    private static string FormatBackendActivityDisplayContent(BackendActivityProjection activity)
    {
        var evidenceLabel = FormatEvidenceLevelLabel(activity.EvidenceLevel);
        var headline = ResolveBackendActivityHeadline(activity.ActivityKind);
        var correlationSuffix = string.IsNullOrWhiteSpace(activity.AcpCorrelationId)
            ? string.Empty
            : $" ({activity.AcpCorrelationId})";
        return $"Backend activity: {headline} — {activity.Summary}{correlationSuffix} [{evidenceLabel}]";
    }

    private static string ResolveBackendActivityHeadline(string activityKind) =>
        activityKind switch
        {
            "ToolCall" => "Tool call",
            "ToolCallUpdate" => "Tool call update",
            "Plan" => "Plan update",
            "UsageUpdate" => "Usage update",
            "SessionControlUpdate" => "Session control update",
            _ => "Backend activity",
        };

    private static string FormatActionActivityDisplayContent(ActionActivityProjection activity)
    {
        var evidenceLabel = FormatEvidenceLevelLabel(activity.EvidenceLevel);
        var summary = activity.Summary;
        var boundedMarkers = BuildBoundedEvidenceMarkers(activity.WasTruncated, activity.WasRedacted);
        var boundedSuffix = boundedMarkers.Length == 0 ? string.Empty : $" {boundedMarkers}";

        return activity.ResultKind switch
        {
            "Succeeded" =>
                $"Tool result: {activity.Headline} — {summary} [{evidenceLabel}]{boundedSuffix}",
            "Denied" =>
                $"Action denied: {activity.Headline} — {summary} [{evidenceLabel}]{boundedSuffix}",
            "Failed" =>
                $"Action failed: {activity.Headline} — {summary} [{evidenceLabel}]{boundedSuffix}",
            _ =>
                $"Agent action: {activity.Headline} — {summary} ({activity.ResultKind}) [{evidenceLabel}]{boundedSuffix}",
        };
    }

    private static string FormatEvidenceLevelLabel(string evidenceLevel) =>
        evidenceLevel switch
        {
            "ZaideExecuted" => "Zaide-executed",
            "ZaideMediated" => "Zaide-mediated",
            "BackendExecutedAndReported" => "Backend-reported",
            "ExternallyObserved" => "Externally observed",
            "Unobservable" => "Unobservable",
            _ => evidenceLevel,
        };

    private static string BuildBoundedEvidenceMarkers(bool wasTruncated, bool wasRedacted)
    {
        if (wasTruncated && wasRedacted)
        {
            return "[truncated] [redacted]";
        }

        if (wasTruncated)
        {
            return "[truncated]";
        }

        if (wasRedacted)
        {
            return "[redacted]";
        }

        return string.Empty;
    }

    internal readonly struct BackendActivityProjection
    {
        public BackendActivityProjection(
            string activityKind,
            string evidenceLevel,
            string acpCorrelationId,
            string summary)
        {
            ActivityKind = activityKind;
            EvidenceLevel = evidenceLevel;
            AcpCorrelationId = acpCorrelationId;
            Summary = summary;
        }

        public string ActivityKind { get; }

        public string EvidenceLevel { get; }

        public string AcpCorrelationId { get; }

        public string Summary { get; }
    }

    internal readonly struct ActionActivityProjection
    {
        public ActionActivityProjection(
            string actionKind,
            string headline,
            string resultKind,
            string evidenceLevel,
            bool wasTruncated,
            bool wasRedacted,
            string summary)
        {
            ActionKind = actionKind;
            Headline = headline;
            ResultKind = resultKind;
            EvidenceLevel = evidenceLevel;
            WasTruncated = wasTruncated;
            WasRedacted = wasRedacted;
            Summary = summary;
        }

        public string ActionKind { get; }

        public string Headline { get; }

        public string ResultKind { get; }

        public string EvidenceLevel { get; }

        public bool WasTruncated { get; }

        public bool WasRedacted { get; }

        public string Summary { get; }
    }
}
