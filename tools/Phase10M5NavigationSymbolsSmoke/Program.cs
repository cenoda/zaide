// Phase 10 M5 manual smoke: real csharp-ls + definition/document/workspace symbols.
// Outside Zaide.slnx. Run:
//   export PATH="$PATH:$HOME/.dotnet/tools"
//   dotnet run --project tools/Phase10M5NavigationSymbolsSmoke -- tools/Phase10M0LanguageIntelligenceProof/fixture

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Phase10M5NavigationSymbolsSmoke <fixture-dir>");
    return 2;
}

var fixtureDir = Path.GetFullPath(args[0]);
var projectPath = Directory.GetFiles(fixtureDir, "*.csproj").FirstOrDefault();
var samplePath = Path.Combine(fixtureDir, "Sample.cs");
if (projectPath is null || !File.Exists(samplePath))
{
    Console.Error.WriteLine($"Fixture missing under {fixtureDir}");
    return 2;
}

var sampleText = await File.ReadAllTextAsync(samplePath);
var greetOffset = sampleText.IndexOf("Greet(\"world\")", StringComparison.Ordinal);
if (greetOffset < 0)
    greetOffset = sampleText.IndexOf("Greet", StringComparison.Ordinal);
if (greetOffset < 0)
{
    Console.Error.WriteLine("FAIL: Greet call site not found in Sample.cs");
    return 1;
}

var caretOffset = greetOffset + "Greet".Length / 2;

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddSingleton<Workspace>();
services.AddSingleton<IProjectFileSystem, FileSystemProjectFileSystem>();
services.AddSingleton<IProjectDiscovery, ProjectDiscovery>();
services.AddSingleton<IProjectContextService, ProjectContextService>();
services.AddSingleton<ILanguageServerBinaryLocator, LanguageServerBinaryLocator>();
services.AddSingleton<ILanguageServerSessionFactory, CsharpLsSessionFactory>();
services.AddSingleton<ILanguageSessionService, LanguageSessionService>();
services.AddSingleton<ILanguageDocumentBridge, LanguageDocumentBridge>();
services.AddSingleton<ILanguageNavigationService, LanguageNavigationService>();
services.AddSingleton<ILanguageSymbolService, LanguageSymbolService>();

await using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<ILanguageDocumentBridge>();
var workspace = provider.GetRequiredService<Workspace>();
var project = provider.GetRequiredService<IProjectContextService>();
var session = provider.GetRequiredService<ILanguageSessionService>();
var navigation = provider.GetRequiredService<ILanguageNavigationService>();
var symbols = provider.GetRequiredService<ILanguageSymbolService>();

Console.WriteLine($"Fixture: {fixtureDir}");
Console.WriteLine($"csharp-ls: {new LanguageServerBinaryLocator().Resolve() ?? "(missing)"}");

await project.LoadAsync(fixtureDir);

var context = project.Current;
if (context.State == ProjectContextState.Ambiguous && context.Candidates.Count > 0)
{
    var preferred = context.Candidates.FirstOrDefault(c =>
                         string.Equals(Path.GetExtension(c.FilePath), ".csproj", StringComparison.OrdinalIgnoreCase))
                    ?? context.Candidates[0];
    project.SelectProject(preferred);
    await Task.Delay(500);
}

var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
while (DateTime.UtcNow < deadline &&
       session.Current.State is not LanguageSessionState.Ready and not LanguageSessionState.Failed)
{
    await Task.Delay(100);
}

Console.WriteLine(
    $"Session state={session.Current.State} gen={session.Current.Generation} failure={session.Current.Failure?.Message}");
if (session.Current.State != LanguageSessionState.Ready)
{
    Console.Error.WriteLine("FAIL: session did not become Ready");
    return 1;
}

workspace.OpenDocument(samplePath, sampleText);
workspace.SetActiveDocument(workspace.Documents.First(d =>
    string.Equals(d.FilePath, samplePath, StringComparison.Ordinal)));

// Allow document bridge didOpen to settle.
await Task.Delay(TimeSpan.FromSeconds(2));

// ── 1. Go to Definition ──────────────────────────────────────────────────
navigation.RequestDefinition(samplePath, caretOffset);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
while (DateTime.UtcNow < deadline &&
       navigation.Current.State is not LanguageNavigationState.Ready
           and not LanguageNavigationState.Choose
           and not LanguageNavigationState.Empty
           and not LanguageNavigationState.Failed
           and not LanguageNavigationState.Unavailable
           and not LanguageNavigationState.Idle)
{
    await Task.Delay(100);
}

// Single Ready may be auto-consumed to Idle by a host; capture via Take if present.
var defLocation = navigation.TryTakeSingleLocation();
if (defLocation is null && navigation.Current.State == LanguageNavigationState.Choose)
    defLocation = navigation.Current.Locations.FirstOrDefault();

