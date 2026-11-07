using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Memory.Abstractions;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Neutral tenants, projects and keys for runtime tests.</summary>
internal static class TestKeys
{
    /// <summary>The first tenant.</summary>
    public static readonly TenantId TenantA = new("01JC0000000000000000000001");

    /// <summary>The second tenant.</summary>
    public static readonly TenantId TenantB = new("01JC0000000000000000000009");

    /// <summary>The first project.</summary>
    public static readonly ProjectId Project1 = new("01JC0000000000000000000002");

    /// <summary>The second project.</summary>
    public static readonly ProjectId Project2 = new("01JC0000000000000000000003");

    /// <summary>The third project.</summary>
    public static readonly ProjectId Project3 = new("01JC0000000000000000000004");

    /// <summary>Creates a key.</summary>
    /// <param name="project">The project.</param>
    /// <param name="agentId">The agent id.</param>
    /// <param name="tenant">The tenant; defaults to <see cref="TenantA"/>.</param>
    /// <returns>The key.</returns>
    public static MemoryScopeKey Key(ProjectId project, string agentId = "agent-a", TenantId? tenant = null) =>
        new(tenant ?? TenantA, project, agentId);

    /// <summary>The read scope the default template requires for a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The scope text.</returns>
    public static string ReadScope(MemoryScopeKey key) => $"memory.read:{key.TenantId}:{key.ProjectId}:{key.AgentId}";

    /// <summary>A caller holding the given scopes.</summary>
    /// <param name="scopes">The scopes.</param>
    /// <returns>The caller.</returns>
    public static MemoryCaller Caller(params string[] scopes) => new("agent:agent-a", scopes, ["memory-reader"]);
}
