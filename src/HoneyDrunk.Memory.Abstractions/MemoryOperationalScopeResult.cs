namespace HoneyDrunk.Memory.Abstractions;

/// <summary>The outcome of an operational scope request.</summary>
/// <param name="Status">The approval status.</param>
/// <param name="ApprovalId">The approval id derived from the request.</param>
/// <param name="Scope">The read-only operational scope; present exactly when <paramref name="Status"/> is <see cref="MemoryApprovalStatus.Approved"/>.</param>
public sealed record MemoryOperationalScopeResult(MemoryApprovalStatus Status, string ApprovalId, IMemoryScope? Scope)
{
    /// <summary>Gets the approval id.</summary>
    public string ApprovalId { get; } = string.IsNullOrWhiteSpace(ApprovalId)
        ? throw new ArgumentException("An approval id is required.", nameof(ApprovalId))
        : ApprovalId;

    /// <summary>Gets the scope, present exactly when approved.</summary>
    public IMemoryScope? Scope { get; } = (Status == MemoryApprovalStatus.Approved) == (Scope is not null)
        ? Scope
        : throw new ArgumentException("A scope is present exactly when the status is Approved.", nameof(Scope));
}
