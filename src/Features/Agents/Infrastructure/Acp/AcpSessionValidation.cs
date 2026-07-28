using System;
using System.IO;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal static class AcpSessionValidation
{
    public static void RequireAbsoluteWorkingDirectory(string absoluteWorkingDirectory)
    {
        if (string.IsNullOrWhiteSpace(absoluteWorkingDirectory))
        {
            throw new AcpProtocolException("ACP session cwd is required.");
        }

        if (!Path.IsPathRooted(absoluteWorkingDirectory))
        {
            throw new AcpProtocolException("ACP session cwd must be an absolute path.");
        }
    }
}
