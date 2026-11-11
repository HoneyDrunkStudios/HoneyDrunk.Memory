namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>The id formats the tables hold.</summary>
internal static class MemoryIds
{
    /// <summary>The length of an entry id: a GUID as 32 hexadecimal digits.</summary>
    internal const int EntryIdLength = 32;

    /// <summary>The length of a tenant or project id: ULID text.</summary>
    internal const int UlidLength = 26;
}
