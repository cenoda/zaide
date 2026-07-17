using System.Linq;
using Avalonia;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Views;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Settings.Presentation;

public sealed class InstalledFontCatalogTests
{
    static InstalledFontCatalogTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        EnsureApplication();
    }

    [Fact]
    public void ExtractPrimaryFamilyName_TakesFirstCommaSeparatedEntry()
    {
        Assert.Equal("Cascadia Code", InstalledFontCatalog.ExtractPrimaryFamilyName(
            "Cascadia Code, Consolas, monospace"));
        Assert.Equal("Georgia", InstalledFontCatalog.ExtractPrimaryFamilyName("Georgia, serif"));
        Assert.Equal("Fira Code", InstalledFontCatalog.ExtractPrimaryFamilyName("Fira Code"));
    }

    [Fact]
    public void BuildEntries_IncludesInstalledFontsSortedAlphabetically()
    {
        var entries = InstalledFontCatalog.BuildEntries(null);
        var names = entries.Select(entry => entry.Name).ToList();

        Assert.NotEmpty(names);
        Assert.Equal(names.OrderBy(name => name, System.StringComparer.OrdinalIgnoreCase), names);
        Assert.All(entries, entry => Assert.True(entry.IsAvailable));
    }

    [Fact]
    public void BuildEntries_PrependsUnavailablePersistedFamily()
    {
        const string missing = "Zaide Missing Font 9f3c2a1b";
        var entries = InstalledFontCatalog.BuildEntries(missing);

        Assert.Equal(missing, entries[0].Name);
        Assert.False(entries[0].IsAvailable);
        Assert.Contains("(unavailable)", entries[0].DisplayText);
        Assert.DoesNotContain(entries.Skip(1), entry => entry.Name == missing);
    }

    [Fact]
    public void ResolvePreviewFontFamily_FallsBackForUnavailableFamily()
    {
        var preview = InstalledFontCatalog.ResolvePreviewFontFamily(
            "Zaide Missing Font 9f3c2a1b",
            isAvailable: false);

        Assert.Equal("sans-serif", preview.Name);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
                app.Initialize();
            return;
        }

        new App().Initialize();
    }
}
