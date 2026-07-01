using System;

namespace Zaide.Models;

public class TownhallMessage
{
    public string Id { get; init; } = string.Empty;
    public string SenderId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}
