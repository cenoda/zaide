namespace Phase16NativeHarnessEvaluation;

public static class Phase16SandboxAvailability
{
    public static bool IsBubblewrapAvailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        return File.Exists("/usr/bin/bwrap");
    }
}
