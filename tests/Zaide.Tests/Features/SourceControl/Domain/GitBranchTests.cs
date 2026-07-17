using Xunit;
using Zaide.Features.SourceControl.Domain;

namespace Zaide.Tests.Features.SourceControl.Domain;

public class GitBranchTests
{
    [Fact]
    public void ToString_ReturnsBranchName()
    {
        var branch = new GitBranch("feature/phase-7.4", isCurrent: true);

        Assert.Equal("feature/phase-7.4", branch.ToString());
    }
}
