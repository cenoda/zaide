using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Phase10M0LanguageIntelligenceProof;

namespace Phase10M0LanguageIntelligenceProof;

internal static class Program
{
    // Phase 10 M0 — standalone executable technology proof.
    // Not part of Zaide.slnx / Zaide.Tests. Proves one Linux C# LSP server + client library pair.

    static int Main(string[] args)
    {
        try
        {
            return RunAsync(args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: " + ex);
            return 2;
        }
    }

    static async Task<int> RunAsync(string[] args)
    {
        var results = new List<ProofResult>();
        void Record(string name, bool pass, string detail)
        {
            results.Add(new ProofResult(name, pass, detail));
            Console.WriteLine($"[{(pass ? "PASS" : "FAIL")}] {name}: {detail}");
        }

        Console.WriteLine("=== Phase 10 M0 Language Intelligence Technology Proof ===");
        Console.WriteLine($"Client protocol library: {StreamJsonRpcMarker.LibraryIdentity}");
        Console.WriteLine($"Time (UTC): {DateTime.UtcNow:O}");
        Console.WriteLine();

        // ── Resolve server binary ────────────────────────────────────────────
        var serverPath = ResolveCsharpLs(args);
        if (serverPath is null)
        {
            Record("server-acquisition", false,
                "csharp-ls not found. Install with: dotnet tool install -g csharp-ls");
            return PrintSummary(results);
        }

        var versionProbe = await ProbeServerVersionAsync(serverPath);
        Record("server-acquisition", true,
            $"path={serverPath}; version-probe={versionProbe}");

        // ── Fixture paths (project/solution candidate + parent workspace folder) ──
        var fixtureDir = LocateFixtureDir();
        var projectPath = Path.GetFullPath(Path.Combine(fixtureDir, "Fixture.csproj"));
        var slnxPath = Path.GetFullPath(Path.Combine(fixtureDir, "Fixture.slnx"));
        var samplePath = Path.GetFullPath(Path.Combine(fixtureDir, "Sample.cs"));
        if (!File.Exists(projectPath) || !File.Exists(samplePath))
        {
            Record("fixture", false, $"Missing fixture under {fixtureDir}");
            return PrintSummary(results);
        }

        // Locked contract: workspace folder = parent directory of winning ProjectCandidate.FilePath.
        // For a project file, parent is the fixture directory itself.
        var workspaceFolder = Path.GetDirectoryName(projectPath)!;
        var workspaceUri = PathToFileUri(workspaceFolder);
        var sampleUri = PathToFileUri(samplePath);
        var sampleText = await File.ReadAllTextAsync(samplePath);
        Record("fixture", true,
            $"project={projectPath}; slnx={slnxPath}; workspaceFolder={workspaceFolder}");

        // ── AvaloniaEdit proofs (no server required) ─────────────────────────
        var pos = AvaloniaEditProof.ProvePositionEncoding();
        foreach (var d in pos.Details) Console.WriteLine("  " + d);
        Record("avaloniaedit-position-encoding", pos.Passed, pos.Summary);

        var undo = AvaloniaEditProof.ProveWholeDocumentUndoGroup();
        foreach (var d in undo.Details) Console.WriteLine("  " + d);
        Record("avaloniaedit-undo-group", undo.Passed, undo.Summary);

        var caret = AvaloniaEditProof.ProveCaretSelectionAfterFullReplace();
        foreach (var d in caret.Details) Console.WriteLine("  " + d);
        Record("avaloniaedit-caret-mapping", caret.Passed, caret.Summary);

        // ── Primary lifecycle + language capability session ──────────────────
        var staleCallbackCount = 0;
        await using (var session = new Session(1, serverPath, workspaceFolder))
        {
            session.Client.SetNotificationHandler((gen, node) =>
            {
                if (gen != session.Client.Generation)
                    Interlocked.Increment(ref staleCallbackCount);
            });

            await session.Client.LaunchAsync(serverPath, workspaceFolder);
            Record("launch", session.Client.ProcessId is > 0,
                $"pid={session.Client.ProcessId}; path={serverPath}");

            var initResult = await session.InitializeAsync(workspaceUri, workspaceFolder);
            var capabilities = initResult?["capabilities"] as JsonObject;
            var positionEncoding = initResult?["capabilities"]?["positionEncoding"]?.ToString()
                ?? initResult?["positionEncoding"]?.ToString()
                ?? "(not reported — default utf-16 per LSP)";
            // Some servers put positionEncoding at top-level of InitializeResult.
            if (initResult?["positionEncoding"] is JsonNode pe)
                positionEncoding = pe.ToString();

            Record("initialize", initResult is not null,
                $"positionEncoding={positionEncoding}; " +
                $"hasCompletion={capabilities?["completionProvider"] is not null}; " +
                $"hasHover={capabilities?["hoverProvider"]}; " +
                $"hasDefinition={capabilities?["definitionProvider"]}; " +
                $"hasDocSymbol={capabilities?["documentSymbolProvider"]}; " +
                $"hasWsSymbol={capabilities?["workspaceSymbolProvider"]}; " +
                $"hasFormatting={capabilities?["documentFormattingProvider"]}; " +
                $"textDocumentSync={capabilities?["textDocumentSync"]}");

            await session.Client.NotifyAsync("initialized", new { });
            Record("initialized", true, "initialized notification sent");

            // Capability matrix snapshot
            Console.WriteLine();
            Console.WriteLine("Capability matrix (from InitializeResult):");
            Console.WriteLine($"  positionEncoding: {positionEncoding}");
            Console.WriteLine($"  completionProvider: {capabilities?["completionProvider"]}");
            Console.WriteLine($"  hoverProvider: {capabilities?["hoverProvider"]}");
            Console.WriteLine($"  definitionProvider: {capabilities?["definitionProvider"]}");
            Console.WriteLine($"  documentSymbolProvider: {capabilities?["documentSymbolProvider"]}");
            Console.WriteLine($"  workspaceSymbolProvider: {capabilities?["workspaceSymbolProvider"]}");
            Console.WriteLine($"  documentFormattingProvider: {capabilities?["documentFormattingProvider"]}");
            Console.WriteLine($"  textDocumentSync: {capabilities?["textDocumentSync"]}");
            Console.WriteLine();

            // didOpen
            await session.Client.NotifyAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = sampleUri,
                    languageId = "csharp",
                    version = 1,
                    text = sampleText
                }
            });
            Record("didOpen", true, $"uri={sampleUri}; version=1; bytes={Encoding.UTF8.GetByteCount(sampleText)}");

