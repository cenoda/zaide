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

        return new TownhallMessage
        {
            Id = entry.Id.Value,
            SenderId = projectedLegacySenderId,
            SenderName = projectedSenderName,
            SenderAvatar = avatar,
            Content = entry.Content,
            Timestamp = entry.Timestamp,
            Kind = ToTownhallMessageKind(entry.Kind)
        };
    }

    public static ConversationEntryKind ClassifyTownhallMirror(
        TownhallMessageKind kind,
        ActorId author,
        string content,
        IActorCatalog catalog)
    {
        return kind switch
        {
            TownhallMessageKind.Chat when author == catalog.CanonicalHuman.Id =>
                ConversationEntryKind.UserChat,
            TownhallMessageKind.Chat =>
                ConversationEntryKind.AssistantResponse,
            TownhallMessageKind.AgentError when content.StartsWith("Routing failed:", StringComparison.Ordinal) =>
                ConversationEntryKind.RoutingFailure,
            TownhallMessageKind.AgentError =>
                ConversationEntryKind.ExecutionFailure,
            TownhallMessageKind.ChannelEvent =>
                ConversationEntryKind.ChannelEvent,
            TownhallMessageKind.System =>
                ConversationEntryKind.SystemNotification,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported Townhall mirror classification.")
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
}
