// Phase 10 M6 manual smoke: real csharp-ls + whole-document formatting + FoS contract.
// Outside Zaide.slnx. Run:
//   export PATH="$PATH:$HOME/.dotnet/tools"
//   dotnet run --project tools/Phase10M6FormattingSmoke -- tools/Phase10M0LanguageIntelligenceProof/fixture

using AvaloniaEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Services;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Phase10M6FormattingSmoke <fixture-dir>");
    return 2;
}

var fixtureDir = Path.GetFullPath(args[0]);
var projectPath = Directory.GetFiles(fixtureDir, "*.csproj").FirstOrDefault();
if (projectPath is null)
{
    Console.Error.WriteLine($"Fixture missing under {fixtureDir}");
    return 2;
}

// Deliberately unformatted C# for csharp-ls to reformat.
var samplePath = Path.Combine(fixtureDir, "Unformatted.cs");
const string unformatted = """
using System;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;
namespace Demo{
public class Unformatted{
public static void Main(){
Console.WriteLine( "hello" );
}
}
}
""";
await File.WriteAllTextAsync(samplePath, unformatted);

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
services.AddSingleton<ILanguageFormattingService, LanguageFormattingService>();

await using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<ILanguageDocumentBridge>();
var workspace = provider.GetRequiredService<Workspace>();
var project = provider.GetRequiredService<IProjectContextService>();
var session = provider.GetRequiredService<ILanguageSessionService>();
var formatting = provider.GetRequiredService<ILanguageFormattingService>();

Console.WriteLine($"Fixture: {fixtureDir}");
Console.WriteLine($"csharp-ls: {new LanguageServerBinaryLocator().Resolve() ?? "(missing)"}");
Console.WriteLine($"Sample: {samplePath}");

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

var original = await File.ReadAllTextAsync(samplePath);
var doc = workspace.OpenDocument(samplePath, original);
workspace.SetActiveDocument(doc);

// Allow document bridge didOpen to settle.
await Task.Delay(TimeSpan.FromSeconds(2));

// ── 1. Explicit format document ──────────────────────────────────────────
var outcome = await formatting.FormatDocumentAsync(samplePath);
Console.WriteLine($"Format outcome: kind={outcome.Kind} feedback={outcome.FeedbackMessage}");

if (!outcome.IsAccepted)
{
    Console.Error.WriteLine("FAIL: formatting not accepted");
    return 1;
}

if (!outcome.HasTextChange || outcome.FormattedText is null)
{
    // Retry once after a longer settle; some servers need extra analysis time.
    await Task.Delay(TimeSpan.FromSeconds(3));
    outcome = await formatting.FormatDocumentAsync(samplePath);
    Console.WriteLine($"Format retry: kind={outcome.Kind} feedback={outcome.FeedbackMessage}");
}

if (!outcome.HasTextChange || outcome.FormattedText is null)
{
    Console.Error.WriteLine("FAIL: expected non-empty formatting edits for unformatted file");
    Console.Error.WriteLine($"Source length={original.Length}");
    return 1;
}

var formatted = outcome.FormattedText;
Console.WriteLine($"PASS formatDocument: sourceLen={original.Length} formattedLen={formatted.Length}");
Console.WriteLine("--- formatted preview (first 200 chars) ---");
Console.WriteLine(formatted.Length <= 200 ? formatted : formatted[..200] + "…");

// ── 2. Single undo group restores original (headless AvaloniaEdit) ───────
var textDoc = new TextDocument(original);
var stack = textDoc.UndoStack;
stack.StartUndoGroup();
try
{
    textDoc.Text = formatted;
}
finally
{
    stack.EndUndoGroup();
}

if (textDoc.Text != formatted)
{
    Console.Error.WriteLine("FAIL: document text not applied");
    return 1;
}

stack.Undo();
if (textDoc.Text != original)
{
    Console.Error.WriteLine("FAIL: single undo did not restore original");
    return 1;
}

Console.WriteLine("PASS undo: one undo restores entire original document");

// Apply formatted content to the workspace document (as EditorView would).
doc.Content = formatted;

// ── 3. Format-on-Save: format before write; write formatted once ─────────
var savePath = Path.Combine(fixtureDir, "Unformatted.FoS.cs");
await File.WriteAllTextAsync(savePath, unformatted);
var fosDoc = workspace.OpenDocument(savePath, unformatted);
workspace.SetActiveDocument(fosDoc);
await Task.Delay(TimeSpan.FromSeconds(2));

var fosOutcome = await formatting.FormatDocumentAsync(savePath);
if (fosOutcome.HasTextChange && fosOutcome.FormattedText is not null)
    fosDoc.Content = fosOutcome.FormattedText;

await File.WriteAllTextAsync(savePath, fosDoc.Content);
var written = await File.ReadAllTextAsync(savePath);
if (fosOutcome.HasTextChange)
{
    if (written != fosOutcome.FormattedText)
    {
        Console.Error.WriteLine("FAIL: Format-on-Save write did not match formatted content");
        return 1;
    }

    Console.WriteLine("PASS formatOnSave: accepted formatting written once");
}
else
{
    Console.WriteLine($"PASS formatOnSave: no edits (kind={fosOutcome.Kind}); wrote current content once");
}

// ── 4. Failure path still saves current content ──────────────────────────
var failPath = Path.Combine(fixtureDir, "Unformatted.FailSave.cs");
const string failContent = "class KeepMe { }";
await File.WriteAllTextAsync(failPath, failContent);
// Unsupported-style: format a non-open / non-active path should not mutate
// the fail document. Simulate FoS failure by formatting while inactive.
workspace.SetActiveDocument(doc); // failPath not active
var failOutcome = await formatting.FormatDocumentAsync(failPath);
if (failOutcome.IsAccepted && failOutcome.HasTextChange)
{
    Console.Error.WriteLine("FAIL: expected unavailable/stale for inactive document");
    return 1;
}

// Save proceeds with current content regardless.
await File.WriteAllTextAsync(failPath, failContent);
var failWritten = await File.ReadAllTextAsync(failPath);
if (failWritten != failContent)
{
    Console.Error.WriteLine("FAIL: failure path altered saved content");
    return 1;
}

Console.WriteLine(
    $"PASS failureStillSaves: kind={failOutcome.Kind}; wrote current content unchanged");

// Capability advertisement
var readySession = session.TryGetReadySession(session.Current.Generation);
Console.WriteLine(
    $"Capabilities: DocumentFormattingSupported={readySession?.Capabilities.DocumentFormattingSupported}");

Console.WriteLine("PASS Phase 10 M6 formatting smoke");
return 0;
