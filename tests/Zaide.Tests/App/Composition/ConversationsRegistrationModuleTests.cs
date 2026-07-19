using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;

namespace Zaide.Tests.App.Composition;

public sealed class ConversationsRegistrationModuleTests
{
    private static string ReadRepoFile(string relativePath)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Zaide.slnx).");
    }

    [Fact]
    public void AddZaideConversations_RegistersExactlyTwoPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideConversations();

        Assert.Same(services, returned);
        Assert.Equal(2, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IActorCatalog)
                && d.ImplementationType == typeof(ActorCatalog));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IConversationStore)
                && d.ImplementationType == typeof(ConversationStore));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesConversationStoreAsSingleton()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        var store1 = provider.GetRequiredService<IConversationStore>();
        var store2 = provider.GetRequiredService<IConversationStore>();
        Assert.Same(store1, store2);
        Assert.IsType<ConversationStore>(store1);
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesActorCatalogAsSingleton()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        var catalog1 = provider.GetRequiredService<IActorCatalog>();
        var catalog2 = provider.GetRequiredService<IActorCatalog>();
        Assert.Same(catalog1, catalog2);
        Assert.IsType<ActorCatalog>(catalog1);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideConversationsOnce_AfterAppCoreBeforeAgents()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideConversations\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var conversationsIndex = programSource.IndexOf("AddZaideConversations()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        var townhallIndex = programSource.IndexOf("AddZaideTownhall()", StringComparison.Ordinal);

        Assert.True(appCoreIndex >= 0);
        Assert.True(conversationsIndex > appCoreIndex);
        Assert.True(agentsIndex > conversationsIndex);
        Assert.True(townhallIndex > agentsIndex);

        Assert.DoesNotContain("AddSingleton<IActorCatalog", programSource);
    }

    [Fact]
    public void ConversationsModuleSource_ContainsExactlyTheTwoPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class ConversationsServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideConversations", moduleSource);
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IActorCatalog,\s*ActorCatalog>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IConversationStore,\s*ConversationStore>\(\)"));
    }
}
