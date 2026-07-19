using System;
using System.IO;
using System.Reflection;
using Xunit;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 7 M7: executable ownership ratchet for final Townhall mirror/projection
/// boundaries after legacy string-protocol removal.
/// </summary>
public sealed class Refactor7M7OwnershipRatchetTests
{
    private static readonly string[] ForbiddenMirrorCoordinatorDisplayPrefixes =
    [
        "$\"Assistant:",
        "$\"Routing failed:",
        "$\"Error:",
        "\"Assistant: \"",
        "\"Routing failed: \"",
        "\"Error: \"",
    ];

    [Fact]
    public void TownhallViewModel_AddMirroredActivity_RemainsAbsent()
    {
        var method = typeof(TownhallViewModel).GetMethod(
            "AddMirroredActivity",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Null(method);
    }

    [Fact]
    public void TownhallEntryProjection_ClassifyTownhallMirror_RemainsAbsent()
    {
        var method = typeof(TownhallEntryProjection).GetMethod(
            "ClassifyTownhallMirror",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Null(method);
    }

    [Fact]
    public void AgentTownhallMirrorCoordinator_DoesNotFormatTownhallDisplayPrefixes()
    {
        var source = ReadProductionSource("src/App/Shell/AgentTownhallMirrorCoordinator.cs");

        foreach (var forbidden in ForbiddenMirrorCoordinatorDisplayPrefixes)
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TownhallEntryProjection_ToTownhallDisplayContent_OwnsFrozenCompatibilityPrefixes()
    {
        var method = typeof(TownhallEntryProjection).GetMethod(
            nameof(TownhallEntryProjection.ToTownhallDisplayContent),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(method);

        Assert.Equal(
            "Assistant: reply",
            InvokeDisplay(ConversationEntryKind.AssistantResponse, "reply"));
        Assert.Equal(
            "Routing failed: unknown",
            InvokeDisplay(ConversationEntryKind.RoutingFailure, "unknown"));
        Assert.Equal(
            "Error: boom",
            InvokeDisplay(ConversationEntryKind.ExecutionFailure, "boom"));
    }

    private static string InvokeDisplay(ConversationEntryKind kind, string content)
    {
        var entry = TownhallEntryProjection.CreateTypedEntry(
            kind,
            ActorId.HumanUser,
            DateTimeOffset.UtcNow,
            content);

        return TownhallEntryProjection.ToTownhallDisplayContent(entry);
    }

    private static string ReadProductionSource(string relativePath)
    {
        var repoRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();
        return File.ReadAllText(Path.Combine(repoRoot, relativePath));
    }
}
