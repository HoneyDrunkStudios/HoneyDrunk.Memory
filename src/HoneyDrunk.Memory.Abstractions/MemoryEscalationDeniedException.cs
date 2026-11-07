namespace HoneyDrunk.Memory.Abstractions;

/// <summary>Auth denied a scope escalation. No scope was widened. Messages name keys and reasons, never content.</summary>
public sealed class MemoryEscalationDeniedException : UnauthorizedAccessException
{
    /// <summary>Initializes a new instance of the <see cref="MemoryEscalationDeniedException"/> class.</summary>
    public MemoryEscalationDeniedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryEscalationDeniedException"/> class.</summary>
    /// <param name="message">The message.</param>
    public MemoryEscalationDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryEscalationDeniedException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MemoryEscalationDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryEscalationDeniedException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="deniedScopes">The keys that were denied.</param>
    /// <param name="innerException">The policy failure that caused the denial, if any.</param>
    public MemoryEscalationDeniedException(string message, IReadOnlyList<MemoryScopeKey> deniedScopes, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(deniedScopes);
        this.DeniedScopes = [.. deniedScopes];
    }

    /// <summary>Gets the keys that were denied.</summary>
    public IReadOnlyList<MemoryScopeKey> DeniedScopes { get; } = [];
}
