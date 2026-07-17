// Phase 10 M4 manual smoke: real csharp-ls + production completion/hover pipeline.
// Outside Zaide.slnx. Run:
//   export PATH="$PATH:$HOME/.dotnet/tools"
//   dotnet run --project tools/Phase10M4CompletionHoverSmoke -- /path/to/fixture

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Phase10M4CompletionHoverSmoke <fixture-dir>");
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

var caretOffset = greetOffset + "Greet".Length;

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
services.AddSingleton<ILanguageCompletionService, LanguageCompletionService>();
services.AddSingleton<ILanguageHoverService, LanguageHoverService>();

await using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<ILanguageDocumentBridge>();
var workspace = provider.GetRequiredService<Workspace>();
var project = provider.GetRequiredService<IProjectContextService>();
var session = provider.GetRequiredService<ILanguageSessionService>();
var completion = provider.GetRequiredService<ILanguageCompletionService>();
var hover = provider.GetRequiredService<ILanguageHoverService>();

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

completion.RequestExplicit(samplePath, caretOffset);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
while (DateTime.UtcNow < deadline &&
       completion.Current.State != LanguageCompletionState.Ready)
{
    await Task.Delay(100);
}

if (completion.Current.State != LanguageCompletionState.Ready ||
    completion.Current.Items.Count == 0)
{
    Console.Error.WriteLine(
        $"FAIL completion: state={completion.Current.State} items={completion.Current.Items.Count}");
    return 1;
}

Console.WriteLine(
    $"PASS completion: items={completion.Current.Items.Count} first={completion.Current.Items[0].Label}");

hover.Schedule(samplePath, caretOffset);
await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay + TimeSpan.FromMilliseconds(200));
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
while (DateTime.UtcNow < deadline && !hover.Current.IsVisible)
{
    await Task.Delay(100);
}

if (!hover.Current.IsVisible)
{
    Console.Error.WriteLine($"FAIL hover: state={hover.Current.State}");
    return 1;
}

Console.WriteLine($"PASS hover: {Truncate(hover.Current.Content ?? "", 120)}");

// Stale discard: request completion on Sample.cs, switch active document, dismiss — other doc must stay unchanged.
var otherPath = Path.Combine(fixtureDir, "Other.cs");
const string otherContent = "namespace Fixture { class Other { int Field; } }";
await File.WriteAllTextAsync(otherPath, otherContent);
workspace.OpenDocument(otherPath, otherContent);
workspace.SetActiveDocument(workspace.Documents.First(d =>
    string.Equals(d.FilePath, otherPath, StringComparison.Ordinal)));

completion.RequestExplicit(samplePath, caretOffset);
await Task.Delay(50);
completion.Dismiss();

var staleVisible = completion.Current.IsPopupOpen;
var otherDoc = workspace.Documents.First(d =>
    string.Equals(d.FilePath, otherPath, StringComparison.Ordinal));
var otherTextBefore = otherDoc.Content;

completion.RequestExplicit(otherPath, otherContent.IndexOf("Field", StringComparison.Ordinal));
await Task.Delay(TimeSpan.FromSeconds(2));

if (staleVisible)
{
    Console.Error.WriteLine("FAIL stale: popup remained open after dismiss/tab context change");
    return 1;
}

if (!string.Equals(otherTextBefore, otherDoc.Content, StringComparison.Ordinal))
{
    Console.Error.WriteLine("FAIL stale: inactive document text mutated");
    return 1;
}

Console.WriteLine("PASS stale: dismissed completion did not mutate inactive/other editor text");
Console.WriteLine("PASS Phase 10 M4 completion/hover smoke");
return 0;

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";
