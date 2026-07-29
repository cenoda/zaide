using System;
using System.IO;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// Exclusive workspace-partition file lock for single-writer coordination.
/// A second writer fails closed with contention rather than interleaving writes.
/// </summary>
internal sealed class AgentDurableRecordPartitionLock : IDisposable
{
    private readonly FileStream? _lockStream;

    private AgentDurableRecordPartitionLock(FileStream lockStream)
    {
        _lockStream = lockStream;
    }

    public static bool TryAcquire(string lockPath, out AgentDurableRecordPartitionLock? partitionLock)
    {
        try
        {
            var directory = Path.GetDirectoryName(lockPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            partitionLock = new AgentDurableRecordPartitionLock(stream);
            return true;
        }
        catch (IOException)
        {
            partitionLock = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            partitionLock = null;
            return false;
        }
    }

    public void Dispose()
    {
        _lockStream?.Dispose();
    }
}
