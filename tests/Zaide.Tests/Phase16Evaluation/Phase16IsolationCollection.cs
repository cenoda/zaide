using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

[CollectionDefinition("Phase16Isolation", DisableParallelization = true)]
public sealed class Phase16IsolationCollection : ICollectionFixture<Phase16IsolationCollectionFixture>
{
}

public sealed class Phase16IsolationCollectionFixture
{
    public Phase16IsolationCollectionFixture()
    {
        Phase16CleanupGate.ResetForTesting();
    }
}
