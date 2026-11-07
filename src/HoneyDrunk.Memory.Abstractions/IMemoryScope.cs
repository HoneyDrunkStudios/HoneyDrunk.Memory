namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// A resolved authorization window over agent memory. Every store call takes one. A scope writes only
/// to its <see cref="Home"/> key and reads only the keys in <see cref="Readable"/>.
/// </summary>
public interface IMemoryScope
{
    /// <summary>Gets the home key: the only key this scope can write to.</summary>
    MemoryScopeKey Home { get; }

    /// <summary>Gets the keys this scope may read, with <see cref="Home"/> first.</summary>
    IReadOnlyList<MemoryScopeKey> Readable { get; }

    /// <summary>Gets how the scope was issued.</summary>
    MemoryScopeKind Kind { get; }

    /// <summary>Gets a value indicating whether the scope may write and delete; true only for <see cref="MemoryScopeKind.Agent"/>.</summary>
    bool CanWrite { get; }

    /// <summary>Gets the instant after which the scope is refused, or <see langword="null"/> when it does not expire.</summary>
    DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Widens an agent scope to read further keys. Auth decides (invariant 142); the result is a new,
    /// read-only scope with the same home. All keys are allowed or none.
    /// </summary>
    /// <param name="additionalScopes">The keys to add to the readable set.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The widened, read-only scope.</returns>
    /// <exception cref="MemoryEscalationDeniedException">Auth denied at least one key.</exception>
    /// <exception cref="MemoryScopeException">The scope is not an agent scope.</exception>
    Task<IMemoryScope> EscalateAsync(IReadOnlyList<MemoryScopeKey> additionalScopes, CancellationToken cancellationToken = default);
}
