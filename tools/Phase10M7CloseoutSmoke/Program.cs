// Phase 10 M7 closeout smoke: one coherent Linux session across M1–M6 surfaces.
// Outside Zaide.slnx. Run:
//   export PATH="$PATH:$HOME/.dotnet/tools"
//   dotnet run --project tools/Phase10M7CloseoutSmoke -- tools/Phase10M0LanguageIntelligenceProof/fixture

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Phase10M7CloseoutSmoke <fixture-dir>");
    return 2;
}

var fixtureDir = Path.GetFullPath(args[0]);
var samplePath = Path.Combine(fixtureDir, "Sample.cs");
if (!Directory.GetFiles(fixtureDir, "*.csproj").Any() || !File.Exists(samplePath))
{
    Console.Error.WriteLine($"Fixture missing under {fixtureDir}");
    return 2;
}

var sampleText = await File.ReadAllTextAsync(samplePath);
var greetOffset = sampleText.IndexOf("Greet", StringComparison.Ordinal);
if (greetOffset < 0)
{
    Console.Error.WriteLine("FAIL: Greet symbol not found in Sample.cs");
    return 1;
}

var brokenPath = Path.Combine(fixtureDir, "M7Broken.cs");
const string brokenSource = """
using System;
public static class M7Broken
{
    public static int Broken() => 1  // deliberate CS1002
}
""";
const string fixedSource = """
using System;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;
public static class M7Broken
{
    public static int Broken() => 1;
}
""";

await File.WriteAllTextAsync(brokenPath, brokenSource);

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
services.AddSingleton<ILanguageDiagnosticsService, LanguageDiagnosticsService>();
services.AddSingleton<ILanguageCompletionService, LanguageCompletionService>();
services.AddSingleton<ILanguageHoverService, LanguageHoverService>();
services.AddSingleton<ILanguageNavigationService, LanguageNavigationService>();
services.AddSingleton<ILanguageSymbolService, LanguageSymbolService>();
services.AddSingleton<ILanguageFormattingService, LanguageFormattingService>();

await using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<ILanguageDocumentBridge>();
var workspace = provider.GetRequiredService<Workspace>();
var project = provider.GetRequiredService<IProjectContextService>();
var session = provider.GetRequiredService<ILanguageSessionService>();
var diagnostics = provider.GetRequiredService<ILanguageDiagnosticsService>();
var completion = provider.GetRequiredService<ILanguageCompletionService>();
var hover = provider.GetRequiredService<ILanguageHoverService>();
var navigation = provider.GetRequiredService<ILanguageNavigationService>();
var symbols = provider.GetRequiredService<ILanguageSymbolService>();
var formatting = provider.GetRequiredService<ILanguageFormattingService>();

Console.WriteLine($"Fixture: {fixtureDir}");
Console.WriteLine($"csharp-ls: {new LanguageServerBinaryLocator().Resolve() ?? "(missing)"}");
Console.WriteLine($"Host: Linux");
Console.WriteLine($"Date: {DateTime.UtcNow:yyyy-MM-dd}");

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

var snap = session.Current;
Console.WriteLine(
    $"Session state={snap.State} gen={snap.Generation} statusBar=\"{LanguageSessionStatusPolicy.MapStatusBarText(snap)}\"");
if (snap.State != LanguageSessionState.Ready)
{
    Console.Error.WriteLine($"FAIL: session did not become Ready ({snap.Failure?.Kind})");
    return 1;
}

var ready = session.TryGetReadySession(snap.Generation)!;
var caps = ready.Capabilities;
Console.WriteLine(
    $"Capabilities: completion={caps.CompletionSupported} hover={caps.HoverSupported} " +
    $"definition={caps.DefinitionSupported} docSym={caps.DocumentSymbolSupported} " +
    $"wsSym={caps.WorkspaceSymbolSupported} format={caps.DocumentFormattingSupported}");

// ── Diagnostics publish + clear ───────────────────────────────────────────
workspace.OpenDocument(brokenPath, brokenSource);
workspace.SetActiveDocument(workspace.Documents.First(d =>
    string.Equals(d.FilePath, brokenPath, StringComparison.Ordinal)));
await Task.Delay(TimeSpan.FromSeconds(3));

var diagSnap = diagnostics.Current;
var published = diagSnap.Diagnostics.Count;
Console.WriteLine($"PASS diagnostics publish: count={published}");
if (published < 1)
{
    Console.Error.WriteLine("FAIL: expected at least one diagnostic");
    return 1;
}

var doc = workspace.ActiveDocument!;
doc.Content = fixedSource;
await Task.Delay(TimeSpan.FromSeconds(3));
var cleared = diagnostics.Current.Diagnostics.Count(d =>
    string.Equals(d.FilePath, brokenPath, StringComparison.Ordinal));
Console.WriteLine($"PASS diagnostics clear: remainingOnBroken={cleared}");

