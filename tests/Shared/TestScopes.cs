using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Memory.Abstractions;

namespace HoneyDrunk.Memory.Providers.Contract;

/// <summary>
/// Plain scopes for provider tests. Providers trust the scope they are given; the runtime's
/// scope-checked store is the gate that rejects scopes it did not issue.
/// </summary>
internal static class TestScopes
{
    /// <summary>The first tenant.</summary>
    public static readonly TenantId TenantA = new("01JC0000000000000000000001");

    /// <summary>The second tenant.</summary>
    public static readonly TenantId TenantB = new("01JC0000000000000000000009");

    /// <summary>The first project.</summary>
    public static readonly ProjectId ProjectA = new("01JC0000000000000000000002");

    /// <summary>The second project.</summary>
    public static readonly ProjectId ProjectB = new("01JC0000000000000000000003");

    /// <summary>Creates a key.</summary>
    /// <param name="agentId">The agent id.</param>
    /// <param name="tenant">The tenant; defaults to <see cref="TenantA"/>.</param>
    /// <param name="project">The project; defaults to <see cref="ProjectA"/>.</param>
    /// <returns>The key.</returns>
    public static MemoryScopeKey Key(string agentId = "agent-a", TenantId? tenant = null, ProjectId? project = null) =>
        new(tenant ?? TenantA, project ?? ProjectA, agentId);

    /// <summary>A writable scope over one key.</summary>
    /// <param name="home">The home key.</param>
    /// <returns>The scope.</returns>
    public static IMemoryScope Writable(MemoryScopeKey home) => new TestScope(home, [home], MemoryScopeKind.Agent, true);

    /// <summary>A read-only scope over several keys, home first.</summary>
    /// <param name="home">The home key.</param>
    /// <param name="others">The other readable keys.</param>
    /// <returns>The scope.</returns>
    public static IMemoryScope ReadOnly(MemoryScopeKey home, params MemoryScopeKey[] others) =>
        new TestScope(home, [home, .. others], MemoryScopeKind.Escalated, false);

    private sealed class TestScope(MemoryScopeKey home, IReadOnlyList<MemoryScopeKey> readable, MemoryScopeKind kind, bool canWrite) : IMemoryScope
    {
        public MemoryScopeKey Home { get; } = home;

        public IReadOnlyList<MemoryScopeKey> Readable { get; } = readable;

        public MemoryScopeKind Kind { get; } = kind;

        public bool CanWrite { get; } = canWrite;

        public DateTimeOffset? ExpiresAt => null;

        public Task<IMemoryScope> EscalateAsync(IReadOnlyList<MemoryScopeKey> additionalScopes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Test scopes do not escalate.");
    }
}
