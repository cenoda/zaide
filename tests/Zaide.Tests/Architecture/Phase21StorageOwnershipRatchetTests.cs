using System.Linq;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Conversations.Infrastructure;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Phase 21 M1 storage ownership ratchet.
/// </summary>
public sealed class Phase21StorageOwnershipRatchetTests
{
    [Fact]
    public void DurableRecordStore_IsAgentsInfrastructureOwned()
    {
        Assert.Equal(
            "Zaide.Features.Agents.Infrastructure.Transparency.Storage",
            typeof(AgentDurableRecordFileStore).Namespace);
    }

    [Fact]
    public void DurableRecordPaths_AreIsolatedFromConversationPersistence()
    {
        var durableRoot = AgentDurableRecordPathResolver.GetRootDirectory();
        var conversationPath = ConversationStorePathResolver.GetStorePath();

        Assert.NotEqual(conversationPath, durableRoot);
        Assert.Contains("agents-durable", durableRoot, System.StringComparison.Ordinal);
        Assert.DoesNotContain("conversations.json", durableRoot, System.StringComparison.Ordinal);
    }

    [Fact]
    public void DurableRecordStore_DoesNotReferenceConversationStore()
    {
        var referencesConversationStore = typeof(AgentDurableRecordFileStore)
            .Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith(
                "Zaide.Features.Agents.Infrastructure.Transparency",
                System.StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public))
            .Any(f => f.FieldType.FullName?.Contains(
                "ConversationStore",
                System.StringComparison.Ordinal) == true);

        Assert.False(referencesConversationStore);
    }
}
