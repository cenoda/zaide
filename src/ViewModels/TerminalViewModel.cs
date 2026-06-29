using System;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for the embedded terminal panel. Owns the raw output buffer and
/// bridges the UI-agnostic <see cref="ITerminalService"/> to the view.
///
/// <para><b>Threading:</b> <see cref="ITerminalService"/> raises its events on
/// a background reader thread. This ViewModel decodes bytes on that thread
/// (the UTF-8 <see cref="Decoder"/> is only ever touched there, so it stays
/// single-threaded across chunk boundaries) and then marshals the buffer
/// mutation onto the UI thread before raising <see cref="OutputText"/>
/// changes.</para>
///
/// Registered as a Singleton — one terminal session for the app lifetime.
/// </summary>
public class TerminalViewModel : ReactiveObject, IDisposable
{
    private const int DefaultMaxBufferChars = 200_000;

    private readonly ITerminalService _service;
    private readonly Action<Action> _uiPost;
    private readonly int _maxBufferChars;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _bufferLock = new();

    private bool _startRequested;
    private volatile bool _disposed;
    private int _currentColumns;
    private int _currentRows;
    private int _pendingColumns;
    private int _pendingRows;

    /// <summary>
    /// Raw terminal output accumulated so far (bounded ring buffer). No ANSI
    /// parsing — escape sequences appear verbatim (Phase 3 MVP scope).
    /// </summary>
    public string OutputText
    {
        get { lock (_bufferLock) return _outputBuffer.ToString(); }
    }

    private bool _isRunning;
    /// <summary>Whether the underlying shell process is alive.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    private string? _startupError;
    /// <summary>
    /// Error message from the last failed <see cref="EnsureStartedAsync"/>.
    /// null before any start attempt and after a successful start.
    /// </summary>
    public string? StartupError
    {
        get => _startupError;
        private set => this.RaiseAndSetIfChanged(ref _startupError, value);
    }

    /// <summary>Clears the output buffer.</summary>
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    /// <summary>Production constructor used by the DI container.</summary>
    public TerminalViewModel(ITerminalService terminalService)
        : this(terminalService, action => Dispatcher.UIThread.Post(action), DefaultMaxBufferChars)
    {
    }

    /// <summary>
    /// Seam constructor for tests: <paramref name="uiPost"/> lets tests run the
    /// marshaled work synchronously, and <paramref name="maxBufferChars"/> lets
    /// them exercise buffer trimming with a small capacity.
    /// </summary>
    internal TerminalViewModel(ITerminalService terminalService, Action<Action> uiPost, int maxBufferChars)
    {
        _service = terminalService;
        _uiPost = uiPost;
        _maxBufferChars = maxBufferChars;

        ClearCommand = ReactiveCommand.Create(Clear);

        _service.OutputReceived += OnOutputReceived;
        _service.ProcessExited += OnProcessExited;
    }

    /// <summary>
    /// Lazily starts the terminal on first reveal/focus. Safe to call multiple
    /// times — only the first call starts the session. On failure, records
    /// <see cref="StartupError"/> and allows a later retry.
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        if (_startRequested || _disposed) return;
        _startRequested = true;

        try
        {
            await _service.StartAsync();
            StartupError = null;
            IsRunning = _service.IsRunning;

            // If the panel computed a viewport size before startup, the PTY
            // silently ignored it. Reapply now so the shell gets its real size.
            ApplyPendingResize();
        }
        catch (Exception ex)
        {
            StartupError = ex.Message;
            IsRunning = false;
            _startRequested = false; // allow a retry on the next reveal
        }
    }

    /// <summary>
    /// Forwards raw input bytes to the shell. Safe before startup or after
    /// exit — the service treats writes as a no-op when not running.
    /// </summary>
    public Task SendInputAsync(byte[] data) => _service.WriteAsync(data);

    /// <summary>
    /// Forwards a terminal viewport resize to the PTY backend. Safe to call
    /// before startup or after exit — the service treats it as a no-op.
    /// Only forwards to the service when the dimensions actually change.
    /// When called before the service is running, the latest dimensions are
    /// remembered and forwarded automatically after startup completes.
    /// </summary>
    /// <param name="columns">Number of terminal columns.</param>
    /// <param name="rows">Number of terminal rows.</param>
    public void Resize(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0) return;

        // Always remember the latest request so it survives startup.
        _pendingColumns = columns;
        _pendingRows = rows;

        if (columns == _currentColumns && rows == _currentRows) return;

        _currentColumns = columns;
        _currentRows = rows;
        _service.Resize(columns, rows);
    }

    /// <summary>
    /// Reapplies the last <see cref="Resize"/> request to the service. Called
    /// after startup completes so the PTY receives the real viewport size
    /// even if the panel computed it before the shell was alive.
    /// </summary>
    private void ApplyPendingResize()
    {
        if (_pendingColumns <= 0 || _pendingRows <= 0) return;

        _service.Resize(_pendingColumns, _pendingRows);
        _currentColumns = _pendingColumns;
        _currentRows = _pendingRows;
    }

    private void OnOutputReceived(byte[] data)
    {
        if (_disposed || data.Length == 0) return;

        // Decode on the reader thread. The Decoder retains any trailing bytes
        // of an incomplete multibyte sequence for the next chunk.
        var chars = new char[data.Length];
        int n = _decoder.GetChars(data, 0, data.Length, chars, 0, flush: false);
        if (n == 0) return; // incomplete sequence; nothing to render yet

        string text = new string(chars, 0, n);
        _uiPost(() => Append(text));
    }

    private void OnProcessExited()
    {
        _uiPost(() =>
        {
            IsRunning = false;
            Append("\r\n[Process exited]\r\n");
        });
    }

    private void Append(string text)
    {
        lock (_bufferLock)
        {
            _outputBuffer.Append(text);
            int excess = _outputBuffer.Length - _maxBufferChars;
            if (excess > 0)
                _outputBuffer.Remove(0, excess);
        }
        this.RaisePropertyChanged(nameof(OutputText));
    }

    private void Clear()
    {
        lock (_bufferLock)
            _outputBuffer.Clear();
        this.RaisePropertyChanged(nameof(OutputText));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _service.OutputReceived -= OnOutputReceived;
        _service.ProcessExited -= OnProcessExited;
        _service.Dispose();
    }
}
