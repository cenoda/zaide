using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Absolute-path child-process launch options without shell interpolation.
/// </summary>
internal sealed class AcpProcessLaunchOptions
{
    public AcpProcessLaunchOptions(string fileName, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("ACP executable path is required.", nameof(fileName));
        }

        if (!System.IO.Path.IsPathRooted(fileName))
        {
            throw new ArgumentException("ACP executable path must be absolute.", nameof(fileName));
        }

        FileName = fileName;
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string> AllowlistedEnvironment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
