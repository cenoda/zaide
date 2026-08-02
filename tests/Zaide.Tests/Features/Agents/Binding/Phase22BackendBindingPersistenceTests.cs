using System;
using System.IO;
using System.Text.Json;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Binding;

public sealed class Phase22BackendBindingPersistenceTests
{
    [Fact]
    public void MissingFile_StartsEmpty()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        Assert.Equal(AgentActorBackendBindingLoadState.Empty, store.LoadResult.State);
        Assert.False(store.HasBinding(ActorId.TownhallAgent));
        Assert.False(File.Exists(dir.PrimaryPath));
    }

    [Fact]
    public void AtomicWrite_CreatesPrimaryAndLastKnownGood()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        var actorId = ActorId.TownhallAgent;

        Assert.True(store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);

        Assert.True(File.Exists(dir.PrimaryPath));
        Assert.False(File.Exists(dir.TempPath));

        // Second mutation should create LKG from previous primary.
        Assert.True(store.TryUpdate(
            actorId,
            new AgentActorBackendBinding(actorId, AgentBackendIds.NativeHarness),
            expectedRevision: 1).IsSuccess);
        Assert.True(File.Exists(dir.LastKnownGoodPath));
        Assert.False(File.Exists(dir.TempPath));

        var reloaded = CreateStore(dir);
        Assert.Equal(AgentActorBackendBindingLoadState.Loaded, reloaded.LoadResult.State);
        Assert.True(reloaded.TryGetBinding(actorId, out var binding));
        Assert.Equal(2, binding.Revision);
        Assert.Equal(AgentBackendIds.NativeHarness, binding.BackendId);
    }

    [Fact]
    public void LeftoverTemp_IsIgnored_OnLoad()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        var actorId = ActorId.TownhallAgent;
        Assert.True(store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);

        File.WriteAllText(dir.TempPath, """{"schemaVersion":1,"bindings":[]}""");
        Assert.True(File.Exists(dir.TempPath));

        var reloaded = CreateStore(dir);
        Assert.True(reloaded.HasBinding(actorId));
        Assert.Equal(1, reloaded.GetRevision(actorId));
    }

    [Fact]
    public void CorruptPrimary_RecoversFromLastKnownGood()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        var actorId = ActorId.TownhallAgent;
        Assert.True(store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);
        Assert.True(store.TryUpdate(
            actorId,
            new AgentActorBackendBinding(actorId, AgentBackendIds.NativeHarness),
            expectedRevision: 1).IsSuccess);

        File.WriteAllText(dir.PrimaryPath, "{ not-json");
        var reloaded = CreateStore(dir);
        Assert.Equal(
            AgentActorBackendBindingLoadState.RecoveredFromLastKnownGood,
            reloaded.LoadResult.State);
        Assert.True(reloaded.HasBinding(actorId));
    }

    [Fact]
    public void CorruptPrimaryWithoutLkg_StartsUnboundWithRecoveryError()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(dir.PrimaryPath, "{ broken");
        var store = CreateStore(dir);
        Assert.Equal(
            AgentActorBackendBindingLoadState.UnboundWithRecoveryError,
            store.LoadResult.State);
        Assert.True(store.LoadResult.HasRecoveryError);
        Assert.False(store.HasBinding(ActorId.TownhallAgent));
        // Fail closed: do not rewrite/delete the corrupt primary.
        Assert.True(File.Exists(dir.PrimaryPath));
    }

    [Fact]
    public void UnknownSchema_FailsClosed_WithoutRewrite()
    {
        using var dir = TempDir.Create();
        var original = """
            {
              "schemaVersion": 99,
              "bindings": []
            }
            """;
        File.WriteAllText(dir.PrimaryPath, original);

        var store = CreateStore(dir);
        Assert.Equal(AgentActorBackendBindingLoadState.UnsupportedSchema, store.LoadResult.State);
        Assert.False(store.HasBinding(ActorId.TownhallAgent));
        Assert.Equal(original.Trim(), File.ReadAllText(dir.PrimaryPath).Trim());
    }

    [Fact]
    public void Reload_RehydratesDurableFieldsOnly_NoAuthZombies()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity(
            "/usr/bin/fake-agent",
            new[] { "arg1", "arg2" },
            registryId: "reg",
            distributionProvenance: "fixture");

        Assert.True(store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.Acp,
            runtime,
            "acp-fake-agent",
            "1.0.0")).IsSuccess);

        store.SetRuntimeAuthentication(
            actorId,
            "oauth",
            AgentAuthenticationConnectionState.Authenticated);

        var reloaded = CreateStore(dir);
        Assert.True(reloaded.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentBackendIds.Acp, binding.BackendId);
        Assert.Equal("/usr/bin/fake-agent", binding.AcpRuntime!.ExecutablePath);
        Assert.Equal(new[] { "arg1", "arg2" }, binding.AcpRuntime.Arguments);
        Assert.Equal("acp-fake-agent", binding.ExpectedAgentName);
        Assert.Equal("1.0.0", binding.ExpectedAgentVersion);
        Assert.Equal("reg", binding.AcpRuntime.RegistryId);
        Assert.Null(binding.SelectedAuthMethodId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, binding.AuthenticationState);
    }

    [Fact]
    public void Unbind_SurvivesReload()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        var actorId = ActorId.TownhallAgent;
        Assert.True(store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);
        Assert.True(store.TryUnbind(actorId, expectedRevision: 1).IsSuccess);

        var reloaded = CreateStore(dir);
        Assert.False(reloaded.HasBinding(actorId));
    }

    [Fact]
    public void BindingDocument_NeverContainsSecrets()
    {
        using var dir = TempDir.Create();
        var store = CreateStore(dir);
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity(
            "/usr/bin/fake-agent",
            new[] { "--token-name", "public-label" });

        Assert.True(store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.Acp,
            runtime,
            "acp-fake-agent",
            "1.0.0")).IsSuccess);

        store.SetRuntimeAuthentication(
            actorId,
            "sk-live-secret-value",
            AgentAuthenticationConnectionState.Authenticated);

        var json = File.ReadAllText(dir.PrimaryPath);
        Assert.DoesNotContain("sk-live-secret-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedAuthMethodId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("authenticationState", json, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authenticated", json, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("bindings", out var bindings));
        Assert.Equal(JsonValueKind.Array, bindings.ValueKind);
    }

    [Fact]
    public void PathResolver_UsesSettingsDirectory()
    {
        Assert.EndsWith(
            AgentActorBackendBindingPathResolver.PrimaryFileName,
            AgentActorBackendBindingPathResolver.GetPrimaryPath(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            AgentActorBackendBindingPathResolver.TempFileName,
            AgentActorBackendBindingPathResolver.GetTempPath(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            AgentActorBackendBindingPathResolver.LastKnownGoodFileName,
            AgentActorBackendBindingPathResolver.GetLastKnownGoodPath(),
            StringComparison.Ordinal);
    }

    private static AgentActorBackendBindingStore CreateStore(TempDir dir) =>
        new(dir.PrimaryPath, dir.TempPath, dir.LastKnownGoodPath);

    private sealed class TempDir : IDisposable
    {
        private TempDir(string root)
        {
            Root = root;
            PrimaryPath = Path.Combine(root, AgentActorBackendBindingPathResolver.PrimaryFileName);
            TempPath = Path.Combine(root, AgentActorBackendBindingPathResolver.TempFileName);
            LastKnownGoodPath = Path.Combine(root, AgentActorBackendBindingPathResolver.LastKnownGoodFileName);
        }

        public string Root { get; }

        public string PrimaryPath { get; }

        public string TempPath { get; }

        public string LastKnownGoodPath { get; }

        public static TempDir Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-binding-persist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempDir(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
