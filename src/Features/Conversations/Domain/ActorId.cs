using System;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Typed, ordinal actor identity. Display names are not keys.
/// Values are constructed only through the factory methods on this type.
/// </summary>
public readonly struct ActorId : IEquatable<ActorId>
{
    private readonly string? _value;

    private ActorId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static ActorId HumanUser { get; } = new("human:user-1");

    public static ActorId TownhallAgent { get; } = new("townhall-agent:agent-1");

    public static ActorId PanelSeed(string seedKey) =>
        new($"panel-seed:{seedKey}");

    public static ActorId PanelFallback(int fallbackNumber)
    {
        if (fallbackNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fallbackNumber),
                fallbackNumber,
                "Fallback numbers are 1-based.");
        }

        return new ActorId($"panel-fallback:{fallbackNumber}");
    }

    public static ActorId PanelCustom(string legacyAgentId) =>
        new($"panel-custom:{legacyAgentId}");

    public bool Equals(ActorId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ActorId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);

    public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);

    public override string ToString() => Value;
}
