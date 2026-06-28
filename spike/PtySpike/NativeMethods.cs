using System;
using System.Runtime.InteropServices;

namespace PtySpike;

/// <summary>
/// Raw libc P/Invoke surface for the PTY spike. This is the candidate native
/// path for <c>LinuxTerminalService</c> (Phase 3, Step 2): allocate a PTY via
/// <c>posix_openpt</c> and spawn the shell via <c>posix_spawn</c> with
/// <c>POSIX_SPAWN_SETSID</c> — so no managed code runs in a post-fork child
/// branch.
/// </summary>
internal static class NativeMethods
{
    private const string Libc = "libc";

    // --- open flags (Linux) ---
    public const int O_RDWR = 0x0002;
    public const int O_NOCTTY = 0x0100;

    // --- posix_spawn attribute flags (glibc; SETSID requires glibc >= 2.26) ---
    public const short POSIX_SPAWN_SETSID = 0x80;

    // --- ioctl request: set window size ---
    public const nuint TIOCSWINSZ = 0x5414;

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

    // --- child reaping ---
    [DllImport(Libc, SetLastError = true)]
    public static extern int waitpid(int pid, out int status, int options);

    // --- posix_spawn family (opaque structs passed by pointer) ---
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
}
