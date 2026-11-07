using HoneyDrunk.Kernel.Abstractions.Context;

namespace HoneyDrunk.Memory.Canary;

/// <summary>A grid context accessor over a fixed snapshot.</summary>
internal sealed class FixedGridContextAccessor : IGridContextAccessor
{
    /// <inheritdoc />
    public IGridContext GridContext { get; } = new GridContextSnapshot("memory-canary", "test-studio", "test", correlationId: "corr-canary");
}
