using System;
using System.Linq;
using System.Text;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Bounded, permission-review-ready display summary for one immutable action request.
/// </summary>
internal sealed class AgentActionDisplaySummary
{
    public AgentActionDisplaySummary(
        AgentActionKind kind,
        string headline,
        string detailText,
        bool wasTruncated)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Action kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            throw new ArgumentException("Display headline is required.", nameof(headline));
        }

        ArgumentNullException.ThrowIfNull(detailText);

        var boundedDetail = BoundDetail(detailText, out var truncated);
        Headline = headline.Trim();
        DetailText = boundedDetail;
        Kind = kind;
        WasTruncated = wasTruncated || truncated;
        LineCount = CountLines(DetailText);
    }

    public AgentActionKind Kind { get; }

    public string Headline { get; }

    public string DetailText { get; }

    public int LineCount { get; }

    public bool WasTruncated { get; }

    private static string BoundDetail(string detailText, out bool truncated)
    {
        truncated = false;
        var lines = detailText.Replace("\r\n", "\n").Split('\n');
        if (lines.Length > AgentActionBudgets.PermissionPreviewSummaryMaxLines)
        {
            truncated = true;
            lines = lines.Take(AgentActionBudgets.PermissionPreviewSummaryMaxLines).ToArray();
        }

        var joined = string.Join('\n', lines);
        var byteCount = AgentActionBudgets.GetUtf8ByteCount(joined);
        if (byteCount <= AgentActionBudgets.PermissionPreviewSummaryMaxBytes)
        {
            return joined;
        }

        truncated = true;
        return TruncateUtf8(joined, AgentActionBudgets.PermissionPreviewSummaryMaxBytes);
    }

    private static string TruncateUtf8(string text, int maxBytes)
    {
        var encoding = Encoding.UTF8;
        if (encoding.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var builder = new StringBuilder();
        var usedBytes = 0;
        foreach (var character in text)
        {
            var charBytes = encoding.GetByteCount(new[] { character });
            if (usedBytes + charBytes > maxBytes)
            {
                break;
            }

            builder.Append(character);
            usedBytes += charBytes;
        }

        return builder.ToString();
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        return text.Replace("\r\n", "\n").Split('\n').Length;
    }
}
