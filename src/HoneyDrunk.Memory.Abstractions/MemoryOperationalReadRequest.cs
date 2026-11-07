namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// A request to read several memory keys outside an agent's own scope, for example an audit or an
/// administrative view. Operator approves it; the approval binds exactly this request.
/// </summary>
/// <param name="RequesterId">The requesting operator or system.</param>
/// <param name="Scopes">The keys to read: 1 to 50, duplicates removed with the first order kept. The first is the scope's home.</param>
/// <param name="Purpose">Why the read is needed: 1 to 200 characters on one line. It is shown to the approver.</param>
/// <param name="Expiry">When the request and any scope it yields expire: 1 minute to 7 days ahead when requested.</param>
/// <param name="CorrelationId">The correlation id of the requesting operation.</param>
public sealed record MemoryOperationalReadRequest(
    string RequesterId,
    IReadOnlyList<MemoryScopeKey> Scopes,
    string Purpose,
    DateTimeOffset Expiry,
    string CorrelationId)
{
    /// <summary>The most keys one request may name.</summary>
    public const int MaxScopes = 50;

    /// <summary>The longest purpose.</summary>
    public const int MaxPurposeLength = 200;

    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    /// <summary>Gets the requester.</summary>
    public string RequesterId { get; } = string.IsNullOrWhiteSpace(RequesterId)
        ? throw new ArgumentException("A requester is required.", nameof(RequesterId))
        : RequesterId;

    /// <summary>Gets the distinct keys, in first order.</summary>
    public IReadOnlyList<MemoryScopeKey> Scopes { get; } = ValidateScopes(Scopes, nameof(Scopes));

    /// <summary>Gets the purpose.</summary>
    public string Purpose { get; } = ValidatePurpose(Purpose, nameof(Purpose));

    /// <summary>Gets the correlation id.</summary>
    public string CorrelationId { get; } = string.IsNullOrWhiteSpace(CorrelationId)
        ? throw new ArgumentException("A correlation id is required.", nameof(CorrelationId))
        : CorrelationId;

    private static MemoryScopeKey[] ValidateScopes(IReadOnlyList<MemoryScopeKey> scopes, string paramName)
    {
        ArgumentNullException.ThrowIfNull(scopes, paramName);
        if (scopes.Any(key => key.AgentId is null))
        {
            throw new ArgumentException("Every key must be initialized.", paramName);
        }

        var distinct = scopes.Distinct().ToArray();
        return distinct.Length is >= 1 and <= MaxScopes
            ? distinct
            : throw new ArgumentException("An operational read names 1 to 50 keys.", paramName);
    }

    private static string ValidatePurpose(string purpose, string paramName)
    {
        ArgumentNullException.ThrowIfNull(purpose, paramName);
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > MaxPurposeLength)
        {
            throw new ArgumentException("The purpose must be 1 to 200 characters and not only whitespace.", paramName);
        }

        return purpose.Any(c => char.IsControl(c) || c is LineSeparator or ParagraphSeparator)
            ? throw new ArgumentException("The purpose must be one line with no control characters.", paramName)
            : purpose;
    }
}