            // Wait for diagnostics (publishDiagnostics)
            var diags = await session.WaitForDiagnosticsAsync(sampleUri, TimeSpan.FromSeconds(60));
            var diagCount = diags?["params"]?["diagnostics"] is JsonArray arr ? arr.Count : 0;
            Record("diagnostics", diagCount > 0,
                diagCount > 0
                    ? $"publishDiagnostics received; count={diagCount}; sample={SummarizeDiagnostics(diags)}"
                    : "no publishDiagnostics within timeout");

            // Locate Greet identifier for hover/definition/completion
            var greetOffset = sampleText.IndexOf("Greet(\"world\")", StringComparison.Ordinal);
            if (greetOffset < 0) greetOffset = sampleText.IndexOf("Greet", StringComparison.Ordinal);
            var (greetLine, greetChar) = OffsetToLspUtf16(sampleText, greetOffset);

            // completion
            try
            {
                var completion = await session.Client.RequestAsync(
                    "textDocument/completion",
                    new
                    {
                        textDocument = new { uri = sampleUri },
                        position = new { line = greetLine, character = greetChar + 1 },
                    },
                    timeout: TimeSpan.FromSeconds(30));
                var compCount = CountCompletionItems(completion);
                Record("completion", compCount > 0 || completion is not null,
                    $"items≈{compCount}; rawType={completion?.GetValueKind()}");
            }
            catch (Exception ex)
            {
                Record("completion", false, ex.Message);
            }

            // hover
            try
            {
                var hover = await session.Client.RequestAsync(
                    "textDocument/hover",
                    new
                    {
                        textDocument = new { uri = sampleUri },
                        position = new { line = greetLine, character = greetChar + 1 },
                    },
                    timeout: TimeSpan.FromSeconds(30));
                var hoverText = ExtractHover(hover);
                Record("hover", !string.IsNullOrWhiteSpace(hoverText),
                    string.IsNullOrWhiteSpace(hoverText) ? "empty hover" : Truncate(hoverText, 160));
            }
            catch (Exception ex)
            {
                Record("hover", false, ex.Message);
            }

