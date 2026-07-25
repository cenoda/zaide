using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.App.Composition.Registration;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using WorkspaceDomain = Zaide.Features.Workspace.Domain.Workspace;

namespace Zaide.Tests.Features.Workspace.Infrastructure;

/// <summary>
/// Phase 17 M2 corrective pass #3 — direct tests for the production
/// <see cref="WorkspaceActionAuthority"/>: event-driven generation
/// advancement, thread-safe capture / IsCurrent, full live-state
/// validation (identity, generation, canonical root, device, inode),
/// and fail-closed behaviour for unresolvable paths.
/// </summary>
public sealed class Phase17WorkspaceAuthorityTests : IDisposable
{
    private readonly string _dirA;
    private readonly string _dirB;
    private readonly WorkspaceDomain _workspace;
    private readonly WorkspaceActionAuthority _authority;

    public Phase17WorkspaceAuthorityTests()
    {
        var baseDir = Path.Combine(
            Path.GetTempPath(),
            "zaide-p17-auth-" + Guid.NewGuid().ToString("N"));
        _dirA = Path.Combine(baseDir, "project-a");
        _dirB = Path.Combine(baseDir, "project-b");
        Directory.CreateDirectory(_dirA);
        Directory.CreateDirectory(_dirB);

        _workspace = new WorkspaceDomain();
        _authority = new WorkspaceActionAuthority(_workspace);
    }

