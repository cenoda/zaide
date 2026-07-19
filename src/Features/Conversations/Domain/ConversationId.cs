using System;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Typed, ordinal conversation identity. Panel and channel presentation keys
/// are projected into this namespace; they are not interchangeable with
/// <c>PanelId</c> or raw channel selection state.
/// </summary>
public readonly struct ConversationId : IEquatable<ConversationId>
{
    private readonly string? _value;

    private ConversationId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static ConversationId ForChannel(string channelId) =>
        new($"channel:{channelId ?? string.Empty}");

    public static ConversationId NewDirect() =>
        new($"direct:{Guid.NewGuid():N}");

    public bool Equals(ConversationId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ConversationId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(ConversationId left, ConversationId right) => left.Equals(right);

    public static bool operator !=(ConversationId left, ConversationId right) => !left.Equals(right);

    public override string ToString() => Value;

    /// <summary>
    /// Returns the channel presentation key when this identity was created by
    /// <see cref="ForChannel"/>.
    /// </summary>
    public bool TryGetChannelId(out string channelId)
    {
        const string prefix = "channel:";
        if (_value is not null
            && _value.StartsWith(prefix, StringComparison.Ordinal)
            && _value.Length > prefix.Length)
        {
            channelId = _value[prefix.Length..];
            return true;
        }

        channelId = string.Empty;
        return false;
    }
}
