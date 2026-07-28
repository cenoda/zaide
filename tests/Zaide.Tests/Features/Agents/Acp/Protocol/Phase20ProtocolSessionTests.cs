using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolSessionTests
{
    [Fact]
    public void Phase20ProtocolSession_CreateSession_RejectsRelativeWorkingDirectory()
    {
        var ex = Assert.Throws<AcpProtocolException>(() =>
            AcpSessionValidation.RequireAbsoluteWorkingDirectory("relative/path"));
        Assert.Contains("absolute path", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phase20ProtocolSession_SessionMethods_FailBeforeInitialize()
    {
        await using var clientToAgent = new System.IO.MemoryStream();
        await using var agentToClient = new System.IO.MemoryStream();
        await using var session = new AcpProtocolSession(clientToAgent, agentToClient);

        await Assert.ThrowsAsync<AcpProtocolException>(() =>
            session.CreateSessionAsync("/tmp/workspace", CancellationToken.None));
    }
}
