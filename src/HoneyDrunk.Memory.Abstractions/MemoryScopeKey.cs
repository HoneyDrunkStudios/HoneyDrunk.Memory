using HoneyDrunk.Kernel.Abstractions.Identity;

namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// The coordinates of one memory partition: a tenant, a project inside it and an agent. Every entry
/// lives under exactly one key, and a scope names the keys it may read and the one it may write.
/// </summary>
/// <param name="TenantId">The tenant.</param>
/// <param name="ProjectId">The project within the tenant.</param>
/// <param name="AgentId">
/// The agent id: 1 to 64 characters of <c>a-z</c>, <c>0-9</c>, <c>.</c>, <c>_</c> or <c>-</c>,
/// starting with a letter or digit.
/// </param>
public readonly record struct MemoryScopeKey(TenantId TenantId, ProjectId ProjectId, string AgentId)
{
    /// <summary>Gets the validated agent id.</summary>
    public string AgentId { get; } = MemoryValidation.AgentId(AgentId, nameof(AgentId));

    /// <summary>Returns the key text <c>{tenant}/{project}/{agent}</c>.</summary>
    /// <returns>The key text.</returns>
    public override string ToString() => $"{this.TenantId}/{this.ProjectId}/{this.AgentId}";
}
