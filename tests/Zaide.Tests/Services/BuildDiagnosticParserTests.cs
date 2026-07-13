using System;
using System.IO;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

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
}
