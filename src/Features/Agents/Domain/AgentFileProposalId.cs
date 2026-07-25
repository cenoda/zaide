using System;
using System.Security.Cryptography;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Unique identifier for a file action proposal.
/// </summary>
internal readonly struct AgentFileProposalId : IEquatable<AgentFileProposalId>, IComparable<AgentFileProposalId>
{
    private readonly string _value;

    private AgentFileProposalId(string value)
    {
        _value = value;
    }

    public string Value => _value;

    /// <summary>
    /// Creates a new unique proposal identifier.
    /// </summary>
    public static AgentFileProposalId New()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);
        return new AgentFileProposalId(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    /// <summary>
    /// Creates a proposal identifier from a string value.
    /// </summary>
    public static AgentFileProposalId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Proposal id value is required.", nameof(value));
        }

        if (value.Length != 32) // 16 bytes = 32 hex chars
        {
            throw new ArgumentException(
                "Proposal id must be 32 lowercase hexadecimal characters.",
                nameof(value));
        }

        foreach (var character in value)
        {
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                throw new ArgumentException(
                    "Proposal id must use lowercase hexadecimal characters.",
                    nameof(value));
            }
        }

        return new AgentFileProposalId(value);
    }

    public bool Equals(AgentFileProposalId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentFileProposalId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public int CompareTo(AgentFileProposalId other) =>
        string.Compare(_value, other._value, StringComparison.Ordinal);

    public static bool operator ==(AgentFileProposalId left, AgentFileProposalId right) =>
        left.Equals(right);

    public static bool operator !=(AgentFileProposalId left, AgentFileProposalId right) =>
        !left.Equals(right);

    public static bool operator <(AgentFileProposalId left, AgentFileProposalId right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(AgentFileProposalId left, AgentFileProposalId right) =>
        left.CompareTo(right) > 0;

    public override string ToString() => Value;
}