namespace WorkflowTestsPass;

public sealed class PassingTests
{
    [Fact]
    public void Always_passes() => Assert.True(true);
}
