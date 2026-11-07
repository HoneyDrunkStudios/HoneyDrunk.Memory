using HoneyDrunk.Memory.Abstractions;

namespace HoneyDrunk.Memory;

/// <summary>
/// A scope issued by <see cref="MemoryScopeResolver"/>. Only the resolver constructs it, and the
/// scope-checked store accepts no other <see cref="IMemoryScope"/>.
/// </summary>
internal sealed class IssuedMemoryScope : IMemoryScope
{
    private readonly MemoryScopeResolver resolver;

    private IssuedMemoryScope(MemoryScopeResolver resolver, MemoryScopeKind kind, IReadOnlyList<MemoryScopeKey> readable, DateTimeOffset? expiresAt, MemoryCaller? caller)
    {
        this.resolver = resolver;
        this.Kind = kind;
        this.Readable = readable;
        this.ExpiresAt = expiresAt;
        this.Caller = caller;
    }

    /// <inheritdoc />
    public MemoryScopeKey Home => this.Readable[0];

    /// <inheritdoc />
    public IReadOnlyList<MemoryScopeKey> Readable { get; }

    /// <inheritdoc />
    public MemoryScopeKind Kind { get; }

    /// <inheritdoc />
    public bool CanWrite => this.Kind == MemoryScopeKind.Agent;

    /// <inheritdoc />
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Gets the caller an agent or escalated scope was resolved for; <see langword="null"/> for an operational scope.</summary>
    public MemoryCaller? Caller { get; }

    /// <summary>Issues an agent scope.</summary>
    /// <param name="resolver">The issuing resolver.</param>
    /// <param name="home">The home key.</param>
    /// <param name="caller">The caller.</param>
    /// <returns>The scope.</returns>
    public static IssuedMemoryScope ForAgent(MemoryScopeResolver resolver, MemoryScopeKey home, MemoryCaller caller) =>
        new(resolver, MemoryScopeKind.Agent, [home], expiresAt: null, caller);

    /// <summary>Issues an escalated scope over the same home and caller.</summary>
    /// <param name="from">The agent scope being widened.</param>
    /// <param name="additional">The added keys, without the home key.</param>
    /// <returns>The scope.</returns>
    public static IssuedMemoryScope ForEscalation(IssuedMemoryScope from, IReadOnlyList<MemoryScopeKey> additional) =>
        new(from.resolver, MemoryScopeKind.Escalated, [from.Home, .. additional], expiresAt: null, from.Caller);

    /// <summary>Issues an operational scope.</summary>
    /// <param name="resolver">The issuing resolver.</param>
    /// <param name="readable">The keys, home first.</param>
    /// <param name="expiresAt">The expiry.</param>
    /// <returns>The scope.</returns>
    public static IssuedMemoryScope ForOperation(MemoryScopeResolver resolver, IReadOnlyList<MemoryScopeKey> readable, DateTimeOffset expiresAt) =>
        new(resolver, MemoryScopeKind.Operational, [.. readable], expiresAt, caller: null);

    /// <inheritdoc />
    public Task<IMemoryScope> EscalateAsync(IReadOnlyList<MemoryScopeKey> additionalScopes, CancellationToken cancellationToken = default) =>
        this.resolver.EscalateAsync(this, additionalScopes, cancellationToken);
}
