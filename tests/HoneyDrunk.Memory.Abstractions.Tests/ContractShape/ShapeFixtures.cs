namespace HoneyDrunk.Memory.Abstractions.Tests.ContractShape;

/// <summary>Public fixture types for the renderer tests.</summary>
public static class ShapeFixtures
{
    /// <summary>Fixture kinds with explicit numbers.</summary>
    public enum FixtureKind
    {
        /// <summary>The first kind.</summary>
        First = 0,

        /// <summary>The second kind.</summary>
        Second = 5,
    }

    /// <summary>A fixture root interface.</summary>
    public interface IFixtureRoot
    {
        /// <summary>Gets the current fixture, if any.</summary>
        FixtureRecord? Current { get; }

        /// <summary>Finds a fixture.</summary>
        /// <param name="name">The optional name.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The token.</param>
        /// <returns>The fixture, or null.</returns>
        Task<FixtureRecord?> FindAsync(string? name, int count = 3, CancellationToken cancellationToken = default);
    }

    /// <summary>A fixture record reached through the root's signatures.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Values">The values.</param>
    /// <param name="Kind">The kind.</param>
    public sealed record FixtureRecord(string Name, IReadOnlyList<string?> Values, FixtureKind Kind);

    /// <summary>A fixture type that no root signature mentions.</summary>
    public sealed class FixtureUnreferenced
    {
        /// <summary>Gets or sets a value.</summary>
        public int Value { get; set; }
    }
}
