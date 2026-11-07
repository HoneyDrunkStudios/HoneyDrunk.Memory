namespace HoneyDrunk.Memory.Abstractions;

/// <summary>The approval status of an operational read.</summary>
public enum MemoryApprovalStatus
{
    /// <summary>Awaiting a human decision.</summary>
    Pending = 0,

    /// <summary>Approved; a scope is issued.</summary>
    Approved = 1,

    /// <summary>Denied.</summary>
    Denied = 2,

    /// <summary>Expired.</summary>
    Expired = 3,
}
