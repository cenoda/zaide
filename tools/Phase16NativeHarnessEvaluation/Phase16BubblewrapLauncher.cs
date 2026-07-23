using System.Diagnostics;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16BubblewrapLauncher
{
    public static async Task<Phase16SandboxLaunchResult> LaunchAsync(
        Phase16SandboxLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            throw new InvalidOperationException("Bubblewrap is unavailable on this host.");
        }

        ValidateRequestOrThrow(request);

        var sandboxEnvironment = Phase16EnvironmentPolicy.CreateSandboxEnvironment(request.AllowedEnvironment);
        var startInfo = BuildBubblewrapStartInfo(request, sandboxEnvironment);
        return await Phase16ProcessLifecycleManager.RunAsync(startInfo, request, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ProcessStartInfo BuildBubblewrapStartInfo(
        Phase16SandboxLaunchRequest request,
        IReadOnlyDictionary<string, string> sandboxEnvironment)
    {
        ValidateRequestOrThrow(request);

        var hostWorkspace = Path.GetFullPath(request.WorkingDirectory);
        Directory.CreateDirectory(hostWorkspace);

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/bwrap",
            WorkingDirectory = hostWorkspace,
        };

        startInfo.ArgumentList.Add("--unshare-all");
        startInfo.ArgumentList.Add("--die-with-parent");
        startInfo.ArgumentList.Add("--new-session");
        startInfo.ArgumentList.Add("--ro-bind");
        startInfo.ArgumentList.Add("/");
        startInfo.ArgumentList.Add("/");

        foreach (var writableRoot in request.WritableRootPaths
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            Directory.CreateDirectory(writableRoot);
            startInfo.ArgumentList.Add("--bind");
            startInfo.ArgumentList.Add(writableRoot);
            startInfo.ArgumentList.Add(writableRoot);
        }

        startInfo.ArgumentList.Add("--dev");
        startInfo.ArgumentList.Add("/dev");
        startInfo.ArgumentList.Add("--proc");
        startInfo.ArgumentList.Add("/proc");
        startInfo.ArgumentList.Add("--chdir");
        startInfo.ArgumentList.Add(hostWorkspace);
        startInfo.ArgumentList.Add("--clearenv");

        foreach (var entry in sandboxEnvironment)
        {
            startInfo.ArgumentList.Add("--setenv");
            startInfo.ArgumentList.Add(entry.Key);
            startInfo.ArgumentList.Add(entry.Value);
        }

        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(request.ExecutablePath);
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        foreach (var entry in sandboxEnvironment)
        {
            startInfo.Environment[entry.Key] = entry.Value;
        }

        return startInfo;
    }

    private static void ValidateRequestOrThrow(Phase16SandboxLaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            throw new ManifestValidationException("Sandbox executable path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ManifestValidationException("Sandbox working directory is required.");
        }

        if (request.WritableRootPaths.Count == 0)
        {
            throw new ManifestValidationException("At least one writable root is required.");
        }

        Phase16WritableRootGuard.EnsureWritableRootOrThrow(
            Path.GetFullPath(request.WorkingDirectory),
            request.WritableRootPaths.Select(Path.GetFullPath).ToArray());

        if (!request.ExecutablePath.StartsWith('/'))
        {
            throw new ManifestValidationException("Sandbox executable path must be absolute.");
        }
    }
}
