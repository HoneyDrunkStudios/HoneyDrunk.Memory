using HoneyDrunk.Memory.Abstractions;
using System.Reflection;
using Xunit;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>What the package exposes and what it depends on.</summary>
public sealed class SurfaceTests
{
    private static readonly Assembly Provider = typeof(DataMemoryStore).Assembly;

    /// <summary>No type that reaches the tables is public: content leaves only through the store.</summary>
    [Fact]
    public void Only_the_store_and_its_registration_are_public()
    {
        var exported = Provider.GetExportedTypes().Select(type => type.Name).Order(StringComparer.Ordinal);
        var declared = typeof(DataMemoryStore).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(method => method.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["DataMemoryStore", "MemoryDataServiceCollectionExtensions"], exported);
        Assert.Empty(typeof(DataMemoryStore).GetConstructors());
        Assert.Equal(typeof(IMemoryStore).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal), declared);
        Assert.Empty(typeof(DataMemoryStore).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
    }

    /// <summary>The provider depends on the contracts and on HoneyDrunk.Data, not on the runtime or a database driver.</summary>
    [Fact]
    public void Provider_references_no_runtime_and_no_driver()
    {
        string[] allowed = ["HoneyDrunk.Memory.Abstractions", "HoneyDrunk.Kernel.Abstractions", "HoneyDrunk.Data.EntityFramework", "HoneyDrunk.Data.Abstractions", "HoneyDrunk.Data"];

        var references = Provider.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();

        Assert.Contains("HoneyDrunk.Memory.Abstractions", references);
        Assert.Contains("HoneyDrunk.Data.EntityFramework", references);
        Assert.All(references.Where(name => name.StartsWith("HoneyDrunk.", StringComparison.Ordinal)), name => Assert.Contains(name, allowed));
        Assert.DoesNotContain(references, name => name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) || name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) || name.Contains("SqlClient", StringComparison.OrdinalIgnoreCase));
    }
}
