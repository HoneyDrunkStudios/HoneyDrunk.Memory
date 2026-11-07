namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// A scope was used for something it does not allow: a write through a read-only scope, an expired
/// scope, or widening a scope that is not an agent scope. Messages name keys and reasons, never content.
/// </summary>
public sealed class MemoryScopeException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="MemoryScopeException"/> class.</summary>
    public MemoryScopeException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryScopeException"/> class.</summary>
    /// <param name="message">The message.</param>
    public MemoryScopeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryScopeException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MemoryScopeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
