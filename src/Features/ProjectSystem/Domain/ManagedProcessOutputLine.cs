using System;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// One line captured from a managed process stdout or stderr stream.
/// </summary>
/// <param name="Generation">
/// Operation generation that produced the line. Consumers ignore lines whose
/// generation does not match the active workflow snapshot.
/// </param>
/// <param name="Stream">Whether the line came from stdout or stderr.</param>
/// <param name="Text">The line text without the trailing newline.</param>
/// <param name="Timestamp">UTC timestamp when the line was read.</param>
public sealed record ManagedProcessOutputLine(
    long Generation,
    ProcessStreamKind Stream,
    string Text,
    DateTimeOffset Timestamp);