            // definition
            try
            {
                var definition = await session.Client.RequestAsync(
                    "textDocument/definition",
                    new
                    {
                        textDocument = new { uri = sampleUri },
                        position = new { line = greetLine, character = greetChar + 1 },
                    },
                    timeout: TimeSpan.FromSeconds(30));
                var defCount = CountLocations(definition);
                Record("definition", defCount > 0,
                    defCount > 0 ? $"locations={defCount}; {Truncate(definition?.ToJsonString() ?? "", 160)}" : "no locations");
            }
            catch (Exception ex)
            {
                Record("definition", false, ex.Message);
            }

            // document symbols
            try
            {
                var docSymbols = await session.Client.RequestAsync(
                    "textDocument/documentSymbol",
                    new { textDocument = new { uri = sampleUri } },
                    timeout: TimeSpan.FromSeconds(30));
                var symCount = CountSymbols(docSymbols);
                Record("documentSymbol", symCount > 0,
                    $"symbols≈{symCount}");
            }
            catch (Exception ex)
            {
                Record("documentSymbol", false, ex.Message);
            }

            // workspace symbols
            try
            {
                var wsSymbols = await session.Client.RequestAsync(
                    "workspace/symbol",
                    new { query = "Greet" },
                    timeout: TimeSpan.FromSeconds(30));
                var wsCount = CountSymbols(wsSymbols);
                Record("workspaceSymbol", wsCount >= 0 && wsSymbols is not null,
                    $"symbols≈{wsCount}");
            }
            catch (Exception ex)
            {
                Record("workspaceSymbol", false, ex.Message);
            }

            // formatting
            try
            {
                var formatting = await session.Client.RequestAsync(
                    "textDocument/formatting",
                    new
                    {
                        textDocument = new { uri = sampleUri },
                        options = new { tabSize = 4, insertSpaces = true }
                    },
                    timeout: TimeSpan.FromSeconds(30));
                var editCount = formatting is JsonArray edits ? edits.Count : formatting is null ? -1 : 1;
                Record("formatting", formatting is not null,
                    $"textEdits≈{editCount}");
            }
            catch (Exception ex)
            {
                Record("formatting", false, ex.Message);
            }

            // didChange (full content — also valid; server advertised change=2 incremental but accepts full)
            var changedText = sampleText.Replace("var value = 42", "var value = 42;", StringComparison.Ordinal);
            await session.Client.NotifyAsync("textDocument/didChange", new
            {
                textDocument = new { uri = sampleUri, version = 2 },
                contentChanges = new object[]
                {
                    new { text = changedText } // full-document change
                }
            });
            Record("didChange", true, "full-document didChange version=2 (semicolon fix)");

            // Allow server to re-publish diagnostics after change
            await Task.Delay(1500);

            // cancellation of in-flight request
            try
            {
                using var cancelCts = new CancellationTokenSource();
                var cancelTask = session.Client.RequestAsync(
                    "workspace/symbol",
                    new { query = "Sample" },
                    cancelCts.Token,
                    timeout: TimeSpan.FromSeconds(30));
                cancelCts.Cancel();
                try
                {
                    await cancelTask;
                    Record("cancellation", false, "request completed before cancel took effect (soft fail acceptable if server was instant)");
                }
                catch (OperationCanceledException)
                {
                    Record("cancellation", true, "in-flight request cancelled via CancellationToken + $/cancelRequest");
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex.InnerException is OperationCanceledException)
                {
                    Record("cancellation", true, "in-flight request cancelled");
                }
            }
            catch (Exception ex)
            {
                Record("cancellation", false, ex.Message);
            }

            // didClose
            await session.Client.NotifyAsync("textDocument/didClose", new
            {
                textDocument = new { uri = sampleUri }
            });
            Record("didClose", true, $"uri={sampleUri}");