    public void Dispose()
    {
        _authority.Dispose();

        try
        {
            var parent = Path.GetDirectoryName(_dirA);
            if (parent is not null && Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ----------------------------------------------------------------
    // No workspace
    // ----------------------------------------------------------------

    [Fact]
    public void TryCapture_NoWorkspace_ReturnsFalse()
    {
        // Workspace starts with no path (null WorkspacePath).
        var captured = _authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    // ----------------------------------------------------------------
    // First capture
    // ----------------------------------------------------------------

    [Fact]
    public void FirstCapture_AfterOpen_ReturnsScopeWithGeneration1()
    {
        _workspace.SetProjectFromPath(_dirA);

        var captured = _authority.TryCaptureCurrentScope(out var scope);
        Assert.True(captured);
        Assert.Equal(WorkspaceGeneration.Initial, scope.Generation);
        Assert.Equal(_dirA, scope.RootPath);
        Assert.True(scope.CapturedRootDevice > 0);
        Assert.True(scope.CapturedRootInode > 0);
    }

    // ----------------------------------------------------------------
    // Close
    // ----------------------------------------------------------------

    [Fact]
    public void TryCapture_AfterClose_ReturnsFalse()
    {
        _workspace.SetProjectFromPath(_dirA);
        _workspace.SetProjectFromPath(null); // close

        var captured = _authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    [Fact]
    public void Close_AdvancesGenerationAndClearsIdentity()
    {
        _workspace.SetProjectFromPath(_dirA);
        var first = _authority.TryCaptureCurrentScope(out var scopeA);
        Assert.True(first);

        _workspace.SetProjectFromPath(null); // close

        var second = _authority.TryCaptureCurrentScope(out _);
        Assert.False(second);

        // Verify the captured scope is no longer current.
        Assert.False(_authority.IsCurrent(scopeA));
    }

    // ----------------------------------------------------------------
    // Switch
    // ----------------------------------------------------------------

    [Fact]
    public void Switch_BumpsGeneration()
    {
        _workspace.SetProjectFromPath(_dirA);
        var capturedA = _authority.TryCaptureCurrentScope(out var scopeA);
        Assert.True(capturedA);

        _workspace.SetProjectFromPath(_dirB);
        var capturedB = _authority.TryCaptureCurrentScope(out var scopeB);
        Assert.True(capturedB);

        Assert.NotEqual(scopeA.Identity, scopeB.Identity);
        Assert.True(scopeB.Generation.Value > scopeA.Generation.Value);
        Assert.False(_authority.IsCurrent(scopeA));
        Assert.True(_authority.IsCurrent(scopeB));
    }

    // ----------------------------------------------------------------
    // A → close → A (same path reopen)
    // ----------------------------------------------------------------

    [Fact]
    public void CloseAndReopenSamePath_BumpsGeneration()
    {
        _workspace.SetProjectFromPath(_dirA);
        var captured1 = _authority.TryCaptureCurrentScope(out var scope1);
        Assert.True(captured1);
        Assert.Equal(WorkspaceGeneration.Initial, scope1.Generation);

        _workspace.SetProjectFromPath(null); // close
        _workspace.SetProjectFromPath(_dirA); // reopen same path

        var captured2 = _authority.TryCaptureCurrentScope(out var scope2);
        Assert.True(captured2);
        // Same identity (same canonical path), but different generation.
        Assert.Equal(scope1.Identity, scope2.Identity);
        Assert.NotEqual(scope1.Generation, scope2.Generation);
        Assert.True(scope2.Generation.Value > scope1.Generation.Value);

        // Old scope is stale.
        Assert.False(_authority.IsCurrent(scope1));
        Assert.True(_authority.IsCurrent(scope2));
    }

    // ----------------------------------------------------------------
    // A → B → A
    // ----------------------------------------------------------------

    [Fact]
    public void AThenBThenA_EachTransitionBumpsGeneration()
    {
        _workspace.SetProjectFromPath(_dirA);
        var cap1 = _authority.TryCaptureCurrentScope(out var s1);
        Assert.True(cap1);

        _workspace.SetProjectFromPath(_dirB);
        var cap2 = _authority.TryCaptureCurrentScope(out var s2);
        Assert.True(cap2);

        _workspace.SetProjectFromPath(_dirA);
        var cap3 = _authority.TryCaptureCurrentScope(out var s3);
        Assert.True(cap3);

        // Generations are strictly monotonic.
        Assert.True(s3.Generation.Value > s2.Generation.Value);
        Assert.True(s2.Generation.Value > s1.Generation.Value);

        // Identity for A is stable; identity for B is different.
        Assert.Equal(s1.Identity, s3.Identity);
        Assert.NotEqual(s1.Identity, s2.Identity);

        // Old scopes are stale.
        Assert.False(_authority.IsCurrent(s1));
        Assert.False(_authority.IsCurrent(s2));
        Assert.True(_authority.IsCurrent(s3));
    }

    // ----------------------------------------------------------------
    // Stale scope rejection
    // ----------------------------------------------------------------

    [Fact]
    public void IsCurrent_RejectsStaleGeneration()
    {
        _workspace.SetProjectFromPath(_dirA);
        var cap = _authority.TryCaptureCurrentScope(out var scope);
        Assert.True(cap);

        // Bump generation with any transition.
        _workspace.SetProjectFromPath(_dirB);

        Assert.False(_authority.IsCurrent(scope));
    }

    [Fact]
    public void IsCurrent_RejectsStaleIdentity()
    {
        _workspace.SetProjectFromPath(_dirA);
        var cap = _authority.TryCaptureCurrentScope(out var scope);
        Assert.True(cap);

        // Switch to a different project.
        _workspace.SetProjectFromPath(_dirB);

        Assert.False(_authority.IsCurrent(scope));
    }

    // ----------------------------------------------------------------
    // Root symlink retargeting detection
    // ----------------------------------------------------------------

    [Fact]
    public void IsCurrent_RejectsWhenRootSymlinkRetargeted()
    {
        _workspace.SetProjectFromPath(_dirA);
        var cap = _authority.TryCaptureCurrentScope(out var scope);
        Assert.True(cap);

        // Replace _dirA with a symlink to _dirB.
        Directory.Delete(_dirA, recursive: true);
        try
        {
            Directory.CreateSymbolicLink(_dirA, _dirB);

            // The workspace path text is unchanged, but the canonical
            // root has changed — IsCurrent must detect this.
            Assert.False(_authority.IsCurrent(scope));
        }
        finally
        {
            if (Directory.Exists(_dirA))
            {
                Directory.Delete(_dirA);
            }

            Directory.CreateDirectory(_dirA);
        }
    }

    // ----------------------------------------------------------------
    // Root directory replacement detection (stat-based)
    // ----------------------------------------------------------------

    [Fact]
    public void IsCurrent_RejectsWhenRootDirectoryReplaced()
    {
        _workspace.SetProjectFromPath(_dirA);
        var cap = _authority.TryCaptureCurrentScope(out var scope);
        Assert.True(cap);

        // Delete and recreate _dirA at the same path (new inode).
        Directory.Delete(_dirA, recursive: true);
        try
        {
            Directory.CreateDirectory(_dirA);

            // Same path, same canonical root, but different device/inode
            // (on most filesystems a new directory gets a new inode).
            Assert.False(_authority.IsCurrent(scope));
        }
        finally
        {
            if (Directory.Exists(_dirA))
            {
                Directory.Delete(_dirA, recursive: true);
            }

            Directory.CreateDirectory(_dirA);
        }
    }

    // ----------------------------------------------------------------
    // Canonical root validation in IsCurrent
    // ----------------------------------------------------------------

    [Fact]
    public void IsCurrent_RejectsWhenCanonicalRootDiffers()
    {
        _workspace.SetProjectFromPath(_dirA);
        var cap = _authority.TryCaptureCurrentScope(out var scope);
        Assert.True(cap);

        // Manually construct a scope with a wrong canonical root.
        StatDeviceInode(_dirA, out var dev, out var ino);
        var tampered = new WorkspaceActionScope(
            scope.Identity,
            scope.Generation,
            _dirA,
            capturedCanonicalRoot: _dirB, // wrong
            dev,
            ino);

        Assert.False(_authority.IsCurrent(tampered));
    }

    // ----------------------------------------------------------------
    // Fail closed for unresolvable paths
    // ----------------------------------------------------------------

    [Fact]
    public void TryCapture_FailsClosed_WhenPathIsRelative()
    {
        // Set a relative path directly on the workspace (bypasses
        // normal UI validation for testing purposes).
        _workspace.SetProjectFromPath("relative/path");

        var captured = _authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    [Fact]
    public void TryCapture_FailsClosed_WhenPathIsDotDirectory()
    {
        _workspace.SetProjectFromPath(".");

        var captured = _authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    [Fact]
    public void TryCapture_FailsClosed_WhenPathIsUnqualifiedName()
    {
        _workspace.SetProjectFromPath("src");

        var captured = _authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    [Fact]
    public void TryCapture_FailsClosed_WhenPathDoesNotExist()
    {
        var missing = Path.Combine(_dirA, "nonexistent");
        _workspace.SetProjectFromPath(missing);

        var captured = _authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    // ----------------------------------------------------------------
    // Thread safety: concurrent access does not corrupt state
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentCaptureAndIsCurrent_IsThreadSafe()
    {
        _workspace.SetProjectFromPath(_dirA);

        var barrier = new System.Threading.Barrier(2);
        Exception? error = null;

        var t1 = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 1000; i++)
                {
                    _ = _authority.TryCaptureCurrentScope(out _);
                }
            }
            catch (Exception e)
            {
                error = e;
            }
        });

        var t2 = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 1000; i++)
                {
                    _ = _authority.TryCaptureCurrentScope(out var s);
                    if (s is not null)
                    {
                        _ = _authority.IsCurrent(s);
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }
        });

        await Task.WhenAll(t1, t2);
        Assert.Null(error);
    }

    [Fact]
    public async Task ConcurrentCaptureAndFolderChange_DoesNotCorrupt()
    {
        _workspace.SetProjectFromPath(_dirA);

        var barrier = new System.Threading.Barrier(2);
        Exception? error = null;

        var t1 = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 500; i++)
                {
                    _ = _authority.TryCaptureCurrentScope(out _);
                }
            }
            catch (Exception e)
            {
                error = e;
            }
        });

