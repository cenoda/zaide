using System.Text.Json;
using Xunit;
using Zaide.Services;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M6 unit tests for DAP breakpoint verification mapping.
/// </summary>
public sealed class DapBreakpointVerificationParserTests
{
    [Fact]
    public void Parse_MapsVerifiedPendingAndRejected()
    {
        var body = JsonDocument.Parse(
            """
            {
              "breakpoints": [
                { "line": 1, "verified": true },
                { "line": 2, "verified": false },
                { "line": 3, "verified": false, "message": "The breakpoint is pending and will be resolved when debugging starts." },
                { "line": 4, "verified": false, "message": "No code" }
              ]
            }
            """).RootElement;

        var results = DapBreakpointVerificationParser.Parse(
            "/tmp/Program.cs",
            new[] { 1, 2, 3, 4 },
            body);

        Assert.Equal(4, results.Count);
        Assert.Equal(DebugBreakpointVerificationState.Verified, results[0].State);
        Assert.Equal(DebugBreakpointVerificationState.Pending, results[1].State);
        Assert.Equal(DebugBreakpointVerificationState.Pending, results[2].State);
        Assert.Equal(DebugBreakpointVerificationState.Rejected, results[3].State);
        Assert.Equal("No code", results[3].Message);
    }

    [Fact]
    public void Parse_MissingBody_YieldsPendingPerRequestedLine()
    {
        var results = DapBreakpointVerificationParser.Parse(
            "/tmp/Program.cs",
            new[] { 10, 20 },
            body: null);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(DebugBreakpointVerificationState.Pending, r.State));
        Assert.Equal(10, results[0].RequestedLine);
        Assert.Equal(20, results[1].RequestedLine);
    }
}
