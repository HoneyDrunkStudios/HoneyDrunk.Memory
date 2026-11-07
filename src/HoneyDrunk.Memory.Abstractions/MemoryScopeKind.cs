namespace HoneyDrunk.Memory.Abstractions;

/// <summary>How a memory scope was issued.</summary>
public enum MemoryScopeKind
{
    /// <summary>An agent's own scope: reads and writes its home key only, with no authorization question.</summary>
    Agent = 0,

    /// <summary>An agent scope widened through Auth; read-only.</summary>
    Escalated = 1,

    /// <summary>An operational read scope issued after Operator approval; read-only and time-limited.</summary>
    Operational = 2,
}
