using System;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Agent-neutral opaque correlation token for entries that belong to one
/// bounded execution attempt. Producers map their run identity into this type;
/// Conversations does not depend on Agents execution types.
/// </summary>
public readonly struct ConversationEntryCorrelationId : IEquatable<ConversationEntryCorrelationId>
{
    private readonly string? _value;

    private ConversationEntryCorrelationId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static ConversationEntryCorrelationId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Correlation id value is required.", nameof(value));
        }

        return new ConversationEntryCorrelationId(value);
    }

    public bool Equals(ConversationEntryCorrelationId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ConversationEntryCorrelationId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(
        ConversationEntryCorrelationId left,
        ConversationEntryCorrelationId right) => left.Equals(right);

    public static bool operator !=(
        ConversationEntryCorrelationId left,
        ConversationEntryCorrelationId right) => !left.Equals(right);

    public override string ToString() => Value;
}