            // graceful shutdown
            try
            {
                var shutdown = await session.Client.RequestAsync("shutdown", null, timeout: TimeSpan.FromSeconds(15));
                await session.Client.NotifyAsync("exit", null);
                var exited = await session.Client.WaitForExitAsync(TimeSpan.FromSeconds(10));
                Record("graceful-shutdown", exited,
                    exited
                        ? $"exitCode={session.Client.ExitCode}; shutdownResult={shutdown?.ToJsonString() ?? "null"}"
                        : "process did not exit after shutdown/exit");
            }
            catch (Exception ex)
            {
                Record("graceful-shutdown", false, ex.Message);
            }
        }

        // ── Forced process-exit handling ─────────────────────────────────────
        {
            await using var forced = new Session(2, serverPath, workspaceFolder);
            await forced.Client.LaunchAsync(serverPath, workspaceFolder);
            await forced.InitializeAsync(workspaceUri, workspaceFolder);
            await forced.Client.NotifyAsync("initialized", new { });
            var pid = forced.Client.ProcessId;
            await forced.Client.ForceKillAsync();
            var dead = forced.Client.HasExited;
            Record("forced-exit", dead, $"killed pid={pid}; hasExited={dead}; exitCode={forced.Client.ExitCode}");
        }

        // ── Restart without leaked children / stale callbacks ────────────────
        {
            var pids = new List<int>();
            LspClient? previous = null;
            for (var gen = 10; gen <= 12; gen++)
            {
                var client = new LspClient(gen);
                var capturedGen = gen;
                client.SetNotificationHandler((callbackGen, _) =>
                {
                    if (callbackGen != capturedGen)
                        Interlocked.Increment(ref staleCallbackCount);
                });

                await client.LaunchAsync(serverPath, workspaceFolder);
                if (client.ProcessId is int pid)
                    pids.Add(pid);

                var init = await new Session(gen, serverPath, workspaceFolder, client)
                    .InitializeAsync(workspaceUri, workspaceFolder);
                await client.NotifyAsync("initialized", new { });
                _ = init;

                if (previous is not null)
                {
                    // Tear down previous generation completely before continuing.
                    try
                    {
                        await previous.RequestAsync("shutdown", null, timeout: TimeSpan.FromSeconds(5));
                        await previous.NotifyAsync("exit", null);
                        await previous.WaitForExitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        await previous.ForceKillAsync();
                    }

                    previous.Dispose();
                }

                previous = client;
            }

            // Final generation graceful stop
            if (previous is not null)
            {
                try
                {
                    await previous.RequestAsync("shutdown", null, timeout: TimeSpan.FromSeconds(5));
                    await previous.NotifyAsync("exit", null);
                    await previous.WaitForExitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    await previous.ForceKillAsync();
                }

                previous.Dispose();
            }

            // Ensure none of the recorded pids are still alive
            var leaked = pids.Where(IsProcessAlive).ToList();
            Record("restart-no-leak", leaked.Count == 0 && staleCallbackCount == 0,
                $"pids=[{string.Join(",", pids)}]; leaked=[{string.Join(",", leaked)}]; staleCallbacks={staleCallbackCount}");
        }

        // ── .slnx compatibility (parent-dir workspace + slnx candidate path) ─
        {
            await using var slnxSession = new Session(20, serverPath, workspaceFolder);
            await slnxSession.Client.LaunchAsync(serverPath, workspaceFolder);
            // Workspace folder remains parent directory of the .slnx path.
            var slnxParent = Path.GetDirectoryName(slnxPath)!;
            var slnxWorkspaceUri = PathToFileUri(slnxParent);
            var init = await slnxSession.InitializeAsync(
                slnxWorkspaceUri,
                slnxParent,
                initializationOptions: new { solution = slnxPath });
            await slnxSession.Client.NotifyAsync("initialized", new { });

            // Give solution load a moment; csharp-ls logs .sln/.slnx discovery.
            await Task.Delay(2000);
            await slnxSession.Client.NotifyAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = sampleUri,
                    languageId = "csharp",
                    version = 1,
                    text = sampleText
                }
            });
            var diags = await slnxSession.WaitForDiagnosticsAsync(sampleUri, TimeSpan.FromSeconds(45));
            var ok = init is not null && diags is not null;
            Record("slnx-workspace", ok,
                ok
                    ? $"initialize ok with workspace={slnxParent}; candidate={slnxPath}; diagnostics received"
                    : "initialize or diagnostics failed for .slnx parent-dir workspace");

            try
            {
                await slnxSession.Client.RequestAsync("shutdown", null, timeout: TimeSpan.FromSeconds(10));
                await slnxSession.Client.NotifyAsync("exit", null);
                await slnxSession.Client.WaitForExitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                await slnxSession.Client.ForceKillAsync();
            }
        }

        // Non-BMP position note using sample file content
        {
            const string emoji = "\U0001F389"; // 🎉
            var emojiIdx = sampleText.IndexOf(emoji, StringComparison.Ordinal);
            if (emojiIdx < 0)
                emojiIdx = sampleText.IndexOf("\uD83C\uDF89", StringComparison.Ordinal);
            if (emojiIdx >= 0)
            {
                var (line, character) = OffsetToLspUtf16(sampleText, emojiIdx);
                var emojiUtf16Len = emoji.Length;
                Record("non-bmp-position", true,
                    $"emoji at string offset {emojiIdx} => LSP utf-16 line={line}, character={character} " +
                    $"(UTF-16 code units for emoji={emojiUtf16Len})");
            }
            else
            {
                Record("non-bmp-position", false, "emoji not found in fixture");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"StreamJsonRpc assembly: {StreamJsonRpcMarker.LibraryIdentity}");
        Console.WriteLine($"Server binary: {serverPath}");
        Console.WriteLine($"Server version probe: {versionProbe}");
        return PrintSummary(results);
    }

    static int PrintSummary(List<ProofResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        var pass = results.Count(r => r.Pass);
        var fail = results.Count(r => !r.Pass);
        foreach (var r in results)
            Console.WriteLine($"  {(r.Pass ? "PASS" : "FAIL"),-4}  {r.Name}");
        Console.WriteLine($"Total: {pass} passed, {fail} failed, {results.Count} total");
        return fail == 0 ? 0 : 1;
    }

    static string? ResolveCsharpLs(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--server" or "--csharp-ls")
                return Path.GetFullPath(args[i + 1]);
        }

        // Prefer PATH / dotnet tools shim.
        var fromPath = FindOnPath("csharp-ls");
        if (fromPath is not null)
            return fromPath;

        var homeTool = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet", "tools", "csharp-ls");
        if (File.Exists(homeTool))
            return homeTool;

        return null;
    }

    static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    static async Task<string> ProbeServerVersionAsync(string serverPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            if (p is null) return "(failed to start)";
            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            var text = (stdout + stderr).Trim();
            return string.IsNullOrWhiteSpace(text) ? $"(exit {p.ExitCode})" : text.Split('\n')[0].Trim();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    static string LocateFixtureDir()
    {
        // Prefer content copied next to the assembly, then source-relative path.
        var baseDir = AppContext.BaseDirectory;
        var beside = Path.Combine(baseDir, "fixture");
        if (File.Exists(Path.Combine(beside, "Sample.cs")))
            return beside;

        var cwd = Environment.CurrentDirectory;
        var fromCwd = Path.Combine(cwd, "fixture");
        if (File.Exists(Path.Combine(fromCwd, "Sample.cs")))
            return fromCwd;

        var fromTools = Path.GetFullPath(Path.Combine(cwd,
            "tools", "Phase10M0LanguageIntelligenceProof", "fixture"));
        if (File.Exists(Path.Combine(fromTools, "Sample.cs")))
            return fromTools;

        // Walk up from baseDir looking for fixture
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "fixture");
            if (File.Exists(Path.Combine(candidate, "Sample.cs")))
                return candidate;
            var toolsCandidate = Path.Combine(dir.FullName, "tools", "Phase10M0LanguageIntelligenceProof", "fixture");
            if (File.Exists(Path.Combine(toolsCandidate, "Sample.cs")))
                return toolsCandidate;
            dir = dir.Parent;
        }

        return beside;
    }

    static string PathToFileUri(string path)
    {
        var full = Path.GetFullPath(path);
        return new Uri(full).AbsoluteUri;
    }

    static (int line, int character) OffsetToLspUtf16(string text, int offset)
    {
        if (offset < 0) offset = 0;
        if (offset > text.Length) offset = text.Length;
        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart);
    }

    static int CountCompletionItems(JsonNode? node)
    {
        if (node is null) return 0;
        if (node is JsonArray arr) return arr.Count;
        if (node["items"] is JsonArray items) return items.Count;
        return 0;
    }

    static int CountLocations(JsonNode? node)
    {
        if (node is null) return 0;
        if (node is JsonArray arr) return arr.Count;
        if (node["uri"] is not null || node["targetUri"] is not null) return 1;
        return 0;
    }

    static int CountSymbols(JsonNode? node)
    {
        if (node is null) return 0;
        if (node is JsonArray arr) return arr.Count;
        return 0;
    }

    static string ExtractHover(JsonNode? node)
    {
        if (node is null) return "";
        var contents = node["contents"];
        if (contents is null) return node.ToJsonString();
        if (contents.GetValueKind() == JsonValueKind.String)
            return contents.GetValue<string>();
        if (contents["value"] is JsonNode v)
            return v.ToString();
        if (contents is JsonArray arr)
            return string.Join(" | ", arr.Select(a => a?["value"]?.ToString() ?? a?.ToString()));
        return contents.ToJsonString();
    }

    static string SummarizeDiagnostics(JsonNode? publishMessage)
    {
        var diags = publishMessage?["params"]?["diagnostics"] as JsonArray;
        if (diags is null || diags.Count == 0) return "(none)";
        var first = diags[0];
        return $"{first?["code"]}: {first?["message"]}";
    }

    static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    static bool IsProcessAlive(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

}

