using System.Diagnostics;
using System.Text;
using System.Text.Json;

var mode = args.Length > 0 ? args[0] : "healthy";
var initializeCount = 0;
var sessionNewCount = 0;
var sessionPromptCount = 0;
var statsFile = Environment.GetEnvironmentVariable("ZAIDE_ACP_STATS_FILE");

void WriteStats()
{
    if (string.IsNullOrWhiteSpace(statsFile))
    {
        return;
    }

    var payload = JsonSerializer.Serialize(new
    {
        initialize = initializeCount,
        sessionNew = sessionNewCount,
        sessionPrompt = sessionPromptCount,
    });
    Directory.CreateDirectory(Path.GetDirectoryName(statsFile)!);
    File.WriteAllText(statsFile, payload);
}

WriteStats();

if (mode == "spawn-child")
{
    Process.Start(new ProcessStartInfo
    {
        FileName = "sleep",
        Arguments = "600",
        UseShellExecute = false,
        CreateNoWindow = true,
    });
}

if (mode == "stderr-secret")
{
    await Console.Error.WriteLineAsync("diagnostic api_key=super-secret-value").ConfigureAwait(false);
}

if (mode == "exit-immediate")
{
    Environment.Exit(7);
}

if (mode == "malformed-stdout")
{
    await Console.Out.WriteLineAsync("not-json").ConfigureAwait(false);
    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
}

if (mode == "oversized-line")
{
    var oversized = new string('x', 5 * 1024 * 1024);
    await Console.Out.WriteLineAsync(oversized).ConfigureAwait(false);
    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
}

if (mode == "hang")
{
    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
}

using var stdin = Console.OpenStandardInput();
using var reader = new StreamReader(stdin, Encoding.UTF8, leaveOpen: true);

while (true)
{
    var line = await reader.ReadLineAsync().ConfigureAwait(false);
    if (line is null)
    {
        break;
    }

    if (line.Length == 0)
    {
        continue;
    }

    using var document = JsonDocument.Parse(line);
    var root = document.RootElement;
    if (!root.TryGetProperty("method", out var methodElement))
    {
        continue;
    }

    var method = methodElement.GetString();
    if (method is null)
    {
        continue;
    }

    // Count at request receipt so M4 evidence counters align with protocol send boundaries.
    if (method == "initialize")
    {
        initializeCount++;
        WriteStats();
    }

    if (method == "session/new")
    {
        sessionNewCount++;
        WriteStats();
    }

    if (method == "session/prompt")
    {
        sessionPromptCount++;
        WriteStats();
    }

    if (mode == "slow-init" && method == "initialize")
    {
        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    if (mode == "slow-request")
    {
        await Task.Delay(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
    }

    // M4 force-quit evidence: keep prompt in-flight without exceeding InitializeTimeout (30s).
    if (mode == "slow-prompt" && method == "session/prompt")
    {
        await Task.Delay(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
    }

    if (method == "session/prompt")
    {
        if (mode == "tool-activity")
        {
            await WriteSessionUpdateAsync(
                "fake-session-1",
                new
                {
                    sessionUpdate = "tool_call",
                    toolCall = new { toolCallId = "tc-fake-1", title = "read_file" },
                }).ConfigureAwait(false);
            await WriteSessionUpdateAsync(
                "fake-session-1",
                new
                {
                    sessionUpdate = "tool_call_update",
                    toolCallUpdate = new { toolCallId = "tc-fake-1", status = "completed" },
                }).ConfigureAwait(false);
        }

        await WriteSessionUpdateAsync(
            "fake-session-1",
            new
            {
                sessionUpdate = "agent_message_chunk",
                content = new { type = "text", text = mode == "tool-activity" ? "tool activity complete" : "fake agent response" },
            }).ConfigureAwait(false);

        // Phase 22.4 transparency re-smoke: emit a stable public usage_update
        // envelope so ACP reported point-in-time tokens and cumulative cost are
        // observable without inventing product-side pricing.
        if (mode is "healthy" or "fast-prompt" or "tool-activity")
        {
            await WriteSessionUpdateAsync(
                "fake-session-1",
                new
                {
                    sessionUpdate = "usage_update",
                    used = 128,
                    size = 200000,
                    cost = new { amount = 0.12, currency = "USD" },
                }).ConfigureAwait(false);
        }
    }

    if (root.TryGetProperty("id", out var idElement))
    {
        var response = BuildResponse(idElement, method, mode, initializeCount);
        var payload = JsonSerializer.Serialize(response);
        await Console.Out.WriteLineAsync(payload).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);

        if (mode == "duplicate-response")
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
            await Console.Out.WriteLineAsync(payload).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);
        }
    }
}

return 0;

static async Task WriteSessionUpdateAsync(string sessionId, object updateBody)
{
    var notification = new
    {
        jsonrpc = "2.0",
        method = "session/update",
        @params = new
        {
            sessionId,
            update = updateBody,
        },
    };

    var payload = JsonSerializer.Serialize(notification);
    await Console.Out.WriteLineAsync(payload).ConfigureAwait(false);
    await Console.Out.FlushAsync().ConfigureAwait(false);
}

static object BuildResponse(JsonElement id, string method, string mode, int initializeCount) =>
    method switch
    {
        "initialize" => new
        {
            jsonrpc = "2.0",
            id = ReadId(id),
            result = new
            {
                protocolVersion = 1,
                agentCapabilities = new
                {
                    loadSession = false,
                    promptCapabilities = new { image = false, audio = false, embeddedContext = false },
                    mcpCapabilities = new { http = false, sse = false },
                },
                authMethods = Array.Empty<object>(),
                agentInfo = ResolveAgentInfo(mode, initializeCount),
            },
        },
        "session/new" => new
        {
            jsonrpc = "2.0",
            id = ReadId(id),
            result = new { sessionId = "fake-session-1" },
        },
        "session/prompt" => new
        {
            jsonrpc = "2.0",
            id = ReadId(id),
            result = new { stopReason = "end_turn" },
        },
        "authenticate" => new
        {
            jsonrpc = "2.0",
            id = ReadId(id),
            result = new { },
        },
        _ => new
        {
            jsonrpc = "2.0",
            id = ReadId(id),
            error = new { code = -32601, message = "Method not found" },
        },
    };

static object ResolveAgentInfo(string mode, int initializeCount) =>
    mode == "identity-mismatch" && initializeCount > 1
        ? new { name = "acp-fake-agent-wrong", version = "phase-20-m2" }
        : new { name = "acp-fake-agent", version = "phase-20-m2" };

static object ReadId(JsonElement id) =>
    id.ValueKind switch
    {
        JsonValueKind.Number when id.TryGetInt64(out var number) => number,
        JsonValueKind.String => id.GetString()!,
        _ => id.Clone(),
    };
