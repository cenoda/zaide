using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using WorkspaceDomain = Zaide.Features.Workspace.Domain.Workspace;

namespace Zaide.Features.Workspace.Infrastructure;

/// <summary>
/// Production workspace-owned authority that captures the active workspace
/// identity, generation, canonical root path, and root filesystem identity
/// (device + inode) from the live <see cref="WorkspaceDomain"/> state.
///
/// <para>
/// Subscribes to <see cref="WorkspaceDomain.WorkspaceFolderChanged"/> so that
/// generation advances on <em>every</em> transition — open, close, switch,
/// and reopen — even A → close → A. Identity is derived deterministically
/// from the canonical path; generation is an event-driven monotonic counter.
/// </para>
///
/// <para>
/// <see cref="TryCaptureCurrentScope"/> and <see cref="IsCurrent"/> are
/// thread-safe. <see cref="IsCurrent"/> validates the captured identity,
/// generation, canonical root, device, and inode against live filesystem
/// state — a stale scope is rejected before any file-system access.
/// </para>
///
/// <para>
/// No-workspace fails closed: <see cref="TryCaptureCurrentScope"/> returns
/// <c>false</c>. Unresolvable canonical roots and unavailable filesystem
/// identity also fail closed.
/// </para>
/// </summary>
internal sealed class WorkspaceActionAuthority : IWorkspaceActionAuthority, IDisposable
{
    private readonly WorkspaceDomain _workspace;
    private readonly object _gate = new();

    // Live state — mutated only under _gate and only inside the folder-changed
    // handler (which is invoked synchronously by Workspace.SetProjectFromPath).
    private WorkspaceIdentity _liveIdentity;
    private WorkspaceGeneration _liveGeneration;
    private string _liveCanonicalRoot = string.Empty;
    private ulong _liveDevice;
    private ulong _liveInode;

    private const int StatBufferSize = 256;
    private bool _disposed;

    public event Action? ScopeInvalidated;

    public WorkspaceActionAuthority(WorkspaceDomain workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _liveIdentity = default;
        _liveGeneration = default;

        // If a workspace is already open when the authority is created,
        // capture its state (the event won't fire retroactively).
        if (!string.IsNullOrWhiteSpace(_workspace.WorkspacePath))
        {
            RefreshFromCurrentPath();
        }

        _workspace.WorkspaceFolderChanged += OnWorkspaceFolderChanged;
    }

    /// <inheritdoc/>
    public bool TryCaptureCurrentScope(out WorkspaceActionScope scope)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                scope = null!;
                return false;
            }

            if (_liveIdentity == default || _liveGeneration == default)
            {
                scope = null!;
                return false;
            }

            var rootPath = _workspace.WorkspacePath;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                scope = null!;
                return false;
            }

            scope = new WorkspaceActionScope(
                _liveIdentity,
                _liveGeneration,
                rootPath,
                _liveCanonicalRoot,
                _liveDevice,
                _liveInode);
            return true;
        }
    }

    /// <inheritdoc/>
    public bool IsCurrent(WorkspaceActionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            var rootPath = _workspace.WorkspacePath;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            // Re-resolve canonical root and device/inode from live filesystem
            // so that root symlink retargeting and root directory replacement
            // are detected even without a generation change.
            if (!TryRealpath(rootPath, out var liveCanonicalRoot))
            {
                return false;
            }

            if (!TryStatDeviceInode(liveCanonicalRoot, out var liveDevice, out var liveInode))
            {
                return false;
            }

            var liveIdentity = ComputeIdentity(liveCanonicalRoot);

            return liveIdentity == scope.Identity
                && _liveGeneration == scope.Generation
                && string.Equals(liveCanonicalRoot, scope.CapturedCanonicalRoot, StringComparison.Ordinal)
                && liveDevice == scope.CapturedRootDevice
                && liveInode == scope.CapturedRootInode;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _workspace.WorkspaceFolderChanged -= OnWorkspaceFolderChanged;
    }

    // ------------------------------------------------------------------
    // Event handler — invoked synchronously by Workspace.SetProjectFromPath
    // ------------------------------------------------------------------

    private void OnWorkspaceFolderChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            RefreshFromCurrentPath();
        }

        ScopeInvalidated?.Invoke();
    }

    /// <summary>
    /// Re-reads the current workspace path and updates every live field.
    /// Must be called under <see cref="_gate"/>.
    /// </summary>
    private void RefreshFromCurrentPath()
    {
        var rootPath = _workspace.WorkspacePath;

        // Advance generation on every transition, including close.
        _liveGeneration = _liveGeneration == default
            ? WorkspaceGeneration.Initial
            : _liveGeneration.Next();

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            // Workspace closed — identity resets, no scope can be captured.
            _liveIdentity = default;
            _liveCanonicalRoot = string.Empty;
            _liveDevice = 0;
            _liveInode = 0;
            return;
        }

        // Fail closed: relative paths (including "." and "src") cannot
        // be used as a workspace root for action capture.  Clear state
        // so TryCaptureCurrentScope returns false.
        if (!System.IO.Path.IsPathRooted(rootPath))
        {
            _liveIdentity = default;
            _liveCanonicalRoot = string.Empty;
            _liveDevice = 0;
            _liveInode = 0;
            return;
        }

        // Fail closed: unresolvable paths prevent capture.
        if (!TryRealpath(rootPath, out var canonicalRoot))
        {
            _liveIdentity = default;
            _liveCanonicalRoot = string.Empty;
            _liveDevice = 0;
            _liveInode = 0;
            return;
        }

        if (!TryStatDeviceInode(canonicalRoot, out var device, out var inode))
        {
            _liveIdentity = default;
            _liveCanonicalRoot = string.Empty;
            _liveDevice = 0;
            _liveInode = 0;
            return;
        }

        _liveIdentity = ComputeIdentity(canonicalRoot);
        _liveCanonicalRoot = canonicalRoot;
        _liveDevice = device;
        _liveInode = inode;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Derives a stable workspace identity from a canonical absolute path.
    /// The same folder path always produces the same identity across the
    /// application lifetime.
    /// </summary>
    private static WorkspaceIdentity ComputeIdentity(string canonicalPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        var suffix = Convert.ToHexString(hash).ToLowerInvariant()[..16];
        return WorkspaceIdentity.FromValue($"workspace:{suffix}");
    }

    // ------------------------------------------------------------------
    // Linux realpath + stat support (same approach as WorkspaceFileReader)
    // ------------------------------------------------------------------

    private static bool TryRealpath(string path, out string resolved)
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
        }
        finally
        {
            Free(pointer);
        }

        return resolved.Length > 0;
    }

    private static bool TryStatDeviceInode(
        string path,
        out ulong device,
        out ulong inode)
    {
        device = 0;
        inode = 0;
        var buffer = new byte[StatBufferSize];
        int result;
        try
        {
            result = Stat(path, buffer);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        if (result != 0)
        {
            return false;
        }

        // Linux x86-64 struct stat layout:
        //   st_dev  at offset 0  (8 bytes, unsigned long)
        //   st_ino  at offset 8  (8 bytes, unsigned long)
        device = BitConverter.ToUInt64(buffer, 0);
        inode = BitConverter.ToUInt64(buffer, 8);
        return true;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "realpath")]
    private static extern IntPtr Realpath(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr resolved);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int Stat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        byte[] statBuffer);
}
