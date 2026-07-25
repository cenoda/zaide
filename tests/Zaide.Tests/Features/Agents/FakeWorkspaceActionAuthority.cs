using System;
using System.Runtime.InteropServices;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Test-only workspace action authority. Reports the captured scope as current
/// until <see cref="IsStale"/> is set, simulating a workspace close/switch that
/// bumps the workspace generation between capture and execution.
/// <see cref="HasWorkspace"/> controls whether <see cref="TryCaptureCurrentScope"/>
/// returns <c>true</c>; <see cref="IsStale"/> controls whether
/// <see cref="IsCurrent"/> reports a previously-captured scope as still current.
/// This separation allows tests to exercise no-workspace and stale-generation
/// cases independently.
/// </summary>
internal sealed class FakeWorkspaceActionAuthority : IWorkspaceActionAuthority
{
    private readonly WorkspaceActionScope _scope;

    public FakeWorkspaceActionAuthority(WorkspaceActionScope scope)
    {
        _scope = scope;
    }

    /// <summary>
    /// When <c>false</c>, <see cref="TryCaptureCurrentScope"/> returns <c>false</c>,
    /// simulating a closed workspace.
    /// </summary>
    public bool HasWorkspace { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, <see cref="IsCurrent"/> returns <c>false</c>, simulating
    /// a workspace generation change since capture.
    /// </summary>
    public bool IsStale { get; set; }

    public bool TryCaptureCurrentScope(out WorkspaceActionScope scope)
    {
        scope = _scope;
        return HasWorkspace;
    }

    public bool IsCurrent(WorkspaceActionScope scope) =>
        HasWorkspace && !IsStale && _scope.Equals(scope);

    /// <summary>
    /// Creates a <see cref="WorkspaceActionScope"/> from a directory path,
    /// stat'ing it for device and inode so the scope carries mandatory root
    /// filesystem identity. The canonical root is assumed to match
    /// <paramref name="directoryPath"/> (callers using symlinks must supply
    /// the real canonical path separately).
    /// </summary>
    public static WorkspaceActionScope CreateScopeFromDirectory(string directoryPath)
    {
        StatDeviceInode(directoryPath, out var dev, out var ino);
        return new WorkspaceActionScope(
            WorkspaceIdentity.New(),
            WorkspaceGeneration.Initial,
            directoryPath,
            capturedCanonicalRoot: directoryPath,
            capturedRootDevice: dev,
            capturedRootInode: ino);
    }

    /// <summary>
    /// Creates a <see cref="WorkspaceActionScope"/> with explicit identity
    /// fields. Stats the <paramref name="directoryPath"/> for device/inode.
    /// </summary>
    public static WorkspaceActionScope CreateScope(
        WorkspaceIdentity identity,
        WorkspaceGeneration generation,
        string rootPath,
        string capturedCanonicalRoot)
    {
        StatDeviceInode(capturedCanonicalRoot, out var dev, out var ino);
        return new WorkspaceActionScope(
            identity,
            generation,
            rootPath,
            capturedCanonicalRoot,
            dev,
            ino);
    }

    private static void StatDeviceInode(string path, out ulong device, out ulong inode)
    {
        var buffer = new byte[256];
        var result = Stat(path, buffer);
        if (result != 0)
        {
            throw new InvalidOperationException(
                $"stat failed for '{path}'. The test directory must exist.");
        }

        // Linux x86-64 struct stat: st_dev at 0, st_ino at 8.
        device = BitConverter.ToUInt64(buffer, 0);
        inode = BitConverter.ToUInt64(buffer, 8);
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int Stat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        byte[] statBuffer);
}
