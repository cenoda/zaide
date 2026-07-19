using System;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Typed, ordinal conversation-entry identity. Values are constructed only
/// through the factory methods on this type.
/// </summary>
public readonly struct ConversationEntryId : IEquatable<ConversationEntryId>
{
    private readonly string? _value;

    private ConversationEntryId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static ConversationEntryId New() =>
        new($"entry:{Guid.NewGuid():N}");

    public bool Equals(ConversationEntryId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ConversationEntryId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(ConversationEntryId left, ConversationEntryId right) =>
        left.Equals(right);

    public static bool operator !=(ConversationEntryId left, ConversationEntryId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
