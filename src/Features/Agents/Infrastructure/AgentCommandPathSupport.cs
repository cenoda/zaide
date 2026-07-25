using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Shared canonical path helpers for Phase 17 command resolution and execution.
/// </summary>
internal static class AgentCommandPathSupport
{
    private const uint S_IFMT = 0xF000;
    private const uint S_IFREG = 0x8000;
    private const uint S_IFLNK = 0xA000;
    private const uint S_IXUSR = 0x40;
    private const int StatBufferSize = 256;
    private const int StModeOffset = 24;

    public static bool IsContained(string canonicalRoot, string canonicalCandidate)
    {
        if (string.Equals(canonicalRoot, canonicalCandidate, StringComparison.Ordinal))
        {
            return true;
        }

        var rootWithSeparator = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;

        return canonicalCandidate.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    public static bool TryRealpath(string path, out string resolved)
    {
        resolved = string.Empty;
        IntPtr pointer;
        try
        {
            pointer = Realpath(path, IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        if (pointer == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            resolved = Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
            return resolved.Length > 0;
        }
        finally
        {
            Free(pointer);
        }
    }

    public static bool TryResolveSymlinkChain(
        string path,
        out string canonicalTarget,
        out IReadOnlyList<string> symlinkChain)
    {
        canonicalTarget = string.Empty;
        symlinkChain = Array.Empty<string>();

        if (!TryRealpath(path, out canonicalTarget))
        {
            return false;
        }

        if (string.Equals(path, canonicalTarget, StringComparison.Ordinal))
        {
            return true;
        }

        var chain = new List<string>();
        var current = path;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var hop = 0; hop < 32; hop++)
        {
            if (!visited.Add(current))
            {
                return false;
            }

            if (!TryGetMode(current, out var mode) || (mode & S_IFMT) != S_IFLNK)
            {
                break;
            }

            chain.Add(current);
            if (!TryReadLink(current, out var target))
            {
                return false;
            }

            current = Path.IsPathRooted(target)
                ? Path.GetFullPath(target)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(current) ?? ".", target));
        }

        symlinkChain = chain;
        return string.Equals(current, canonicalTarget, StringComparison.Ordinal)
            || string.Equals(path, canonicalTarget, StringComparison.Ordinal);
    }

    public static bool IsRegularExecutableFile(string canonicalPath)
    {
        if (!File.Exists(canonicalPath))
        {
            return false;
        }

        if (!TryGetMode(canonicalPath, out var mode))
        {
            return false;
        }

        if ((mode & S_IFMT) != S_IFREG)
        {
            return false;
        }

        return (mode & S_IXUSR) != 0;
    }

    public static bool IsDirectory(string canonicalPath) =>
        Directory.Exists(canonicalPath);

    private static bool TryReadLink(string path, out string target)
    {
        target = string.Empty;
        var buffer = new byte[4096];
        int length;
        try
        {
            length = ReadLink(path, buffer, (nuint)buffer.Length);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        if (length <= 0)
        {
            return false;
        }

        target = Encoding.UTF8.GetString(buffer, 0, length);
        return target.Length > 0;
    }

    private static bool TryGetMode(string path, out uint mode)
    {
        mode = 0;
        var buffer = new byte[StatBufferSize];
        if (Stat(path, buffer) != 0)
        {
            return false;
        }

        mode = BitConverter.ToUInt32(buffer, StModeOffset);
        return true;
    }

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr Realpath(string path, IntPtr resolved);

    [DllImport("libc", EntryPoint = "free", SetLastError = true)]
    private static extern void Free(IntPtr pointer);

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat(string path, byte[] buffer);

    [DllImport("libc", EntryPoint = "readlink", SetLastError = true)]
    private static extern int ReadLink(string path, byte[] buffer, nuint bufferSize);
}
