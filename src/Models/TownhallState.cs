using System.Collections.Generic;

namespace Zaide.Models;

public class TownhallState
{
    public List<Channel> Channels { get; init; } = new();
    public string ActiveChannelId { get; set; } = string.Empty;
    public List<TownhallMessage> ActiveChannelMessages { get; set; } = new();
    public List<WorkspaceAgent> Agents { get; init; } = new();
    public string DraftText { get; set; } = string.Empty;
}
