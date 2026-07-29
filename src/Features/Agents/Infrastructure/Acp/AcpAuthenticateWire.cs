using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal sealed class AcpAuthenticateParams
{
    [JsonPropertyName("methodId")]
    public string MethodId { get; init; } = string.Empty;
}
