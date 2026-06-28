using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Zaide.Services;

/// <summary>
/// Raw libc P/Invoke surface for PTY allocation and child-process management.
/// Used exclusively by <see cref="LinuxTerminalService"/> — never called from
/// Views or ViewModels.
/// </summary>
internal static class LinuxPtyInterop
{
    private const string Libc = "libc";

    // --- open flags (Linux) ---
    public const int O_RDWR = 0x0002;
    public const int O_NOCTTY = 0x0100;

    // --- posix_spawn attribute flags (glibc; SETSID requires glibc >= 2.26) ---
    public const short POSIX_SPAWN_SETSID = 0x80;

    // --- ioctl request: set window size ---
    public const nuint TIOCSWINSZ = 0x5414;

    // --- signals for shutdown ---
    public const int SIGHUP = 1;
    public const int SIGKILL = 9;

    // --- waitpid options ---
    public const int WNOHANG = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct Winsize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    // --- PTY allocation ---

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_openpt(int flags);

    [DllImport(Libc, SetLastError = true)]
    public static extern int grantpt(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern int unlockpt(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ptsname_r(int fd, byte[] buf, nuint buflen);

    // --- fd I/O ---

    [DllImport(Libc, SetLastError = true)]
    public static extern nint read(int fd, byte[] buf, nuint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint write(int fd, byte[] buf, nuint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, nuint request, ref Winsize ws);

    // --- process management ---

    [DllImport(Libc, SetLastError = true)]
    public static extern int kill(int pid, int sig);

    [DllImport(Libc, SetLastError = true)]
    public static extern int waitpid(int pid, out int status, int options);

    // --- posix_spawn family ---

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawn(
        out int pid,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        nint fileActions,
        nint attrp,
        nint argv,
        nint envp);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawn_file_actions_init(nint fileActions);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawn_file_actions_destroy(nint fileActions);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawn_file_actions_addopen(
        nint fileActions,
        int fd,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int oflag,
        uint mode);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawn_file_actions_adddup2(
        nint fileActions,
        int fd,
        int newfd);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawnattr_init(nint attr);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawnattr_destroy(nint attr);

    [DllImport(Libc, SetLastError = true)]
    public static extern int posix_spawnattr_setflags(nint attr, short flags);

    // --- helpers ---

    /// <summary>
    /// Reads the slave device path from the master fd via <c>ptsname_r</c>.
    /// </summary>
    public static string GetSlaveName(int master)
    {
        var buf = new byte[256];
        if (ptsname_r(master, buf, (nuint)buf.Length) != 0)
            return string.Empty;
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        return System.Text.Encoding.UTF8.GetString(buf, 0, len);
    }

    /// <summary>
    /// Builds a NULL-terminated <c>char**</c> in native memory. The default
    /// <c>string[]</c> marshaller does not append the NULL terminator that
    /// <c>posix_spawn</c> requires.
    /// </summary>
    public static nint BuildNativeStringArray(string[] items, List<nint> allocations)
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
}
