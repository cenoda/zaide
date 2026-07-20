using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// JSON serialization for the conversation workspace snapshot (schema v1).
/// </summary>
internal static class ConversationSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(ConversationWorkspaceSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    public static ConversationWorkspaceSnapshot? Deserialize(
        string json,
        out bool unsupportedSchemaVersion)
    {
        unsupportedSchemaVersion = false;

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.Number
                || !versionElement.TryGetInt32(out var schemaVersion))
            {
                return null;
            }

            if (schemaVersion > ConversationWorkspaceSnapshot.CurrentSchemaVersion)
            {
                unsupportedSchemaVersion = true;
                return null;
            }

            if (schemaVersion != ConversationWorkspaceSnapshot.CurrentSchemaVersion)
            {
                return null;
            }

            return JsonSerializer.Deserialize<ConversationWorkspaceSnapshot>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<Conversation> ToDomainConversations(
        ConversationWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var conversations = new List<Conversation>();
        foreach (var row in snapshot.Conversations)
        {
            if (!TryParseConversation(row, out var conversation))
            {
                continue;
            }

            conversations.Add(conversation);
        }

        return conversations;
    }

    public static ConversationWorkspaceSnapshot FromDomain(
        IReadOnlyList<Conversation> conversations,
        IReadOnlyList<PersistedChannelSnapshot> channels,
        string? activeConversationId,
        IReadOnlyDictionary<string, string> drafts,
        IReadOnlyDictionary<string, string> lastReadEntryIds)
    {
        return new ConversationWorkspaceSnapshot
        {
            SchemaVersion = ConversationWorkspaceSnapshot.CurrentSchemaVersion,
            Channels = channels.ToList(),
            Conversations = conversations.Select(ToConversationRow).ToList(),
            ActiveConversationId = activeConversationId,
            Drafts = drafts
                .Where(pair => !string.IsNullOrEmpty(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            LastReadEntryIds = lastReadEntryIds.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal)
        };
    }

    private static bool TryParseConversation(
        PersistedConversationSnapshot row,
        out Conversation conversation)
    {
        conversation = null!;

        if (string.IsNullOrWhiteSpace(row.Id)
            || !Enum.TryParse<ConversationKind>(row.Kind, ignoreCase: false, out var kind))
        {
            return false;
        }

        ConversationId conversationId;
        ConversationParticipants participants;
        try
        {
            conversationId = ConversationId.FromValue(row.Id);
            participants = kind switch
            {
                ConversationKind.Channel => ConversationParticipants.ForChannel(),
                ConversationKind.Direct => ParseDirectParticipants(row.Participants),
                _ => throw new InvalidOperationException($"Unsupported kind '{kind}'.")
            };
        }
        catch
        {
            return false;
        }

        var entries = new List<ConversationEntry>();
        foreach (var entryRow in row.Entries)
        {
            if (!TryParseEntry(entryRow, out var entry))
            {
                continue;
            }

            entries.Add(entry);
        }

        conversation = Conversation.Restore(conversationId, kind, participants, entries);
        return true;
    }

    private static ConversationParticipants ParseDirectParticipants(IReadOnlyList<string> participants)
    {
        if (participants.Count != 2)
        {
            throw new ArgumentException("Direct conversations require exactly two participants.");
        }

        return ConversationParticipants.ForDirect(
            ActorId.FromValue(participants[0]),
            ActorId.FromValue(participants[1]));
    }

    private static bool TryParseEntry(
        PersistedConversationEntrySnapshot row,
        out ConversationEntry entry)
    {
        entry = null!;

        if (string.IsNullOrWhiteSpace(row.Id)
            || string.IsNullOrWhiteSpace(row.Kind)
            || string.IsNullOrWhiteSpace(row.Author)
            || string.IsNullOrWhiteSpace(row.Content)
            || row.Timestamp == default)
        {
            return false;
        }

        if (!Enum.TryParse<ConversationEntryKind>(row.Kind, ignoreCase: false, out var kind))
        {
            return false;
        }

        ConversationEntryCorrelationId? correlationId = null;
        if (!string.IsNullOrWhiteSpace(row.CorrelationId))
        {
            correlationId = ConversationEntryCorrelationId.FromValue(row.CorrelationId);
        }

        try
        {
            var id = ConversationEntryId.FromValue(row.Id);
            var author = ActorId.FromValue(row.Author);
            entry = kind switch
            {
                ConversationEntryKind.UserChat =>
                    ConversationEntry.UserChat(id, author, row.Timestamp, row.Content, correlationId),
                ConversationEntryKind.AssistantResponse =>
                    ConversationEntry.AssistantResponse(id, author, row.Timestamp, row.Content, correlationId),
                ConversationEntryKind.RoutingFailure =>
                    ConversationEntry.RoutingFailure(id, author, row.Timestamp, row.Content, correlationId),
                ConversationEntryKind.ExecutionFailure =>
                    ConversationEntry.ExecutionFailure(id, author, row.Timestamp, row.Content, correlationId),
                ConversationEntryKind.ChannelEvent =>
                    ConversationEntry.ChannelEvent(id, author, row.Timestamp, row.Content, correlationId),
                ConversationEntryKind.SystemNotification =>
                    ConversationEntry.SystemNotification(id, author, row.Timestamp, row.Content, correlationId),
                _ => throw new InvalidOperationException($"Unsupported entry kind '{kind}'.")
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PersistedConversationSnapshot ToConversationRow(Conversation conversation)
    {
        return new PersistedConversationSnapshot
        {
            Id = conversation.Id.Value,
            Kind = conversation.Kind.ToString(),
            Participants = conversation.Participants.All
                .Select(participant => participant.Value)
                .ToList(),
            Entries = conversation.Entries.Select(ToEntryRow).ToList()
        };
    }

    private static PersistedConversationEntrySnapshot ToEntryRow(ConversationEntry entry) =>
        new()
        {
            Id = entry.Id.Value,
            Kind = entry.Kind.ToString(),
            Author = entry.Author.Value,
            Timestamp = entry.Timestamp,
            Content = entry.Content,
            CorrelationId = entry.CorrelationId?.Value
        };
}
