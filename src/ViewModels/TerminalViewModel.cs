using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for the embedded terminal panel. Owns the ANSI parser, screen-buffer
/// model, and bridges the UI-agnostic <see cref="ITerminalService"/> to the view.
///
/// <para><b>Threading:</b> <see cref="ITerminalService"/> raises its events on
/// a background reader thread. This ViewModel decodes bytes on that thread
/// (the UTF-8 <see cref="Decoder"/> is only ever touched there, so it stays
/// single-threaded across chunk boundaries) and then marshals the buffer
/// mutation onto the UI thread before raising snapshot changes.</para>
///
/// Registered as a Singleton — one terminal session for the app lifetime.
/// </summary>
public class TerminalViewModel : ReactiveObject, IDisposable
{
    private static readonly byte[] ClearInput = new byte[] { 0x0C };

    private readonly ITerminalService _service;
    private readonly Action<Action> _uiPost;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly AnsiParser _parser = new();
    private readonly TerminalScreen _screen = new();

    private bool _startRequested;
    private volatile bool _disposed;
    private int _currentColumns;
    private int _currentRows;
    private int _pendingColumns;
    private int _pendingRows;

    // ── view-bound properties ──────────────────────────────────────

    private TerminalSnapshot? _screenSnapshot;
    /// <summary>
    /// Immutable snapshot of the visible terminal surface, projected from the
    /// internal screen buffer after every mutation. null before any output is
    /// received.
    /// </summary>
    public TerminalSnapshot? ScreenSnapshot
    {
        get => _screenSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _screenSnapshot, value);
    }

    private int _cursorRow;
    /// <summary>0-based cursor row for render control display.</summary>
    public int CursorRow
    {
        get => _cursorRow;
        private set => this.RaiseAndSetIfChanged(ref _cursorRow, value);
    }

    private int _cursorCol;
    /// <summary>0-based cursor column for render control display.</summary>
    public int CursorCol
    {
        get => _cursorCol;
        private set => this.RaiseAndSetIfChanged(ref _cursorCol, value);
    }

    private bool _cursorVisible;
    /// <summary>
    /// Whether the cursor block is shown. True when the shell is running and
    /// the terminal is focused (focus is tracked in the view layer).
    /// </summary>
    public bool CursorVisible
    {
        get => _cursorVisible;
        private set => this.RaiseAndSetIfChanged(ref _cursorVisible, value);
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
        private set
        {
            this.RaiseAndSetIfChanged(ref _startupError, value);
            this.RaisePropertyChanged(nameof(StatusLabel));
        }
    }

    private TerminalState _state = TerminalState.NotStarted;
    /// <summary>Current lifecycle state of the terminal session.</summary>
    public TerminalState State
    {
        get => _state;
        private set
        {
            this.RaiseAndSetIfChanged(ref _state, value);
            this.RaisePropertyChanged(nameof(StatusLabel));
        }
    }

    /// <summary>Human-readable status for display in the control strip.</summary>
    public string StatusLabel => State switch
    {
        TerminalState.NotStarted => "Not started",
        TerminalState.Running => "Running",
        TerminalState.Exited => "Exited",
        TerminalState.Error => StartupError is { Length: > 0 } e ? $"Error: {e}" : "Error",
        _ => string.Empty
    };

    /// <summary>Clears the terminal surface.</summary>
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    /// <summary>
    /// Restarts the shell after it has exited. Disabled while the terminal is
    /// running. See <see cref="RestartAsync"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RestartCommand { get; }

    /// <summary>Production constructor used by the DI container.</summary>
    public TerminalViewModel(ITerminalService terminalService)
        : this(terminalService, action => Dispatcher.UIThread.Post(action))
    {
    }

    /// <summary>
    /// Seam constructor for tests: <paramref name="uiPost"/> lets tests run the
    /// marshaled work synchronously.
    /// </summary>
    internal TerminalViewModel(ITerminalService terminalService, Action<Action> uiPost)
    {
        _service = terminalService;
        _uiPost = uiPost;

        ClearCommand = ReactiveCommand.CreateFromTask(ClearAsync);

        // Restart is only meaningful once the shell is not running.
        RestartCommand = ReactiveCommand.CreateFromTask(
            RestartAsync,
            this.WhenAnyValue(x => x.IsRunning, running => !running));

        _service.OutputReceived += OnOutputReceived;
        _service.ProcessExited += OnProcessExited;

        // Cursor is visible whenever the shell is running (focus dimming is
        // handled in the view layer). Start invisible until the shell starts.
        CursorVisible = false;
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
            State = IsRunning ? TerminalState.Running : TerminalState.Exited;
            CursorVisible = IsRunning;

            // If the panel computed a viewport size before startup, the PTY
            // silently ignored it. Reapply now so the shell gets its real size.
            ApplyPendingResize();
        }
        catch (Exception ex)
        {
            StartupError = ex.Message;
            IsRunning = false;
            State = TerminalState.Error;
            CursorVisible = false;
            _startRequested = false; // allow a retry on the next reveal
        }
    }

    /// <summary>
    /// Restarts the shell session after a clean exit (or a failed start).
    /// <see cref="EnsureStartedAsync"/> leaves <c>_startRequested</c> set, so
    /// this deliberately clears that gate and resets the cached viewport size
    /// before starting again. The singleton service is reused, so event
    /// subscriptions are not re-added and cannot duplicate. No-op if the
    /// terminal is already running or the ViewModel is disposed.
    /// </summary>
    public async Task RestartAsync()
    {
        if (_disposed || IsRunning) return;

        // Clear the one-shot start gate and the cached size so the next
        // resize (and the post-start reapply) reach the new PTY.
        _startRequested = false;
        _currentColumns = 0;
        _currentRows = 0;

        await EnsureStartedAsync();
    }

    /// <summary>
    /// Forwards raw input bytes to the shell. Safe before startup or after
    /// exit — the service treats writes as a no-op when not running.
    /// </summary>
    public Task SendInputAsync(byte[] data) => _service.WriteAsync(data);

    /// <summary>
    /// Forwards a terminal viewport resize to the PTY backend and the screen
    /// buffer. Safe to call before startup or after exit — the service treats
    /// it as a no-op. Only forwards to the service when the dimensions
    /// actually change. When called before the service is running, the latest
    /// dimensions are remembered and forwarded automatically after startup
    /// completes.
    /// </summary>
    /// <param name="columns">Number of terminal columns.</param>
    /// <param name="rows">Number of terminal rows.</param>
    public void Resize(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0) return;

        // Always remember the latest request so it survives startup.
        _pendingColumns = columns;
        _pendingRows = rows;

        // Resize the screen buffer (always — the screen stays in sync
        // regardless of whether the PTY is alive yet).
        _screen.Resize(columns, rows);
        UpdateSnapshot();

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
            State = TerminalState.Exited;
            CursorVisible = false;
            Append("\r\n[Process exited]\r\n");
        });
    }

    /// <summary>
    /// Parses decoded text through the ANSI parser and applies all resulting
    /// actions to the screen buffer, then projects a new snapshot for view
    /// binding.
    /// </summary>
    private void Append(string text)
    {
        IReadOnlyList<AnsiAction> actions = _parser.Parse(text.AsSpan());
        foreach (var action in actions)
        {
            switch (action)
            {
                case PrintAction p:
                    _screen.WriteText(p.Text.AsSpan());
                    break;

                case ExecuteAction e:
                    _screen.ExecuteC0(e.Control);
                    break;

                case CsiDispatchAction csi:
                    ApplyCsi(csi);
                    break;
            }
        }

        UpdateSnapshot();
    }

    private void ApplyCsi(CsiDispatchAction csi)
    {
        int[] p = csi.Parameters;
        int n = p.Length > 0 ? p[0] : 0;

        switch (csi.FinalByte)
        {
            case 'A':
                _screen.CursorUp(n > 0 ? n : 1);
                break;
            case 'B':
                _screen.CursorDown(n > 0 ? n : 1);
                break;
            case 'C':
                _screen.CursorForward(n > 0 ? n : 1);
                break;
            case 'D':
                _screen.CursorBack(n > 0 ? n : 1);
                break;
            case 'H':
                int row = p.Length > 0 ? p[0] : 1;
                int col = p.Length > 1 ? p[1] : 1;
                _screen.CursorPosition(row, col);
                break;
            case 'J':
                _screen.EraseDisplay(n);
                break;
            case 'K':
                _screen.EraseLine(n);
                break;
            case 'm':
                _screen.SetSgr(csi.Parameters);
                break;
        }
    }

    /// <summary>
    /// Projects the current screen buffer state into a <see cref="TerminalSnapshot"/>
    /// and updates all view-bound properties.
    /// </summary>
    private void UpdateSnapshot()
    {
        int cols = _screen.Columns;
        int rows = _screen.Rows;
        int scrollbackRows = _screen.ScrollbackRowCount;

        var lines = new string[rows];
        var cells = new TerminalCell[cols * rows];
        var scrollbackLines = new string[scrollbackRows];
        var scrollbackCells = new TerminalCell[cols * scrollbackRows];
        var lineChars = new char[cols];
        int idx = 0;
        int scrollbackIdx = 0;

        for (int r = 0; r < scrollbackRows; r++)
        {
            var row = _screen.GetScrollbackRow(r);
            for (int c = 0; c < cols; c++)
            {
                var cell = row[c];
                lineChars[c] = cell.Char;
                scrollbackCells[scrollbackIdx] = new TerminalCell(
                    cell.Char,
                    cell.Attribute.Foreground,
                    cell.Attribute.Background,
                    cell.Attribute.Bold,
                    cell.Attribute.Inverse);
                scrollbackIdx++;
            }

            scrollbackLines[r] = new string(lineChars);
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var cell = _screen.GetCell(r, c);
                lineChars[c] = cell.Char;
                cells[idx] = new TerminalCell(
                    cell.Char,
                    cell.Attribute.Foreground,
                    cell.Attribute.Background,
                    cell.Attribute.Bold,
                    cell.Attribute.Inverse);
                idx++;
            }

            lines[r] = new string(lineChars);
        }

        ScreenSnapshot = new TerminalSnapshot(cols, rows, lines, cells, scrollbackLines, scrollbackCells);
        CursorRow = _screen.CursorRow;
        CursorCol = _screen.CursorCol;
    }

    private async Task ClearAsync()
    {
        if (IsRunning)
        {
            await _service.WriteAsync(ClearInput);
            return;
        }

        _screen.EraseDisplay(3);
        _screen.CursorPosition(1, 1);
        _screen.SetSgr(new[] { 0 }); // reset active attributes (SGR reset)
        UpdateSnapshot();
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
