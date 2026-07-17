using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Language.Application;

namespace Zaide.Services;

/// <summary>
/// Owns one csharp-ls child process and a Content-Length StreamJsonRpc transport.
/// </summary>
internal sealed class CsharpLsSession : ILanguageServerSession
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    private readonly LanguageServerStartOptions _options;
    private Process? _process;
    private JsonRpc? _rpc;
    private LanguageServerCapabilities _capabilities = LanguageServerCapabilities.None;
    private bool _disposed;
    private int _exitSignaled;

    public CsharpLsSession(LanguageServerStartOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public long Generation => _options.Generation;

    /// <inheritdoc />
    public int? ProcessId => _process?.HasExited == false ? _process.Id : _process?.Id;

    /// <inheritdoc />
    public bool HasExited => _process is null || _process.HasExited;

    /// <inheritdoc />
    public event Action<long>? ProcessExited;

    /// <inheritdoc />
    public event Action<LanguageServerPublishDiagnostics>? DiagnosticsPublished;

    /// <summary>
    /// Launches the process and completes LSP initialize/initialized.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var psi = new ProcessStartInfo
        {
            FileName = _options.ServerPath,
            WorkingDirectory = _options.WorkspaceFolderPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true,
        };

        _process.Exited += OnProcessExited;

        if (!_process.Start())
            throw new InvalidOperationException($"Failed to start language server: {_options.ServerPath}");

        // Drain stderr on a background task so protocol reads never block.
        _ = Task.Run(async () =>
        {
            try
            {
                while (_process is { HasExited: false })
                {
                    var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                        break;
                }
            }
            catch
            {
                // Process teardown races are expected.
            }
        });

        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions =
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        var handler = new HeaderDelimitedMessageHandler(
            _process.StandardInput.BaseStream,
            _process.StandardOutput.BaseStream,
            formatter);

        _rpc = new JsonRpc(handler);
        // Register server→client notifications before listening so publishDiagnostics
        // is never dropped as an unknown method during startup races.
        // UseSingleObjectParameterDeserialization is required because LSP sends
        // params as one JSON object (not a positional array).
        _rpc.AddLocalRpcMethod(
            typeof(CsharpLsSession).GetMethod(
                nameof(OnPublishDiagnostics),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!,
            this,
            new JsonRpcMethodAttribute("textDocument/publishDiagnostics")
            {
                UseSingleObjectParameterDeserialization = true,
            });
        _rpc.StartListening();
        _rpc.Disconnected += (_, e) => SignalProcessExited();

        var workspaceUri = LanguageDocumentUri.FromPath(_options.WorkspaceFolderPath);
        var initParams = BuildInitializeParams(workspaceUri, _options);

        // LSP initialize params are a single object (not a positional array).
        var initResult = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "initialize",
            initParams,
            cancellationToken).ConfigureAwait(false);

        _capabilities = LanguageServerCapabilitiesParser.Parse(initResult);

        await _rpc.NotifyWithParameterObjectAsync("initialized", new { }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public LanguageServerCapabilities Capabilities => _capabilities;

    /// <inheritdoc />
    public async Task<LanguageServerCompletionResult?> RequestCompletionAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "textDocument/completion",
            new
            {
                textDocument = new { uri = documentUri },
                position = new { line, character },
            },
            cancellationToken).ConfigureAwait(false);

        return LanguageServerCompletionParser.Parse(result);
    }

    /// <inheritdoc />
    public async Task<LanguageServerHoverResult?> RequestHoverAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "textDocument/hover",
            new
            {
                textDocument = new { uri = documentUri },
                position = new { line, character },
            },
            cancellationToken).ConfigureAwait(false);

        return LanguageServerHoverParser.Parse(result);
    }

    /// <inheritdoc />
    public async Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "textDocument/definition",
            new
            {
                textDocument = new { uri = documentUri },
                position = new { line, character },
            },
            cancellationToken).ConfigureAwait(false);

        return LanguageServerDefinitionParser.Parse(result);
    }

    /// <inheritdoc />
    public async Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "textDocument/documentSymbol",
            new
            {
                textDocument = new { uri = documentUri },
            },
            cancellationToken).ConfigureAwait(false);

        return LanguageServerSymbolParser.Parse(result);
    }

    /// <inheritdoc />
    public async Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "workspace/symbol",
            new
            {
                query = query ?? string.Empty,
            },
            cancellationToken).ConfigureAwait(false);

        return LanguageServerSymbolParser.Parse(result);
    }

    /// <inheritdoc />
    public async Task<LanguageServerFormattingResult?> RequestFormattingAsync(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        // LSP DocumentFormattingParams: textDocument + options (tabSize, insertSpaces).
        var result = await _rpc.InvokeWithParameterObjectAsync<JsonElement?>(
            "textDocument/formatting",
            new
            {
                textDocument = new { uri = documentUri },
                options = new
                {
                    tabSize = 4,
                    insertSpaces = true,
                },
            },
            cancellationToken).ConfigureAwait(false);

        return LanguageServerFormattingParser.Parse(result);
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _rpc is null)
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ShutdownTimeout);

        try
        {
            await _rpc.InvokeWithCancellationAsync<object?>(
                "shutdown",
                Array.Empty<object?>(),
                timeoutCts.Token).ConfigureAwait(false);
            await _rpc.NotifyAsync("exit").ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await ForceKillAsync().ConfigureAwait(false);
            throw;
        }
        catch
        {
            await ForceKillAsync().ConfigureAwait(false);
            throw;
        }

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                await _process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                await ForceKillAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public Task NotifyDidOpenAsync(
        string documentUri,
        int version,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();
        return _rpc.NotifyWithParameterObjectAsync(
            "textDocument/didOpen",
            new
            {
                textDocument = new
                {
                    uri = documentUri,
                    languageId = "csharp",
                    version,
                    text,
                },
            });
    }

    /// <inheritdoc />
    public Task NotifyDidChangeAsync(
        string documentUri,
        int version,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();
        return _rpc.NotifyWithParameterObjectAsync(
            "textDocument/didChange",
            new
            {
                textDocument = new { uri = documentUri, version },
                contentChanges = new object[] { new { text } },
            });
    }

    /// <inheritdoc />
    public Task NotifyDidCloseAsync(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();
        return _rpc.NotifyWithParameterObjectAsync(
            "textDocument/didClose",
            new { textDocument = new { uri = documentUri } });
    }

    /// <inheritdoc />
    public async Task ForceKillAsync()
    {
        if (_process is null || _process.HasExited)
            return;

        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone.
        }

        try
        {
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best effort.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_process is not null)
            _process.Exited -= OnProcessExited;

        try
        {
            _rpc?.Dispose();
        }
        catch
        {
            // Transport may already be torn down.
        }

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(2000);
            }
            catch
            {
                // Best effort.
            }
        }

        _process?.Dispose();
        _process = null;
        _rpc = null;
    }

    private void OnProcessExited(object? sender, EventArgs e) => SignalProcessExited();

    private void SignalProcessExited()
    {
        if (Interlocked.Exchange(ref _exitSignaled, 1) != 0)
            return;

        try
        {
            ProcessExited?.Invoke(Generation);
        }
        catch
        {
            // Observers must not tear down the session.
        }
    }

    /// <summary>
    /// StreamJsonRpc local target for <c>textDocument/publishDiagnostics</c>.
    /// </summary>
    private void OnPublishDiagnostics(JsonElement @params) => HandlePublishDiagnostics(@params);

    private void HandlePublishDiagnostics(JsonElement @params)
    {
        try
        {
            if (@params.ValueKind != JsonValueKind.Object)
                return;

            if (!@params.TryGetProperty("uri", out var uriElement) ||
                uriElement.ValueKind != JsonValueKind.String)
                return;

            var uri = uriElement.GetString();
            if (string.IsNullOrWhiteSpace(uri))
                return;

            int? version = null;
            if (@params.TryGetProperty("version", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.Number &&
                versionElement.TryGetInt32(out var versionValue))
            {
                version = versionValue;
            }

            var payloads = new List<LanguageServerDiagnosticPayload>();
            if (@params.TryGetProperty("diagnostics", out var diagnosticsElement) &&
                diagnosticsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in diagnosticsElement.EnumerateArray())
                {
                    if (TryParseDiagnosticPayload(item, out var payload))
                        payloads.Add(payload);
                }
            }

            var notification = new LanguageServerPublishDiagnostics(
                Generation,
                uri,
                version,
                payloads);

            try
            {
                DiagnosticsPublished?.Invoke(notification);
            }
            catch
            {
                // Observers must not tear down the session.
            }
        }
        catch
        {
            // Malformed notifications are ignored; the session stays alive.
        }
    }

    private static bool TryParseDiagnosticPayload(
        JsonElement item,
        out LanguageServerDiagnosticPayload payload)
    {
        payload = null!;

        if (item.ValueKind != JsonValueKind.Object)
            return false;

        if (!item.TryGetProperty("message", out var messageElement) ||
            messageElement.ValueKind != JsonValueKind.String)
            return false;

        var message = messageElement.GetString();
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (!item.TryGetProperty("range", out var rangeElement) ||
            !TryParseRange(rangeElement, out var range))
            return false;

        var severity = LanguageDiagnosticSeverity.Error;
        if (item.TryGetProperty("severity", out var severityElement) &&
            severityElement.ValueKind == JsonValueKind.Number &&
            severityElement.TryGetInt32(out var severityValue) &&
            severityValue is >= 1 and <= 4)
        {
            severity = (LanguageDiagnosticSeverity)severityValue;
        }

        string? code = null;
        if (item.TryGetProperty("code", out var codeElement))
        {
            code = codeElement.ValueKind switch
            {
                JsonValueKind.String => codeElement.GetString(),
                JsonValueKind.Number => codeElement.GetRawText(),
                _ => null,
            };
        }

        string? source = null;
        if (item.TryGetProperty("source", out var sourceElement) &&
            sourceElement.ValueKind == JsonValueKind.String)
        {
            source = sourceElement.GetString();
        }

        payload = new LanguageServerDiagnosticPayload(
            severity,
            message,
            code,
            source,
            range);
        return true;
    }

    private static bool TryParseRange(JsonElement rangeElement, out LspRange range)
    {
        range = default;
        if (rangeElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!rangeElement.TryGetProperty("start", out var startElement) ||
            !rangeElement.TryGetProperty("end", out var endElement))
            return false;

        if (!TryParsePosition(startElement, out var startLine, out var startCharacter) ||
            !TryParsePosition(endElement, out var endLine, out var endCharacter))
            return false;

        range = new LspRange(startLine, startCharacter, endLine, endCharacter);
        return true;
    }

    private static bool TryParsePosition(JsonElement positionElement, out int line, out int character)
    {
        line = 0;
        character = 0;

        if (positionElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!positionElement.TryGetProperty("line", out var lineElement) ||
            lineElement.ValueKind != JsonValueKind.Number ||
            !lineElement.TryGetInt32(out line))
            return false;

        if (!positionElement.TryGetProperty("character", out var characterElement) ||
            characterElement.ValueKind != JsonValueKind.Number ||
            !characterElement.TryGetInt32(out character))
            return false;

        return true;
    }

    private static Dictionary<string, object?> BuildInitializeParams(
        string workspaceUri,
        LanguageServerStartOptions options)
    {
        object? initializationOptions = options.ProjectKind == ProjectKind.SolutionX
            ? new Dictionary<string, object?> { ["solution"] = options.ProjectFilePath }
            : null;

        var initParams = new Dictionary<string, object?>
        {
            ["processId"] = Environment.ProcessId,
            ["clientInfo"] = new { name = "Zaide", version = "1.0.0" },
            ["rootUri"] = workspaceUri,
            ["rootPath"] = options.WorkspaceFolderPath,
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["general"] = new
                {
                    positionEncodings = new[] { "utf-16", "utf-8" },
                },
                ["textDocument"] = new Dictionary<string, object?>
                {
                    ["publishDiagnostics"] = new { relatedInformation = false },
                    ["synchronization"] = new
                    {
                        dynamicRegistration = false,
                        willSave = false,
                        willSaveWaitUntil = false,
                        didSave = false,
                    },
                },
                ["workspace"] = new Dictionary<string, object?>
                {
                    ["workspaceFolders"] = true,
                },
            },
            ["workspaceFolders"] = new object[]
            {
                new
                {
                    uri = workspaceUri,
                    name = Path.GetFileName(options.WorkspaceFolderPath.TrimEnd(Path.DirectorySeparatorChar)),
                },
            },
            ["trace"] = "off",
        };

        if (initializationOptions is not null)
            initParams["initializationOptions"] = initializationOptions;

        return initParams;
    }

}
