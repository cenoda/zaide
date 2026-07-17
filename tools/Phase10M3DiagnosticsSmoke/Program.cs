// Phase 10 M3 manual smoke: real csharp-ls + production diagnostics pipeline.
// Outside Zaide.slnx. Run:
//   export PATH="$PATH:$HOME/.dotnet/tools"
//   dotnet run --project tools/Phase10M3DiagnosticsSmoke -- /path/to/fixture

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Infrastructure;

using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;
if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Phase10M3DiagnosticsSmoke <fixture-dir>");
    return 2;
}

var fixtureDir = Path.GetFullPath(args[0]);
var projectPath = Path.Combine(fixtureDir, "Smoke.csproj");
var brokenPath = Path.Combine(fixtureDir, "Broken.cs");
if (!File.Exists(projectPath) || !File.Exists(brokenPath))
{
    Console.Error.WriteLine($"Fixture missing under {fixtureDir}");
    return 2;
}

var brokenText = await File.ReadAllTextAsync(brokenPath);
var fixedText = brokenText.Replace("return 1  // missing semicolon — deliberate CS1002", "return 1; // fixed");

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

await using var provider = services.BuildServiceProvider();
var workspace = provider.GetRequiredService<Workspace>();
var project = provider.GetRequiredService<IProjectContextService>();
var session = provider.GetRequiredService<ILanguageSessionService>();
var bridge = provider.GetRequiredService<ILanguageDocumentBridge>();
var diagnostics = provider.GetRequiredService<ILanguageDiagnosticsService>();

Console.WriteLine($"Fixture: {fixtureDir}");
Console.WriteLine($"csharp-ls: {new LanguageServerBinaryLocator().Resolve() ?? "(missing)"}");

await project.LoadAsync(fixtureDir);
var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
while (DateTime.UtcNow < deadline &&
       session.Current.State is not LanguageSessionState.Ready and not LanguageSessionState.Failed)
{
    await Task.Delay(100);
}

Console.WriteLine($"Session state={session.Current.State} gen={session.Current.Generation} failure={session.Current.Failure?.Message}");
if (session.Current.State != LanguageSessionState.Ready)
{
    Console.Error.WriteLine("FAIL: session did not become Ready");
    return 1;
}

workspace.OpenDocument(brokenPath, brokenText);

// Wait for non-empty diagnostics on Broken.cs
deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
LanguageDiagnosticsSnapshot? withErrors = null;
while (DateTime.UtcNow < deadline)
{
    var snap = diagnostics.Current;
    if (snap.State == LanguageSessionState.Ready &&
        snap.Diagnostics.Any(d => d.FilePath.EndsWith("Broken.cs", StringComparison.OrdinalIgnoreCase)))
    {
        withErrors = snap;
        break;
    }

    await Task.Delay(150);
}

if (withErrors is null)
{
    Console.Error.WriteLine("FAIL: no diagnostics received for Broken.cs");
    return 1;
}

Console.WriteLine($"PASS publish: count={withErrors.Diagnostics.Count}");
foreach (var d in withErrors.Diagnostics.Take(5))
    Console.WriteLine($"  [{d.Severity}] {d.Code}: {d.Message} @ {Path.GetFileName(d.FilePath)}:{d.Range.StartLine + 1}:{d.Range.StartCharacter + 1}");

// Fix content through Document.Content (same path as editor edits).
var doc = workspace.Documents.First(d => string.Equals(d.FilePath, brokenPath, StringComparison.Ordinal));
doc.Content = fixedText;

deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
var cleared = false;
while (DateTime.UtcNow < deadline)
{
    var snap = diagnostics.Current;
    if (snap.State == LanguageSessionState.Ready &&
        !snap.Diagnostics.Any(d => d.FilePath.EndsWith("Broken.cs", StringComparison.OrdinalIgnoreCase) &&
                                   d.Severity == LanguageDiagnosticSeverity.Error))
    {
        // Allow residual non-errors; require no error diagnostics for Broken.cs
        if (!snap.Diagnostics.Any(d =>
                d.FilePath.EndsWith("Broken.cs", StringComparison.OrdinalIgnoreCase) &&
                d.Severity == LanguageDiagnosticSeverity.Error))
        {
            cleared = true;
            Console.WriteLine($"PASS clear: remaining diagnostics={snap.Diagnostics.Count} (errors on Broken.cs=0)");
            break;
        }
    }

    await Task.Delay(150);
}

if (!cleared)
{
    Console.WriteLine("Current diagnostics after fix:");
    foreach (var d in diagnostics.Current.Diagnostics)
        Console.WriteLine($"  [{d.Severity}] {d.Code}: {d.Message}");
    Console.Error.WriteLine("FAIL: error diagnostics did not clear after fix");
    return 1;
}

// Teardown
diagnostics.Dispose();
bridge.Dispose();
session.Dispose();
project.Dispose();

Console.WriteLine("PASS Phase 10 M3 diagnostics smoke");
return 0;
