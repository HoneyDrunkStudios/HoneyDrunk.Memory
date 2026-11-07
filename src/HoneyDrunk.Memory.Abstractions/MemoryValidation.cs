using System.Globalization;
using System.Text.RegularExpressions;

namespace HoneyDrunk.Memory.Abstractions;

/// <summary>The pinned formats shared by the memory records.</summary>
internal static partial class MemoryValidation
{
    /// <summary>The most tags an entry or a query may hold.</summary>
    internal const int MaxTags = 16;

    /// <summary>The longest a tag may be.</summary>
    internal const int MaxTagLength = 64;

    /// <summary>The longest entry content, in characters.</summary>
    internal const int MaxContentLength = 16_000;

    /// <summary>Validates an agent id against <c>^[a-z0-9][a-z0-9._-]{0,63}$</c>.</summary>
    /// <param name="agentId">The agent id.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>The agent id, unchanged.</returns>
    internal static string AgentId(string agentId, string paramName)
    {
        ArgumentNullException.ThrowIfNull(agentId, paramName);
        return AgentIdPattern().IsMatch(agentId)
            ? agentId
            : throw new ArgumentException("The agent id must be 1 to 64 characters of a-z, 0-9, '.', '_' or '-', starting with a letter or digit.", paramName);
    }

    /// <summary>Validates an entry kind against <c>^[a-z][a-z0-9_-]{0,31}$</c>.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>The kind, unchanged.</returns>
    internal static string Kind(string kind, string paramName)
    {
        ArgumentNullException.ThrowIfNull(kind, paramName);
        return KindPattern().IsMatch(kind)
            ? kind
            : throw new ArgumentException("The kind must be 1 to 32 characters of a-z, 0-9, '_' or '-', starting with a letter.", paramName);
    }

    /// <summary>Validates entry content: 1 to 16,000 characters, not only whitespace, kept verbatim.</summary>
    /// <param name="content">The content.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>The content, unchanged.</returns>
    internal static string Content(string content, string paramName)
    {
        ArgumentNullException.ThrowIfNull(content, paramName);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("The content must not be empty or whitespace.", paramName);
        }

        return content.Length <= MaxContentLength
            ? content
            : throw new ArgumentException("The content must be at most 16,000 characters.", paramName);
    }

    /// <summary>
    /// Normalizes tags: trimmed, lower-cased with the invariant culture, duplicates removed with the
    /// first order kept. Each tag is 1 to 64 characters with no whitespace or control characters,
    /// and at most 16 remain.
    /// </summary>
    /// <param name="tags">The tags.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>A read-only copy of the normalized tags.</returns>
    internal static IReadOnlyList<string> Tags(IEnumerable<string> tags, string paramName)
    {
        ArgumentNullException.ThrowIfNull(tags, paramName);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>();
        foreach (var tag in tags)
        {
            if (tag is null)
            {
                throw new ArgumentException("A tag must not be null.", paramName);
            }

            var value = tag.Trim().ToLower(CultureInfo.InvariantCulture);
            if (value.Length is 0 or > MaxTagLength || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            {
                throw new ArgumentException("Each tag must be 1 to 64 characters with no whitespace or control characters.", paramName);
            }

            if (seen.Add(value))
            {
                normalized.Add(value);
            }
        }

        return normalized.Count <= MaxTags
            ? normalized.AsReadOnly()
            : throw new ArgumentException("At most 16 distinct tags are allowed.", paramName);
    }

    /// <summary>Copies a list of required non-blank values.</summary>
    /// <param name="values">The values.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>A read-only copy.</returns>
    internal static IReadOnlyList<string> NonBlankList(IReadOnlyList<string> values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        return values.Any(string.IsNullOrWhiteSpace)
            ? throw new ArgumentException("Values must not be null or whitespace.", paramName)
            : Array.AsReadOnly(values.ToArray());
    }

    [GeneratedRegex(@"\A[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex AgentIdPattern();

    [GeneratedRegex(@"\A[a-z][a-z0-9_-]{0,31}\z", RegexOptions.CultureInvariant)]
    private static partial Regex KindPattern();
}
