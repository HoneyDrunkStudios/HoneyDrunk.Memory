namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// The principal a memory scope is resolved for. The host composition asserts it (the trust boundary
/// is the host process); the runtime maps it to an Auth identity when a scope is widened.
/// </summary>
/// <param name="SubjectId">The subject, for example <c>agent:agent-a</c>.</param>
/// <param name="Scopes">The granted scopes, for example <c>memory.read:{tenant}:{project}:{agent}</c>.</param>
/// <param name="Roles">The granted roles.</param>
public sealed record MemoryCaller(string SubjectId, IReadOnlyList<string> Scopes, IReadOnlyList<string> Roles)
{
    /// <summary>Gets the subject.</summary>
    public string SubjectId { get; } = string.IsNullOrWhiteSpace(SubjectId)
        ? throw new ArgumentException("A subject is required.", nameof(SubjectId))
        : SubjectId;

    /// <summary>Gets a copy of the granted scopes.</summary>
    public IReadOnlyList<string> Scopes { get; } = MemoryValidation.NonBlankList(Scopes, nameof(Scopes));

    /// <summary>Gets a copy of the granted roles.</summary>
    public IReadOnlyList<string> Roles { get; } = MemoryValidation.NonBlankList(Roles, nameof(Roles));
}
