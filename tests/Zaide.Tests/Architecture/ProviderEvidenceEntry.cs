using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Source-level service-provider / service-locator evidence location.
/// Inventory only — does not fail the suite on known legacy sites.
/// </summary>
public sealed class ProviderEvidenceEntry : IEquatable<ProviderEvidenceEntry>
{
    public const string KindIServiceProvider = "IServiceProvider";
    public const string KindAppServices = "App.Services";
    public const string KindGetRequiredService = "GetRequiredService";
    public const string KindGetService = "GetService";

    public ProviderEvidenceEntry(
        string relativePath,
        int line,
        string kind,
        string matchedText)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Line = line;
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        MatchedText = matchedText ?? throw new ArgumentNullException(nameof(matchedText));
    }

    public string RelativePath { get; }

    public int Line { get; }

    public string Kind { get; }

    public string MatchedText { get; }

    public bool Equals(ProviderEvidenceEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        return RelativePath == other.RelativePath
            && Line == other.Line
            && Kind == other.Kind
            && MatchedText == other.MatchedText;
    }

    public override bool Equals(object? obj) => Equals(obj as ProviderEvidenceEntry);

    public override int GetHashCode() =>
        HashCode.Combine(RelativePath, Line, Kind, MatchedText);
}
