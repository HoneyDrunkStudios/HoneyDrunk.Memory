using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>How the tables store an instant.</summary>
internal static class MemoryInstants
{
    /// <summary>
    /// Stores the instant as a UTC date and time and reads it back with a zero offset. A UTC value
    /// compares and orders in SQL on every provider; <see cref="DateTimeOffset"/> does not on SQLite.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <returns>The same builder.</returns>
    internal static PropertyBuilder<DateTimeOffset> StoredAsUtc(this PropertyBuilder<DateTimeOffset> property) =>
        property.HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
}