if (defLocation is null)
{
    // Retry once: request again and wait for Ready without host auto-take.
    navigation.RequestDefinition(samplePath, caretOffset);
    deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
    LanguageNavigationSnapshot? readySnap = null;
    while (DateTime.UtcNow < deadline)
    {
        if (navigation.Current.IsSingleNavigateReady || navigation.Current.IsChooserOpen)
        {
            readySnap = navigation.Current;
            break;
        }

        if (navigation.Current.State is LanguageNavigationState.Empty
            or LanguageNavigationState.Failed
            or LanguageNavigationState.Unavailable)
        {
            break;
        }

        await Task.Delay(50);
    }

    if (readySnap is null || readySnap.Locations.Count == 0)
    {
        Console.Error.WriteLine($"FAIL definition: state={navigation.Current.State}");
        return 1;
    }

    defLocation = readySnap.Locations[0];
}

Console.WriteLine(
    $"PASS definition: file={Path.GetFileName(defLocation.FilePath)} " +
    $"line={defLocation.Range.StartLine + 1} char={defLocation.Range.StartCharacter}");

// ── 2. Document symbols ──────────────────────────────────────────────────
symbols.RequestDocumentSymbols(samplePath);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
while (DateTime.UtcNow < deadline &&
       symbols.Current.State is not LanguageSymbolState.Ready
           and not LanguageSymbolState.Empty
           and not LanguageSymbolState.Failed
           and not LanguageSymbolState.Unavailable)
{
    await Task.Delay(100);
}

if (symbols.Current.State != LanguageSymbolState.Ready || symbols.Current.Symbols.Count == 0)
{
    Console.Error.WriteLine(
        $"FAIL documentSymbol: state={symbols.Current.State} count={symbols.Current.Symbols.Count}");
    return 1;
}

Console.WriteLine(
    $"PASS documentSymbol: count={symbols.Current.Symbols.Count} first={symbols.Current.Symbols[0].Name}");

var docSymLocation = symbols.TryAcceptSelected();
if (docSymLocation is null || string.IsNullOrWhiteSpace(docSymLocation.FilePath))
{
    Console.Error.WriteLine("FAIL documentSymbol: select navigation location missing");
    return 1;
}

Console.WriteLine(
    $"PASS documentSymbol navigate: {Path.GetFileName(docSymLocation.FilePath)}:" +
    $"{docSymLocation.Range.StartLine + 1}");

// ── 3. Workspace symbols ─────────────────────────────────────────────────
symbols.RequestWorkspaceSymbols("Greet");
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
while (DateTime.UtcNow < deadline &&
       symbols.Current.State is not LanguageSymbolState.Ready
           and not LanguageSymbolState.Empty
           and not LanguageSymbolState.Failed
           and not LanguageSymbolState.Unavailable)
{
    await Task.Delay(100);
}

if (symbols.Current.State != LanguageSymbolState.Ready || symbols.Current.Symbols.Count == 0)
{
    Console.Error.WriteLine(
        $"FAIL workspaceSymbol: state={symbols.Current.State} count={symbols.Current.Symbols.Count}");
    return 1;
}

Console.WriteLine(
    $"PASS workspaceSymbol: count={symbols.Current.Symbols.Count} first={symbols.Current.Symbols[0].Name}");

var wsLocation = symbols.TryAcceptSelected();
if (wsLocation is null || string.IsNullOrWhiteSpace(wsLocation.FilePath))
{
    Console.Error.WriteLine("FAIL workspaceSymbol: select navigation location missing");
    return 1;
}

Console.WriteLine(
    $"PASS workspaceSymbol navigate: {Path.GetFileName(wsLocation.FilePath)}:" +
    $"{wsLocation.Range.StartLine + 1}");

// ── 4. Stale/cancelled after tab switch must not yield a navigable result ─
workspace.OpenDocument(samplePath, sampleText);
workspace.SetActiveDocument(workspace.Documents.First(d =>
    string.Equals(d.FilePath, samplePath, StringComparison.Ordinal)));
await Task.Delay(500);

navigation.RequestDefinition(samplePath, caretOffset);
await Task.Delay(30);

// Switch active document while definition is in flight / about to complete.
var otherPath = Path.Combine(fixtureDir, "Other.cs");
const string otherContent = "namespace Fixture { class Other { int Field; } }";
await File.WriteAllTextAsync(otherPath, otherContent);
var otherDoc = workspace.OpenDocument(otherPath, otherContent);
workspace.SetActiveDocument(otherDoc);

await Task.Delay(TimeSpan.FromSeconds(2));

var staleSingle = navigation.TryTakeSingleLocation();
var staleAccept = navigation.TryAcceptSelected();
if (staleSingle is not null || staleAccept is not null)
{
    Console.Error.WriteLine("FAIL stale: definition result accepted after tab switch");
    return 1;
}

if (navigation.Current.IsSingleNavigateReady || navigation.Current.IsChooserOpen)
{
    Console.Error.WriteLine("FAIL stale: definition surface still open after tab switch");
    return 1;
}

Console.WriteLine("PASS stale: cancelled/stale definition after tab switch did not navigate");
Console.WriteLine("PASS Phase 10 M5 definition/symbols smoke");
return 0;