// ── Completion + hover (Sample.cs) ────────────────────────────────────────
workspace.OpenDocument(samplePath, sampleText);
workspace.SetActiveDocument(workspace.Documents.First(d =>
    string.Equals(d.FilePath, samplePath, StringComparison.Ordinal)));
await Task.Delay(TimeSpan.FromSeconds(2));

completion.RequestExplicit(samplePath, greetOffset + 2);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
while (DateTime.UtcNow < deadline &&
       completion.Current.State is not LanguageCompletionState.Ready
           and not LanguageCompletionState.Empty
           and not LanguageCompletionState.Unavailable
           and not LanguageCompletionState.Failed)
{
    await Task.Delay(50);
}

var comp = completion.Current;
Console.WriteLine($"PASS completion: state={comp.State} items={comp.Items.Count}");

hover.Schedule(samplePath, greetOffset + 2);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
while (DateTime.UtcNow < deadline && !hover.Current.IsVisible)
    await Task.Delay(50);
Console.WriteLine($"PASS hover: visible={hover.Current.IsVisible} len={hover.Current.Content?.Length ?? 0}");

// ── Definition + symbols ──────────────────────────────────────────────────
navigation.RequestDefinition(samplePath, greetOffset + 2);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
while (DateTime.UtcNow < deadline &&
       navigation.Current.State is not LanguageNavigationState.Ready
           and not LanguageNavigationState.Empty
           and not LanguageNavigationState.Unavailable
           and not LanguageNavigationState.Failed)
{
    await Task.Delay(50);
}
Console.WriteLine(
    $"PASS definition: state={navigation.Current.State} locations={navigation.Current.Locations.Count}");

symbols.RequestDocumentSymbols(samplePath);
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
while (DateTime.UtcNow < deadline &&
       symbols.Current.State is not LanguageSymbolState.Ready
           and not LanguageSymbolState.Empty
           and not LanguageSymbolState.Unavailable
           and not LanguageSymbolState.Failed)
{
    await Task.Delay(50);
}
Console.WriteLine($"PASS documentSymbol: count={symbols.Current.Symbols.Count}");

symbols.RequestWorkspaceSymbols("Greet");
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
while (DateTime.UtcNow < deadline &&
       symbols.Current.State is not LanguageSymbolState.Ready
           and not LanguageSymbolState.Empty
           and not LanguageSymbolState.Unavailable
           and not LanguageSymbolState.Failed)
{
    await Task.Delay(50);
}
Console.WriteLine($"PASS workspaceSymbol: count={symbols.Current.Symbols.Count}");

// ── Formatting + Format on Save contract ─────────────────────────────────
var unformattedPath = Path.Combine(fixtureDir, "Unformatted.cs");
var unformattedSource = "using System;namespace Demo{public class Unformatted{public static void Main(){Console.WriteLine(\"hello\");}}}";
await File.WriteAllTextAsync(unformattedPath, unformattedSource);
workspace.OpenDocument(unformattedPath, unformattedSource);
workspace.SetActiveDocument(workspace.Documents.First(d =>
    string.Equals(d.FilePath, unformattedPath, StringComparison.Ordinal)));
await Task.Delay(TimeSpan.FromSeconds(2));

var formatOutcome = await formatting.FormatDocumentAsync(unformattedPath, CancellationToken.None);
Console.WriteLine(
    $"PASS formatDocument: kind={formatOutcome.Kind} accepted={formatOutcome.IsAccepted} " +
    $"feedback={formatOutcome.FeedbackMessage}");
if (!formatOutcome.IsAccepted || formatOutcome.FormattedText is null)
{
    Console.Error.WriteLine("FAIL: formatting did not apply");
    return 1;
}

var fosDoc = workspace.Documents.First(d =>
    string.Equals(d.FilePath, unformattedPath, StringComparison.Ordinal));
fosDoc.Content = formatOutcome.FormattedText!;
await File.WriteAllTextAsync(unformattedPath, fosDoc.Content);
var written = await File.ReadAllTextAsync(unformattedPath);
if (written != formatOutcome.FormattedText)
{
    Console.Error.WriteLine("FAIL: Format-on-Save write did not match formatted content");
    return 1;
}

Console.WriteLine("PASS formatOnSave: accepted formatting written once");

Console.WriteLine(
    $"Command availability (active doc): completion={LanguageCommandAvailability.CanUseActiveDocumentFeature(session, samplePath, c => c.CompletionSupported)} " +
    $"definition={LanguageCommandAvailability.CanUseActiveDocumentFeature(session, samplePath, c => c.DefinitionSupported)} " +
    $"format={LanguageCommandAvailability.CanUseActiveDocumentFeature(session, unformattedPath, c => c.DocumentFormattingSupported)}");
Console.WriteLine(
    $"Command availability (workspace): wsSymbol={LanguageCommandAvailability.CanUseWorkspaceSymbols(session)}");

Console.WriteLine("PASS Phase 10 M7 closeout smoke");

try { File.Delete(brokenPath); } catch { /* best effort */ }

return 0;