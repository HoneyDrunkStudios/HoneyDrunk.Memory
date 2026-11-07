namespace HoneyDrunk.Memory;

/// <summary>How memory scope escalation is phrased to Auth.</summary>
public sealed class MemoryAuthorizationOptions
{
    /// <summary>
    /// Gets or sets the scope a caller must hold to read a key through escalation. <c>{tenant}</c>,
    /// <c>{project}</c> and <c>{agent}</c> are replaced with the key's coordinates.
    /// </summary>
    public string EscalationScopeTemplate { get; set; } = "memory.read:{tenant}:{project}:{agent}";

    /// <summary>Gets or sets the scheme of the Auth identity built from a memory caller.</summary>
    public string IdentityScheme { get; set; } = "honeydrunk-memory-caller";
}
