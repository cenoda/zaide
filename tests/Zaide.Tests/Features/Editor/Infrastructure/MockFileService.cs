using System;
using System.Threading.Tasks;
using Zaide.Services;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Tests.Features.Editor.Infrastructure;

/// <summary>
/// Test double for IFileService. Delegates to real I/O by default,
/// but allows injecting failures for specific paths.
/// </summary>
public class MockFileService : IFileService
{
    /// <summary>
    /// When set, ReadAllTextAsync throws this exception for all paths.
    /// </summary>
    public Exception? ReadException { get; set; }

    /// <summary>
    /// When set, WriteAllTextAsync throws this exception for all paths.
    /// </summary>
    public Exception? WriteException { get; set; }

    /// <summary>
    /// Tracks the last content passed to WriteAllTextAsync.
    /// </summary>
    public string? LastWrittenContent { get; private set; }

    public Task<string> ReadAllTextAsync(string path)
    {
        if (ReadException is not null)
            throw ReadException;
        return Task.FromResult(string.Empty);
    }

    public Task WriteAllTextAsync(string path, string contents)
    {
        LastWrittenContent = contents;
        if (WriteException is not null)
            throw WriteException;
        return Task.CompletedTask;
    }
}