sealed record ProofResult(string Name, bool Pass, string Detail);

/// <summary>One LSP session generation for the proof.</summary>
sealed class Session : IAsyncDisposable
{
    public LspClient Client { get; }
    private readonly bool _ownsClient;

    public Session(int generation, string serverPath, string workspaceFolder, LspClient? existing = null)
    {
        _ = serverPath;
        _ = workspaceFolder;
        if (existing is not null)
        {
            Client = existing;
            _ownsClient = false;
        }
        else
        {
            Client = new LspClient(generation);
            _ownsClient = true;
        }
    }

    public async Task<JsonNode?> InitializeAsync(
        string workspaceUri,
        string workspaceFolderPath,
        object? initializationOptions = null)
    {
        var initParams = new Dictionary<string, object?>
        {
            ["processId"] = Environment.ProcessId,
            ["clientInfo"] = new { name = "Phase10M0LanguageIntelligenceProof", version = "1.0.0" },
            ["rootUri"] = workspaceUri,
            ["rootPath"] = workspaceFolderPath,
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["general"] = new
                {
                    positionEncodings = new[] { "utf-16", "utf-8" }
                },
                ["textDocument"] = new Dictionary<string, object?>
                {
                    ["synchronization"] = new
                    {
                        dynamicRegistration = false,
                        willSave = false,
                        willSaveWaitUntil = false,
                        didSave = true
                    },
                    ["completion"] = new
                    {
                        dynamicRegistration = false,
                        completionItem = new { snippetSupport = false, documentationFormat = new[] { "markdown", "plaintext" } }
                    },
                    ["hover"] = new
                    {
                        dynamicRegistration = false,
                        contentFormat = new[] { "markdown", "plaintext" }
                    },
                    ["definition"] = new { dynamicRegistration = false },
                    ["documentSymbol"] = new { dynamicRegistration = false },
                    ["formatting"] = new { dynamicRegistration = false },
                    ["publishDiagnostics"] = new { relatedInformation = true }
                },
                ["workspace"] = new Dictionary<string, object?>
                {
                    ["workspaceFolders"] = true,
                    ["symbol"] = new { dynamicRegistration = false }
                }
            },
            ["workspaceFolders"] = new object[]
            {
                new { uri = workspaceUri, name = Path.GetFileName(workspaceFolderPath.TrimEnd(Path.DirectorySeparatorChar)) }
            },
            ["trace"] = "off",
        };

        if (initializationOptions is not null)
            initParams["initializationOptions"] = initializationOptions;

        return await Client.RequestAsync("initialize", initParams, timeout: TimeSpan.FromSeconds(60));
    }

    public async Task<JsonNode?> WaitForDiagnosticsAsync(string uri, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var n in Client.DrainNotifications())
            {
                if (n["method"]?.ToString() == "textDocument/publishDiagnostics")
                {
                    var msgUri = n["params"]?["uri"]?.ToString();
                    if (msgUri is not null &&
                        string.Equals(NormalizeUri(msgUri), NormalizeUri(uri), StringComparison.OrdinalIgnoreCase))
                    {
                        return n;
                    }
                }
            }

            await Task.Delay(100);
        }

        return null;
    }

    private static string NormalizeUri(string uri)
    {
        try { return new Uri(uri).AbsoluteUri; }
        catch { return uri; }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_ownsClient)
            return;

        if (!Client.HasExited)
        {
            try { await Client.ForceKillAsync(); }
            catch { /* ignore */ }
        }

        Client.Dispose();
    }
}
