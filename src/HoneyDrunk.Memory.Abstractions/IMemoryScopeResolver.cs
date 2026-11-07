namespace HoneyDrunk.Memory.Abstractions;

/// <summary>Issues memory scopes. Consumers compile against this contract to obtain a scope.</summary>
public interface IMemoryScopeResolver
{
    /// <summary>
    /// Resolves an agent's own scope over its home key. This is the hot path and raises no
    /// authorization question.
    /// </summary>
    /// <param name="home">The agent's home key.</param>
    /// <param name="caller">The caller the scope is resolved for; its grants are used if the scope is widened.</param>
    /// <returns>A writable agent scope that reads only <paramref name="home"/>.</returns>
    IMemoryScope ResolveAgentScope(MemoryScopeKey home, MemoryCaller caller);

    /// <summary>
    /// Requests a read-only operational scope over several keys, gated by Operator approval
    /// (invariant 142). It does not wait: it returns the current approval status, and a scope only
    /// when approved. Calling again with the same request reads the same approval.
    /// </summary>
    /// <param name="request">The operational read request.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The approval status, its id and, when approved, the scope.</returns>
    Task<MemoryOperationalScopeResult> RequestOperationalScopeAsync(MemoryOperationalReadRequest request, CancellationToken cancellationToken = default);
}
