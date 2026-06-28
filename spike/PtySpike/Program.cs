using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static PtySpike.NativeMethods;

namespace PtySpike;

/// <summary>
/// Phase 3 (M0) PTY proof-of-concept. Verifies the four claims the rest of the
/// phase depends on, before any service/VM/view work is written:
///   1. A PTY master/slave pair can be allocated via libc.
///   2. A shell can be spawned with NO managed post-fork child code
///      (posix_spawn + POSIX_SPAWN_SETSID, slave wired to the child's 0/1/2).
///   3. Output can be read and input written over the master fd.
///   4. TIOCSWINSZ resizes the PTY without error.
///
/// This is a throwaway spike. If it passes, the design in
/// docs/phases/phase-3/IMPLEMENTATION_PLAN.md Step 2 is viable. If it fails,
/// pivot to the Outcome B (redirected-stream) fallback.
/// </summary>
internal static class Program
{
    private static readonly object OutputLock = new();
    private static readonly StringBuilder Captured = new();

    private static int Main()
    {
        Console.WriteLine("=== Zaide PTY spike (Phase 3 M0) ===\n");

        int master = -1;
        int pid = -1;
        var checks = new List<(string name, bool ok)>();

        try
        {
            // --- Step 1: allocate the PTY master ---
            master = posix_openpt(O_RDWR | O_NOCTTY);
            if (master < 0)
                return Fail("posix_openpt", master);
            if (grantpt(master) != 0)
                return Fail("grantpt", -1);
            if (unlockpt(master) != 0)
                return Fail("unlockpt", -1);

            string slavePath = GetSlaveName(master);
            checks.Add(("PTY allocated (master + slave path)", !string.IsNullOrEmpty(slavePath)));
            Console.WriteLine($"[1] master fd = {master}, slave = {slavePath}");

            // --- Step 2: spawn the shell with the slave wired in natively ---
            pid = SpawnShell("/bin/bash", slavePath);
            checks.Add(("Shell spawned via posix_spawn (no managed child code)", pid > 0));
            Console.WriteLine($"[2] bash pid = {pid}");
            if (pid <= 0)
                return Summarize(checks);

            // --- Step 3: read output / write input over the master fd ---
            var readerDone = new ManualResetEventSlim(false);
            var reader = new Thread(() => ReadLoop(master, readerDone)) { IsBackground = true };
            reader.Start();

            Thread.Sleep(400);                 // let the prompt settle
            WriteToPty(master, "echo hello\r"); // \r is what a real terminal sends for Enter
            Thread.Sleep(400);

            bool sawHello = SnapshotContains("hello");
            checks.Add(("Round-trip I/O: 'echo hello' produced 'hello'", sawHello));
            Console.WriteLine($"[3] round-trip 'hello' seen = {sawHello}");

            // --- Step 4: resize via TIOCSWINSZ ---
            var ws = new Winsize { ws_row = 40, ws_col = 120 };
            int resize = ioctl(master, TIOCSWINSZ, ref ws);
            checks.Add(("TIOCSWINSZ resize (40x120) returned 0", resize == 0));
            Console.WriteLine($"[4] ioctl(TIOCSWINSZ) = {resize}");

            // --- clean shutdown: exit the shell and reap it ---
            WriteToPty(master, "exit\r");
            readerDone.Wait(TimeSpan.FromSeconds(3));
            waitpid(pid, out int status, 0);
            pid = -1;
            Console.WriteLine($"[5] shell exited, status = {status}");

            return Summarize(checks);
        }
        finally
        {
            if (pid > 0)
                waitpid(pid, out _, 0);
            if (master >= 0)
                close(master);
        }
    }

    /// <summary>Reads the master fd until the slave closes (EIO) or EOF.</summary>
    private static void ReadLoop(int master, ManualResetEventSlim done)
    {
        var buf = new byte[4096];
        try
        {
            while (true)
            {
                nint n = read(master, buf, (nuint)buf.Length);
                if (n <= 0)
                    break; // 0 = EOF, -1 = EIO once all slave fds are closed
                string chunk = Encoding.UTF8.GetString(buf, 0, (int)n);
                lock (OutputLock)
                    Captured.Append(chunk);
                Console.Write(chunk); // echo raw PTY output so we can see it live
            }
        }
        catch
        {
            // spike: swallow; the check logic decides pass/fail
        }
        finally
        {
            done.Set();
        }
    }

