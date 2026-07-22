using System.Security.Cryptography;
using System.Text;

namespace Phase16NativeHarnessEvaluation;

public static class FixtureTreeHasher
{
    public static string ComputeHash(IReadOnlyDictionary<string, string> relativePathToUtf8Content)
    {
        var normalizedTree = FixturePathCanonicalizer.NormalizeTreeOrThrow(relativePathToUtf8Content);
        var builder = new StringBuilder();
        foreach (var path in normalizedTree.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            builder.Append(path);
            builder.Append('\n');
            builder.Append(StripTrailingWhitespacePerLine(normalizedTree[path]));
            builder.Append('\n');
            builder.Append("---\n");
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string StripTrailingWhitespacePerLine(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }

        return string.Join('\n', lines);
    }
}
