using Microsoft.EntityFrameworkCore.Storage;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>Retries a lost commit reply, the way a provider's retry-on-failure strategy retries a transient error.</summary>
/// <param name="dependencies">The strategy's dependencies.</param>
internal sealed class RetryOnLostReply(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 3, TimeSpan.FromMilliseconds(10))
{
    /// <inheritdoc />
    protected override bool ShouldRetryOn(Exception exception) => exception is LostCommitReply.LostReplyException;
}
