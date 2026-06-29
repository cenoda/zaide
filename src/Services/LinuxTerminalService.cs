using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Linux implementation of <see cref="ITerminalService"/>. Manages a PTY
/// master/slave pair and a child shell process via <c>posix_spawn</c>.
///
/// <para><b>Threading:</b> <see cref="OutputReceived"/> and
/// <see cref="ProcessExited"/> are raised on a background reader thread —
/// callers must marshal to the UI thread themselves.</para>
/// </summary>
public sealed class LinuxTerminalService : ITerminalService
{
    private int _master = -1;
    private int _pid = -1;
    private Thread? _reader;
    private readonly object _writeLock = new();
    private int _exitSignaled;
    private volatile bool _disposed;
    private volatile bool _isRunning;

    /// <inheritdoc/>
    public event Action<byte[]>? OutputReceived;

    /// <inheritdoc/>
    public event Action? ProcessExited;

    /// <inheritdoc/>
    public bool IsRunning
    {
        get => _isRunning;
        private set => _isRunning = value;
    }

    /// <inheritdoc/>
    public Task StartAsync(string shell = "/bin/bash", CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return Task.CompletedTask;
        ct.ThrowIfCancellationRequested();

        // --- reset restart state (start -> exit -> restart) ---
        // A previous session may have exited. Its reader thread already ran
        // SignalExit() and returned, but join it defensively so two reader
        // threads never overlap, then clear the exit latch and stale handles
        // so the next exit is detected, reaped, and raised exactly once again.
        _reader?.Join(TimeSpan.FromSeconds(2));
        _reader = null;

        // A clean shell exit (ReadLoop EOF -> SignalExit) does NOT close the
        // master fd — only Dispose does. On restart we must close the previous
        // master ourselves, otherwise every cycle leaks one PTY fd until
        // terminal creation eventually fails.
        if (_master >= 0)
        {
            LinuxPtyInterop.close(_master);
            _master = -1;
        }

        _pid = -1;
        Interlocked.Exchange(ref _exitSignaled, 0);

        // --- allocate the PTY master (close fd on any partial failure) ---
        _master = LinuxPtyInterop.posix_openpt(LinuxPtyInterop.O_RDWR | LinuxPtyInterop.O_NOCTTY);
        if (_master < 0)
            throw new InvalidOperationException($"posix_openpt failed, errno={Marshal.GetLastPInvokeError()}");

        try
        {
            if (LinuxPtyInterop.grantpt(_master) != 0)
                throw new InvalidOperationException($"grantpt failed, errno={Marshal.GetLastPInvokeError()}");

            if (LinuxPtyInterop.unlockpt(_master) != 0)
                throw new InvalidOperationException($"unlockpt failed, errno={Marshal.GetLastPInvokeError()}");

            string slavePath = LinuxPtyInterop.GetSlaveName(_master);
            if (string.IsNullOrEmpty(slavePath))
                throw new InvalidOperationException("ptsname_r returned empty slave path");

            // --- spawn the shell with the slave wired to stdin/stdout/stderr ---
            _pid = SpawnShell(shell, slavePath);
            if (_pid <= 0)
                throw new InvalidOperationException("posix_spawn failed to produce a valid pid");
        }
        catch
        {
            LinuxPtyInterop.close(_master);
            _master = -1;
            throw;
        }

        IsRunning = true;
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "pty-reader" };
        _reader.Start();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        if (!IsRunning || _master < 0) return Task.CompletedTask;
        ct.ThrowIfCancellationRequested();

        lock (_writeLock)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                var slice = offset == 0 ? data : data[offset..];
                nint n = LinuxPtyInterop.write(_master, slice, (nuint)slice.Length);
                if (n <= 0) break;
                offset += (int)n;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Resize(int columns, int rows)
    {
        if (!IsRunning || _master < 0) return;

        var ws = new LinuxPtyInterop.Winsize
        {
            ws_row = (ushort)rows,
            ws_col = (ushort)columns
        };
        LinuxPtyInterop.ioctl(_master, LinuxPtyInterop.TIOCSWINSZ, ref ws);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pid > 0)
            LinuxPtyInterop.kill(_pid, LinuxPtyInterop.SIGHUP);

