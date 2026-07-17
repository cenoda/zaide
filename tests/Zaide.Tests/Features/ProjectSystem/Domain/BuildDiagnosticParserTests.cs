using System;
using System.IO;
using Xunit;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Language.Application;

namespace Zaide.Tests.Features.ProjectSystem.Domain;

/// <summary>
/// Phase 11 M3 tests for MSBuild / Roslyn CLI diagnostic parsing.
/// </summary>
public sealed class BuildDiagnosticParserTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-build-parser-" + Guid.NewGuid().ToString("N"));

    static BuildDiagnosticParserTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    [Fact]
    public void Parse_ErrorWithLineAndColumn_NormalizesAbsolutePath()
    {
        var file = Path.Combine(TempRoot, "Err.cs");
        var line =
            $"{file}(5,10): error CS1002: ; expected [{Path.Combine(TempRoot, "Err.csproj")}]";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Equal(Path.GetFullPath(file), diagnostic.FilePath);
        Assert.Equal(5, diagnostic.Line);
        Assert.Equal(10, diagnostic.Column);
        Assert.Equal(LanguageDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("CS1002", diagnostic.Code);
        Assert.Equal("; expected", diagnostic.Message);
        Assert.Equal(BuildDiagnosticSources.Build, diagnostic.Source);
    }

    [Fact]
    public void Parse_WarningWithLineOnly_DefaultsColumnToOne()
    {
        var file = Path.Combine(TempRoot, "Warn.cs");
        var line = $"{file}(12): warning CS1030: #warning directive";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Equal(12, diagnostic.Line);
        Assert.Equal(1, diagnostic.Column);
        Assert.Equal(LanguageDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("CS1030", diagnostic.Code);
    }

    [Fact]
    public void Parse_RelativePath_ResolvesAgainstWorkingDirectory()
    {
        var relative = "src/Program.cs";
        var line = $"{relative}(3,4): error CS0103: name does not exist";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Equal(Path.GetFullPath(Path.Combine(TempRoot, relative)), diagnostic.FilePath);
    }

    [Fact]
    public void Parse_JunkLines_AreIgnored()
    {
        var file = Path.Combine(TempRoot, "Only.cs");
        var good = $"{file}(1,2): error CS0001: bad";
        var junk = new[]
        {
            "Build FAILED.",
            "    0 Warning(s)",
            "Time Elapsed 00:00:01.23",
            good,
            "random noise",
        };

        var results = BuildDiagnosticParser.Parse(junk, TempRoot);

        Assert.Single(results);
    }

    [Fact]
    public void Parse_DuplicateSummaryLines_Deduplicates()
    {
        var file = Path.Combine(TempRoot, "Dup.cs");
        var line = $"{file}(2,3): error CS1002: ; expected";
        var results = BuildDiagnosticParser.Parse(new[] { line, line }, TempRoot);

        Assert.Single(results);
    }

    [Fact]
    public void Parse_DiagnosticWithoutCode_ParsesMessageOnly()
    {
        var file = Path.Combine(TempRoot, "NoCode.cs");
        var line = $"{file}(7,3): error some message without code";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Null(diagnostic.Code);
        Assert.Equal("some message without code", diagnostic.Message);
        Assert.Equal(LanguageDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Parse_DoneSeverity_MapsToInformation()
    {
        var file = Path.Combine(TempRoot, "Done.cs");
        var line = $"{file}(1,1): done some informational note";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Equal(LanguageDiagnosticSeverity.Information, diagnostic.Severity);
        Assert.Equal("some informational note", diagnostic.Message);
    }

    [Fact]
    public void Parse_MessageSeverity_MapsToHint()
    {
        var file = Path.Combine(TempRoot, "Msg.cs");
        var line = $"{file}(2,3): message MSB3123: some hint text";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Equal(LanguageDiagnosticSeverity.Hint, diagnostic.Severity);
        Assert.Equal("MSB3123", diagnostic.Code);
        Assert.Equal("some hint text", diagnostic.Message);
    }

    [Fact]
    public void Parse_SeverityKeyword_IsCaseInsensitive()
    {
        var file = Path.Combine(TempRoot, "Case.cs");
        var errorLine = $"{file}(1,1): ERROR CS0001: upper error";
        var warningLine = $"{file}(2,2): WARNING CS0002: upper warning";
        var doneLine = $"{file}(3,3): DONE upper done";
        var messageLine = $"{file}(4,4): MESSAGE upper message";

        var results = BuildDiagnosticParser.Parse(
            new[] { errorLine, warningLine, doneLine, messageLine }, TempRoot);

        Assert.Equal(4, results.Count);
        Assert.Equal(LanguageDiagnosticSeverity.Error, results[0].Severity);
        Assert.Equal(LanguageDiagnosticSeverity.Warning, results[1].Severity);
        Assert.Equal(LanguageDiagnosticSeverity.Information, results[2].Severity);
        Assert.Equal(LanguageDiagnosticSeverity.Hint, results[3].Severity);
    }

    [Fact]
    public void Parse_MixedSeverities_AllParsedAndSorted()
    {
        var file = Path.Combine(TempRoot, "Mixed.cs");
        var lines = new[]
        {
            $"{file}(5,1): warning CS1030: a warning",
            $"{file}(1,1): error CS0001: an error",
            $"{file}(3,1): done a done note",
            $"{file}(4,1): message a message note",
        };

        var results = BuildDiagnosticParser.Parse(lines, TempRoot);

        Assert.Equal(4, results.Count);
        // Sorted by line number within same file
        Assert.Equal(1, results[0].Line);
        Assert.Equal(LanguageDiagnosticSeverity.Error, results[0].Severity);
        Assert.Equal(3, results[1].Line);
        Assert.Equal(LanguageDiagnosticSeverity.Information, results[1].Severity);
        Assert.Equal(4, results[2].Line);
        Assert.Equal(LanguageDiagnosticSeverity.Hint, results[2].Severity);
        Assert.Equal(5, results[3].Line);
        Assert.Equal(LanguageDiagnosticSeverity.Warning, results[3].Severity);
    }

    [Fact]
    public void Parse_DoneWithoutCode_ParsesCorrectly()
    {
        var file = Path.Combine(TempRoot, "DoneNoCode.cs");
        var line = $"{file}(1,1): done build completed";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        var diagnostic = Assert.Single(results);
        Assert.Null(diagnostic.Code);
        Assert.Equal("build completed", diagnostic.Message);
        Assert.Equal(LanguageDiagnosticSeverity.Information, diagnostic.Severity);
    }

    [Fact]
    public void Parse_UnsupportedSeverity_IsIgnored()
    {
        var file = Path.Combine(TempRoot, "Fatal.cs");
        var line = $"{file}(1,1): fatal CS0001: should not parse";

        var results = BuildDiagnosticParser.Parse(new[] { line }, TempRoot);

        Assert.Empty(results);
    }
}
