using Xunit;

namespace HoneyDrunk.Memory.Abstractions.Tests;

/// <summary>Dependency direction of the Abstractions package (ADR-0022 D2).</summary>
public sealed class DependencyTests
{
    /// <summary>The only HoneyDrunk reference is Kernel's Abstractions.</summary>
    [Fact]
    public void Abstractions_reference_only_kernel_abstractions()
    {
        var honeyDrunkReferences = typeof(MemoryEntry).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("HoneyDrunk.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["HoneyDrunk.Kernel.Abstractions"], honeyDrunkReferences);
    }
}
