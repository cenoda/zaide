namespace WorkflowTestsFail;

public sealed class FailingTests
{
    [Fact]
    public void Intentionally_fails() => Assert.Equal(1, 2);
}
