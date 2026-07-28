using System.Diagnostics;
using System.Text;
using System.Text.Json;

var mode = args.Length > 0 ? args[0] : "healthy";

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

    if (mode == "slow-init" && method == "initialize")
    {
        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    if (mode == "slow-request")
    {
        await Task.Delay(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
    }

    if (root.TryGetProperty("id", out var idElement))
    {
        var response = BuildResponse(idElement, method);
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

static object BuildResponse(JsonElement id, string method) =>
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
                agentInfo = new { name = "acp-fake-agent", version = "phase-20-m2" },
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
        _ => new
        {
            jsonrpc = "2.0",
            id = ReadId(id),
            error = new { code = -32601, message = "Method not found" },
        },
    };

static object ReadId(JsonElement id) =>
    id.ValueKind switch
    {
        JsonValueKind.Number when id.TryGetInt64(out var number) => number,
        JsonValueKind.String => id.GetString()!,
        _ => id.Clone(),
    };
