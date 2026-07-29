using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Bounded auth-method advertisement for explicit user selection.
/// </summary>
internal sealed class AgentAdvertisedAuthMethod
{
    public AgentAdvertisedAuthMethod(string id, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Auth method id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Auth method name is required.", nameof(name));
        }

        Id = id;
        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
    }

    public string Id { get; }

    public string Name { get; }

    public string? Description { get; }
}