        var t2 = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 500; i++)
                {
                    _workspace.SetProjectFromPath(i % 2 == 0 ? _dirA : _dirB);
                }
            }
            catch (Exception e)
            {
                error = e;
            }
        });

        await Task.WhenAll(t1, t2);
        Assert.Null(error);

        // Final state should be consistent.
        var captured = _authority.TryCaptureCurrentScope(out var final);
        Assert.True(captured);
        Assert.True(_authority.IsCurrent(final));
    }

    // ----------------------------------------------------------------
    // DI registration
    // ----------------------------------------------------------------

    [Fact]
    public void DI_RegistersAuthorityAsSingleton()
    {
        var services = new ServiceCollection();

        // Simulate the production registration path.
        services.AddSingleton<WorkspaceDomain>();
        services.AddSingleton<IWorkspaceActionAuthority, WorkspaceActionAuthority>();

        var provider = services.BuildServiceProvider();

        var a1 = provider.GetRequiredService<IWorkspaceActionAuthority>();
        var a2 = provider.GetRequiredService<IWorkspaceActionAuthority>();
        Assert.Same(a1, a2);
        Assert.IsType<WorkspaceActionAuthority>(a1);
    }

    [Fact]
    public void DI_RegistersViaModuleExtension()
    {
        var services = new ServiceCollection();
        services.AddSingleton<WorkspaceDomain>();
        services.AddZaideWorkspace();

        var provider = services.BuildServiceProvider();

        var authority = provider.GetService<IWorkspaceActionAuthority>();
        Assert.NotNull(authority);
        Assert.IsType<WorkspaceActionAuthority>(authority);
    }

    // ----------------------------------------------------------------
    // IDisposable clean-up
    // ----------------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesFromWorkspaceEvent()
    {
        var workspace = new WorkspaceDomain();
        var authority = new WorkspaceActionAuthority(workspace);

        authority.Dispose();

        // Opening a workspace after dispose should not throw and should
        // not affect the disposed authority.
        workspace.SetProjectFromPath(_dirA);
        var captured = authority.TryCaptureCurrentScope(out _);
        Assert.False(captured);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static void StatDeviceInode(string path, out ulong device, out ulong inode)
    {
        var buffer = new byte[256];
        var result = Stat(path, buffer);
        if (result != 0)
        {
            throw new InvalidOperationException($"stat failed for '{path}'.");
        }

        device = BitConverter.ToUInt64(buffer, 0);
        inode = BitConverter.ToUInt64(buffer, 8);
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int Stat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        byte[] statBuffer);
}