        if (_master >= 0)
        {
            LinuxPtyInterop.close(_master);
            _master = -1;
        }

        _reader?.Join(TimeSpan.FromSeconds(2));

        // Belt-and-suspenders: reap if the reader thread didn't get to it.
        if (_pid > 0 &&
            LinuxPtyInterop.waitpid(_pid, out _, LinuxPtyInterop.WNOHANG) == 0)
        {
            LinuxPtyInterop.kill(_pid, LinuxPtyInterop.SIGKILL);
            LinuxPtyInterop.waitpid(_pid, out _, 0);
        }

        IsRunning = false;
    }

    // --- private ---

    /// <summary>
    /// Reader thread loop. Owns exit detection and child reaping (single-owner
    /// rule). Treats EOF and EIO as normal end-of-stream.
    /// </summary>
    private void ReadLoop()
    {
        var buf = new byte[4096];
        while (true)
        {
            nint n = LinuxPtyInterop.read(_master, buf, (nuint)buf.Length);
            if (n <= 0) break;

            var chunk = new byte[(int)n];
            Array.Copy(buf, chunk, (int)n);
            OutputReceived?.Invoke(chunk);
        }

        SignalExit();
    }

    /// <summary>
    /// Reaps the child process and raises <see cref="ProcessExited"/> exactly
    /// once, regardless of how many threads call this.
    /// </summary>
    private void SignalExit()
    {
        if (Interlocked.Exchange(ref _exitSignaled, 1) != 0) return;

        if (_pid > 0)
            LinuxPtyInterop.waitpid(_pid, out _, 0);

        IsRunning = false;
        ProcessExited?.Invoke();
    }

    /// <summary>
    /// Spawns the shell with the PTY slave attached to the child's
    /// stdin/stdout/stderr. <c>POSIX_SPAWN_SETSID</c> makes the child a
    /// session leader, and opening the slave (without <c>O_NOCTTY</c>) makes
    /// it the controlling terminal. All of this happens inside
    /// <c>posix_spawn</c> — no managed code runs between fork and exec.
    /// </summary>
    private static int SpawnShell(string shell, string slavePath)
    {
        // Opaque glibc structs; over-allocate and let init() zero them.
        nint fileActions = Marshal.AllocHGlobal(1024);
        nint attr = Marshal.AllocHGlobal(1024);
        var allocations = new List<nint>();

        try
        {
            if (LinuxPtyInterop.posix_spawn_file_actions_init(fileActions) != 0)
                return -1;
            if (LinuxPtyInterop.posix_spawnattr_init(attr) != 0)
                return -1;

            // fd 0 = slave (no O_NOCTTY → becomes controlling terminal),
            // then duplicate onto 1 and 2.
            LinuxPtyInterop.posix_spawn_file_actions_addopen(
                fileActions, 0, slavePath, LinuxPtyInterop.O_RDWR, 0);
            LinuxPtyInterop.posix_spawn_file_actions_adddup2(fileActions, 0, 1);
            LinuxPtyInterop.posix_spawn_file_actions_adddup2(fileActions, 0, 2);

            LinuxPtyInterop.posix_spawnattr_setflags(attr, LinuxPtyInterop.POSIX_SPAWN_SETSID);

            nint argv = LinuxPtyInterop.BuildNativeStringArray(new[] { shell }, allocations);
            nint envp = LinuxPtyInterop.BuildNativeStringArray(BuildEnvironment(), allocations);

            int rc = LinuxPtyInterop.posix_spawn(
                out int pid, shell, fileActions, attr, argv, envp);

            return rc == 0 ? pid : -1;
        }
        finally
        {
            LinuxPtyInterop.posix_spawn_file_actions_destroy(fileActions);
            LinuxPtyInterop.posix_spawnattr_destroy(attr);
            Marshal.FreeHGlobal(fileActions);
            Marshal.FreeHGlobal(attr);
            foreach (nint p in allocations)
                Marshal.FreeCoTaskMem(p);
        }
    }

    private static string[] BuildEnvironment()
    {
        var env = new List<string>();
        foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
        {
            if (kv.Key is string key && !key.Equals("TERM", StringComparison.Ordinal))
                env.Add($"{key}={kv.Value}");
        }
        env.Add("TERM=xterm-256color");
        return env.ToArray();
    }
}