    private static void WriteToPty(int master, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        write(master, bytes, (nuint)bytes.Length);
    }

    private static bool SnapshotContains(string needle)
    {
        lock (OutputLock)
            return Captured.ToString().Contains(needle, StringComparison.Ordinal);
    }

    private static string GetSlaveName(int master)
    {
        var buf = new byte[256];
        if (ptsname_r(master, buf, (nuint)buf.Length) != 0)
            return string.Empty;
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    /// <summary>
    /// Spawns <paramref name="shell"/> with the PTY slave attached to the
    /// child's stdin/stdout/stderr. POSIX_SPAWN_SETSID makes the child a
    /// session leader, and opening the slave (without O_NOCTTY) then makes it
    /// the controlling terminal. All of this happens inside posix_spawn — no
    /// managed code runs between fork and exec.
    /// </summary>
    private static int SpawnShell(string shell, string slavePath)
    {
        // Opaque glibc structs; over-allocate and let init() zero them.
        nint fileActions = Marshal.AllocHGlobal(1024);
        nint attr = Marshal.AllocHGlobal(1024);
        var allocations = new List<nint>();

        try
        {
            if (posix_spawn_file_actions_init(fileActions) != 0)
                return Fail("file_actions_init", -1);
            if (posix_spawnattr_init(attr) != 0)
                return Fail("spawnattr_init", -1);

            // fd 0 = slave (no O_NOCTTY → becomes controlling terminal), then
            // duplicate onto 1 and 2.
            posix_spawn_file_actions_addopen(fileActions, 0, slavePath, O_RDWR, 0);
            posix_spawn_file_actions_adddup2(fileActions, 0, 1);
            posix_spawn_file_actions_adddup2(fileActions, 0, 2);

            posix_spawnattr_setflags(attr, POSIX_SPAWN_SETSID);

            nint argv = BuildNativeStringArray(new[] { shell }, allocations);
            nint envp = BuildNativeStringArray(BuildEnvironment(), allocations);

            int rc = posix_spawn(out int pid, shell, fileActions, attr, argv, envp);
            if (rc != 0)
            {
                Console.Error.WriteLine($"posix_spawn failed: rc={rc}");
                return -1;
            }
            return pid;
        }
        finally
        {
            posix_spawn_file_actions_destroy(fileActions);
            posix_spawnattr_destroy(attr);
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

    /// <summary>
    /// Builds a NULL-terminated <c>char**</c> in native memory. The default
    /// string[] marshaller does not append the NULL terminator that
    /// posix_spawn requires, so it is built by hand.
    /// </summary>
    private static nint BuildNativeStringArray(string[] items, List<nint> allocations)
    {
        nint array = Marshal.AllocCoTaskMem((items.Length + 1) * IntPtr.Size);
        allocations.Add(array);
        for (int i = 0; i < items.Length; i++)
        {
            nint str = Marshal.StringToCoTaskMemUTF8(items[i]);
            allocations.Add(str);
            Marshal.WriteIntPtr(array, i * IntPtr.Size, str);
        }
        Marshal.WriteIntPtr(array, items.Length * IntPtr.Size, IntPtr.Zero);
        return array;
    }

    private static int Fail(string call, int rc)
    {
        int errno = Marshal.GetLastPInvokeError();
        Console.Error.WriteLine($"FAIL: {call} returned {rc}, errno={errno}");
        return 1;
    }

    private static int Summarize(List<(string name, bool ok)> checks)
    {
        Console.WriteLine("\n=== Results ===");
        bool allOk = true;
        foreach (var (name, ok) in checks)
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}");
            allOk &= ok;
        }
        Console.WriteLine();
        Console.WriteLine(allOk
            ? "PTY path is VIABLE → proceed with Step 2 (LinuxTerminalService)."
            : "PTY path FAILED → consider Outcome B (redirected-stream) fallback.");
        return allOk ? 0 : 1;
    }
}
