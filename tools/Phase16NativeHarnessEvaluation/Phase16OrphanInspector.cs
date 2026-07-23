using System.Diagnostics;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16OrphanInspector
{
    public static bool IsProcessAlive(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            var process = Process.GetProcessById(processId);
            process.Refresh();
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
