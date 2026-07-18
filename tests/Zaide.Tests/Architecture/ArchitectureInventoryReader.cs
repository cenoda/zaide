using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Zaide.App.Composition;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Deterministic hybrid inventory reader for Refactor 6.1 architecture tests.
/// Source scans cover placement, provider sites, forbidden-namespace locations,
/// and root-folder admission evidence. Compiled metadata covers top-level
/// public/internal type counts using the M0 counting rule.
/// </summary>
public sealed class ArchitectureInventoryReader
{
    /// <summary>M0 baseline: non-nested, non-compiler-generated top-level types (M1: 395).</summary>
    public const int M0TotalTopLevelTypes = 395;

    /// <summary>M0 baseline public top-level type count.</summary>
    public const int M0PublicTopLevelTypes = 348;

    /// <summary>M0 baseline internal top-level type count (M1: 47).</summary>
    public const int M0InternalTopLevelTypes = 47;

    private static readonly Regex NamespaceDeclarationRegex = new(
        @"^\s*namespace\s+([A-Za-z_][\w.]*)\s*[;{]?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GetRequiredServiceRegex = new(
        @"\.GetRequiredService(?:<|\()",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GetServiceRegex = new(
        @"\.GetService(?:<|\()",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IServiceProviderRegex = new(
        @"\bIServiceProvider\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AppServicesRegex = new(
        @"\bApp\.Services\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ServicesToViewModelsRegex = new(
        @"(?:using\s+Zaide\.ViewModels\b|Zaide\.ViewModels\.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ModelsToServicesRegex = new(
        @"(?:using\s+Zaide\.Services\b|Zaide\.Services\.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Residual R61-V02 edge after SourceControl move: Domain session bag still
    /// depends on Application snapshot types (Refactor 6.3 inversion target).
    /// </summary>
    private static readonly Regex SourceControlStateToApplicationRegex = new(
        @"(?:using\s+Zaide\.Features\.SourceControl\.Application\b|Zaide\.Features\.SourceControl\.Application\.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Residual R61-V07 edge after SourceControl move: application diff-tab
    /// service still depends on editor presentation types (Refactor 6.3).
    /// </summary>
    private static readonly Regex SourceControlDiffTabToEditorPresentationRegex = new(
        @"(?:using\s+Zaide\.Features\.Editor\.Presentation\b|Zaide\.Features\.Editor\.Presentation\.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Residual R61-V05 edge after Terminal move: session factory contract/impl
    /// still depend on presentation session types (Refactor 6.3 inversion).
    /// </summary>
    private static readonly Regex TerminalFactoryToPresentationRegex = new(
        @"(?:using\s+Zaide\.Features\.Terminal\.Presentation\b|Zaide\.Features\.Terminal\.Presentation\.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Residual R61-V06 edge after Agents move: MentionParser still depends on
    /// panel-host presentation state (Refactor 6.3 inversion).
    /// </summary>
    private static readonly Regex MentionParserToPresentationRegex = new(
        @"(?:using\s+Zaide\.Features\.Agents\.Presentation\b|Zaide\.Features\.Agents\.Presentation\.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string SourceControlStateRelativePath =
        "src/Features/SourceControl/Domain/SourceControlState.cs";

    private const string SourceControlDiffTabServiceRelativePath =
        "src/Features/SourceControl/Application/SourceControlDiffTabService.cs";

    private const string TerminalSessionFactoryContractRelativePath =
        "src/Features/Terminal/Contracts/ITerminalSessionFactory.cs";

    private const string TerminalSessionFactoryRelativePath =
        "src/Features/Terminal/Application/TerminalSessionFactory.cs";

    private const string MentionParserRelativePath =
        "src/Features/Agents/Application/MentionParser.cs";

    private readonly string _repositoryRoot;
    private readonly Assembly _productionAssembly;

    public ArchitectureInventoryReader()
        : this(ResolveRepositoryRoot(), typeof(global::Zaide.App.Composition.App).Assembly)
    {
    }

    public ArchitectureInventoryReader(string repositoryRoot, Assembly productionAssembly)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _productionAssembly = productionAssembly
            ?? throw new ArgumentNullException(nameof(productionAssembly));
    }

    public string RepositoryRoot => _repositoryRoot;

    public ArchitectureInventory Read()
    {
        var trackedSourcePaths = ListTrackedProductionSourceFiles();
        var sourceFiles = ReadSourceFileEntries(trackedSourcePaths);
        var types = ReadCompiledTypeEntries();
        var providerEvidence = ReadProviderEvidence(trackedSourcePaths);
        var namespaceDependencyEvidence = ReadNamespaceDependencyEvidence(trackedSourcePaths);
        var rootFolderAdmissionEvidence = ReadRootFolderAdmissionEvidence(sourceFiles);
        var findings = BuildFindings(
            sourceFiles,
            types,
            providerEvidence,
            namespaceDependencyEvidence,
            rootFolderAdmissionEvidence);

        return new ArchitectureInventory(
            _repositoryRoot,
            sourceFiles,
            types,
            providerEvidence,
            namespaceDependencyEvidence,
            rootFolderAdmissionEvidence,
            findings);
    }

    public static string ResolveRepositoryRoot()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = start; current is not null; current = current.Parent)
        {
            var solutionPath = Path.Combine(current.FullName, "Zaide.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }
        }

        var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var current = cwd; current is not null; current = current.Parent)
        {
            var solutionPath = Path.Combine(current.FullName, "Zaide.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not locate repository root containing Zaide.slnx from test base directory or CWD.");
    }

    private IReadOnlyList<string> ListTrackedProductionSourceFiles()
    {
        // Match M0: git-tracked production C# only.
        var output = RunGit("ls-files", "--", "src/*.cs", "src/**/*.cs");
        var paths = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeRelativePath)
            .Where(p => p.StartsWith("src/", StringComparison.Ordinal)
                && p.EndsWith(".cs", StringComparison.Ordinal)
                && !p.Contains("/bin/", StringComparison.Ordinal)
                && !p.Contains("/obj/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (paths.Length == 0)
        {
            throw new InvalidOperationException(
                "git ls-files returned no production C# paths under src/.");
        }

        return paths;
    }

    private IReadOnlyList<ProductionSourceFileEntry> ReadSourceFileEntries(
        IReadOnlyList<string> relativePaths)
    {
        var entries = new List<ProductionSourceFileEntry>(relativePaths.Count);
        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.Combine(_repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Tracked production source file is missing on disk: {relativePath}",
                    fullPath);
            }

            var text = File.ReadAllText(fullPath);
            var declaredNamespace = ExtractDeclaredNamespace(text)
                ?? throw new InvalidOperationException(
                    $"Production source file has no namespace declaration: {relativePath}");

            entries.Add(new ProductionSourceFileEntry(
                relativePath,
                GetTechnicalFolder(relativePath),
                declaredNamespace));
        }

        return entries;
    }

    private IReadOnlyList<ProductionTypeEntry> ReadCompiledTypeEntries()
    {
        Type[] loadedTypes;
        try
        {
            loadedTypes = _productionAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            loadedTypes = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        return loadedTypes
            .Where(IsBaselineProductionType)
            .Select(t => new ProductionTypeEntry(
                t.FullName ?? t.Name,
                t.Namespace!,
                t.IsPublic))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsBaselineProductionType(Type type)
    {
        if (type.IsNested)
        {
            return false;
        }

        if (type.Namespace is null)
        {
            return false;
        }

        if (type.Namespace != "Zaide" && !type.Namespace.StartsWith("Zaide.", StringComparison.Ordinal))
        {
            return false;
        }

        if (type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
        {
            return false;
        }

        // M0 counting rule: public via IsPublic, internal via IsNotPublic.
        return type.IsPublic || type.IsNotPublic;
    }

    private IReadOnlyList<ProviderEvidenceEntry> ReadProviderEvidence(
        IReadOnlyList<string> relativePaths)
    {
        var entries = new List<ProviderEvidenceEntry>();
        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.Combine(_repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var lines = File.ReadAllLines(fullPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                AddProviderMatches(
                    entries,
                    relativePath,
                    lineNumber,
                    line,
                    GetRequiredServiceRegex,
                    ProviderEvidenceEntry.KindGetRequiredService);
                AddProviderMatches(
                    entries,
                    relativePath,
                    lineNumber,
                    line,
                    GetServiceRegex,
                    ProviderEvidenceEntry.KindGetService);
                AddProviderMatches(
                    entries,
                    relativePath,
                    lineNumber,
                    line,
                    IServiceProviderRegex,
                    ProviderEvidenceEntry.KindIServiceProvider);
                AddProviderMatches(
                    entries,
                    relativePath,
                    lineNumber,
                    line,
                    AppServicesRegex,
                    ProviderEvidenceEntry.KindAppServices);
            }
        }

        return entries
            .OrderBy(e => e.RelativePath, StringComparer.Ordinal)
            .ThenBy(e => e.Line)
            .ThenBy(e => e.Kind, StringComparer.Ordinal)
            .ThenBy(e => e.MatchedText, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddProviderMatches(
        List<ProviderEvidenceEntry> entries,
        string relativePath,
        int lineNumber,
        string line,
        Regex regex,
        string kind)
    {
        foreach (Match match in regex.Matches(line))
        {
            entries.Add(new ProviderEvidenceEntry(
                relativePath,
                lineNumber,
                kind,
                match.Value));
        }
    }

    private IReadOnlyList<NamespaceDependencyEvidenceEntry> ReadNamespaceDependencyEvidence(
        IReadOnlyList<string> relativePaths)
    {
        var entries = new List<NamespaceDependencyEvidenceEntry>();
        foreach (var relativePath in relativePaths)
        {
            var normalizedPath = NormalizeRelativePath(relativePath);
            var technicalFolder = GetTechnicalFolder(normalizedPath);
            Regex? targetRegex = technicalFolder switch
            {
                "Services" => ServicesToViewModelsRegex,
                "Models" => ModelsToServicesRegex,
                _ => null
            };

            string? targetFragment = technicalFolder switch
            {
                "Services" => "Zaide.ViewModels",
                "Models" => "Zaide.Services",
                _ => null
            };

            // Refactor 6.2 M8–M9: residual allowlist edges under Features.
            // Path-scoped so other Features layer edges are not ratcheted.
            if (normalizedPath.Equals(SourceControlStateRelativePath, StringComparison.Ordinal))
            {
                technicalFolder = "Features";
                targetRegex = SourceControlStateToApplicationRegex;
                targetFragment = "Zaide.Features.SourceControl.Application";
            }
            else if (normalizedPath.Equals(SourceControlDiffTabServiceRelativePath, StringComparison.Ordinal))
            {
                technicalFolder = "Features";
                targetRegex = SourceControlDiffTabToEditorPresentationRegex;
                targetFragment = "Zaide.Features.Editor.Presentation";
            }
            else if (normalizedPath.Equals(TerminalSessionFactoryContractRelativePath, StringComparison.Ordinal)
                || normalizedPath.Equals(TerminalSessionFactoryRelativePath, StringComparison.Ordinal))
            {
                technicalFolder = "Features";
                targetRegex = TerminalFactoryToPresentationRegex;
                targetFragment = "Zaide.Features.Terminal.Presentation";
            }
            else if (normalizedPath.Equals(MentionParserRelativePath, StringComparison.Ordinal))
            {
                technicalFolder = "Features";
                targetRegex = MentionParserToPresentationRegex;
                targetFragment = "Zaide.Features.Agents.Presentation";
            }

            if (targetRegex is null || targetFragment is null)
            {
                continue;
            }

            var fullPath = Path.Combine(_repositoryRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
            var lines = File.ReadAllLines(fullPath);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in targetRegex.Matches(lines[i]))
                {
                    entries.Add(new NamespaceDependencyEvidenceEntry(
                        normalizedPath,
                        i + 1,
                        technicalFolder,
                        targetFragment,
                        match.Value));
                }
            }
        }

        return entries
            .OrderBy(e => e.RelativePath, StringComparer.Ordinal)
            .ThenBy(e => e.Line)
            .ThenBy(e => e.TargetNamespaceFragment, StringComparer.Ordinal)
            .ThenBy(e => e.MatchedText, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<RootFolderAdmissionEvidenceEntry> ReadRootFolderAdmissionEvidence(
        IReadOnlyList<ProductionSourceFileEntry> sourceFiles)
    {
        return sourceFiles
            .Select(f =>
            {
                var isSrcRoot = f.TechnicalFolder == "src";
                var underInfrastructure = f.RelativePath.StartsWith(
                    "src/Infrastructure/",
                    StringComparison.Ordinal);
                var underUiShared = f.RelativePath.StartsWith(
                    "src/UI/Shared/",
                    StringComparison.Ordinal);

                return new RootFolderAdmissionEvidenceEntry(
                    f.RelativePath,
                    f.TechnicalFolder,
                    isSrcRoot,
                    underInfrastructure,
                    underUiShared);
            })
            .OrderBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ArchitectureFinding> BuildFindings(
        IReadOnlyList<ProductionSourceFileEntry> sourceFiles,
        IReadOnlyList<ProductionTypeEntry> types,
        IReadOnlyList<ProviderEvidenceEntry> providerEvidence,
        IReadOnlyList<NamespaceDependencyEvidenceEntry> namespaceDependencyEvidence,
        IReadOnlyList<RootFolderAdmissionEvidenceEntry> rootFolderAdmissionEvidence)
    {
        var findings = new List<ArchitectureFinding>();

        foreach (var source in sourceFiles)
        {
            findings.Add(new ArchitectureFinding(
                kind: "ProductionSourceFile",
                stableKey: $"source:{source.RelativePath}",
                relativePath: source.RelativePath,
                sourceSymbol: source.DeclaredNamespace,
                evidence: $"folder={source.TechnicalFolder};namespace={source.DeclaredNamespace}"));
        }

        foreach (var type in types)
        {
            findings.Add(new ArchitectureFinding(
                kind: "ProductionType",
                stableKey: $"type:{type.Visibility}:{type.FullName}",
                sourceSymbol: type.FullName,
                evidence: $"namespace={type.Namespace};visibility={type.Visibility}"));
        }

        foreach (var provider in providerEvidence)
        {
            findings.Add(new ArchitectureFinding(
                kind: "ProviderEvidence",
                stableKey: $"provider:{provider.Kind}:{provider.RelativePath}:{provider.Line}:{provider.MatchedText}",
                relativePath: provider.RelativePath,
                line: provider.Line,
                evidence: provider.MatchedText));
        }

        foreach (var dependency in namespaceDependencyEvidence)
        {
            findings.Add(new ArchitectureFinding(
                kind: "NamespaceDependencyEvidence",
                stableKey:
                $"dependency:{dependency.SourceTechnicalFolder}->{dependency.TargetNamespaceFragment}:{dependency.RelativePath}:{dependency.Line}:{dependency.MatchedText}",
                relativePath: dependency.RelativePath,
                line: dependency.Line,
                sourceSymbol: dependency.SourceTechnicalFolder,
                targetSymbol: dependency.TargetNamespaceFragment,
                evidence: dependency.MatchedText));
        }

        foreach (var admission in rootFolderAdmissionEvidence)
        {
            findings.Add(new ArchitectureFinding(
                kind: "RootFolderAdmissionEvidence",
                stableKey: $"admission:{admission.RelativePath}",
                relativePath: admission.RelativePath,
                evidence:
                $"folder={admission.TechnicalFolder};srcRoot={admission.IsSrcRootCompositionFile};infrastructure={admission.IsUnderRootInfrastructure};uiShared={admission.IsUnderUiShared}"));
        }

        return findings
            .OrderBy(f => f.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ExtractDeclaredNamespace(string sourceText)
    {
        using var reader = new StringReader(sourceText);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var match = NamespaceDeclarationRegex.Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string GetTechnicalFolder(string relativePath)
    {
        // src/Program.cs -> src
        // src/Services/Foo.cs -> Services
        var parts = relativePath.Split('/');
        if (parts.Length == 2 && parts[0] == "src")
        {
            return "src";
        }

        if (parts.Length >= 3 && parts[0] == "src")
        {
            return parts[1];
        }

        throw new InvalidOperationException($"Unexpected production source path: {relativePath}");
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').Trim();

    private string RunGit(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed with exit code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }
}
