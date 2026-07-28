using System.Text.Json;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolSessionUpdateTests
{
    [Fact]
    public void Phase20ProtocolSessionUpdate_ParsesAllStableVariants()
    {
        var variants = new[]
        {
            """{"sessionUpdate":"user_message_chunk","content":{"type":"text","text":"u"}}""",
            """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"a"}}""",
            """{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":"t"}}""",
            """{"sessionUpdate":"tool_call","toolCallId":"tc1","title":"read"}""",
            """{"sessionUpdate":"tool_call_update","toolCallId":"tc1","status":"completed"}""",
            """{"sessionUpdate":"plan","entries":[]}""",
            """{"sessionUpdate":"available_commands_update","availableCommands":[]}""",
            """{"sessionUpdate":"current_mode_update","currentModeId":"ask"}""",
            """{"sessionUpdate":"config_option_update","configOptions":[]}""",
            """{"sessionUpdate":"session_info_update","title":"demo"}""",
            """{"sessionUpdate":"usage_update","usedTokens":1,"maxTokens":2}""",
        };

        foreach (var json in variants)
        {
            var update = JsonSerializer.Deserialize<AcpSessionUpdate>(
                json,
                AcpJsonSerializerOptionsFactory.SharedOptions);
            Assert.NotNull(update);
            Assert.NotEqual(AcpSessionUpdateKind.Unknown, update!.Kind);
        }
    }
}
