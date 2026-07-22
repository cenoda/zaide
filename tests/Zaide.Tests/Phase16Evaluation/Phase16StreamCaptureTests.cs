using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16StreamCaptureTests
{
    [Fact]
    public void Append_EnforcesStdoutByteLimit()
    {
        var buffer = new StreamCaptureBuffer(CaptureLimits.MaxStdoutBytes);
        var chunk = new string('a', CaptureLimits.MaxStdoutBytes + 128);
        buffer.Append(chunk);

        Assert.True(buffer.Truncated);
        Assert.Equal(CaptureLimits.MaxStdoutBytes, buffer.CapturedByteCount);
    }

    [Fact]
    public void Append_HandlesMultiByteUtf8WithoutExceedingLimit()
    {
        var buffer = new StreamCaptureBuffer(2);
        buffer.Append("é"); // 2 bytes in UTF-8 — fills the buffer exactly
        buffer.Append("é"); // must not expand beyond the byte limit

        Assert.True(buffer.Truncated);
        Assert.Equal(2, buffer.CapturedByteCount);
    }
}
