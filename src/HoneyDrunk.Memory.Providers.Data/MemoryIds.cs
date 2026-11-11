namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>The id formats the tables hold.</summary>
internal static class MemoryIds
{
    /// <summary>The length of an entry id: a GUID as 32 hexadecimal digits.</summary>
    internal const int EntryIdLength = 32;

    /// <summary>The length of a tenant or project id: ULID text.</summary>
    internal const int UlidLength = 26;

    /// <summary>Creates the id for an entry written at <paramref name="writtenAt"/>.</summary>
    /// <param name="writtenAt">The write time.</param>
    /// <returns>A version 7 GUID as 32 lower-case hexadecimal digits.</returns>
    internal static string NewEntryId(DateTimeOffset writtenAt) => Guid.CreateVersion7(writtenAt).ToString("N");

    /// <summary>
    /// Tells whether <paramref name="entryId"/> can be an entry id: exactly 32 characters of <c>0-9</c>
    /// and <c>a-f</c>. Anything else is answered without a query, so nothing matches by truncation
    /// or by a collation that folds case.
    /// </summary>
    /// <param name="entryId">The candidate id.</param>
    /// <returns><see langword="true"/> when an entry could have this id.</returns>
    internal static bool IsEntryId(string? entryId)
    {
        if (entryId is null || entryId.Length != EntryIdLength)
        {
            return false;
        }

        foreach (var c in entryId)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
