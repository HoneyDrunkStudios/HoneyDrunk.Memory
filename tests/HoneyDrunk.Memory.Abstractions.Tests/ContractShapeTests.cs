using HoneyDrunk.Memory.Abstractions.Tests.ContractShape;
using System.Reflection;
using Xunit;

namespace HoneyDrunk.Memory.Abstractions.Tests;

/// <summary>The contract-shape canary for the frozen Memory surfaces (ADR-0022 D12, invariant 143).</summary>
public sealed class ContractShapeTests
{
    private const string BaselinePath = "api/HoneyDrunk.Memory.Abstractions.contract-shape.txt";

    private static readonly Type[] Roots =
    [
        typeof(IMemoryStore),
        typeof(IMemoryScope),
        typeof(IMemorySummarizer),
        typeof(MemoryEntry),
        typeof(IMemoryScopeResolver),
    ];

    /// <summary>
    /// The frozen contracts match the committed shape. Set <c>HONEYDRUNK_UPDATE_CONTRACT_SHAPE=1</c>
    /// to regenerate it, which is refused unless <c>Version</c> has risen.
    /// </summary>
    [Fact]
    [Trait("Category", "ContractShape")]
    public void Frozen_contracts_match_the_committed_shape()
    {
        var shape = "# package: HoneyDrunk.Memory.Abstractions\n"
            + "# roots: " + string.Join(", ", Roots.Select(root => root.Name)) + "\n"
            + ContractShapeRenderer.Render(Roots);
        var baseline = Path.Join(FindRepositoryRoot(), BaselinePath.Replace('/', Path.DirectorySeparatorChar));
        var informational = typeof(MemoryEntry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException("The Abstractions assembly has no informational version.");
        var update = Environment.GetEnvironmentVariable("HONEYDRUNK_UPDATE_CONTRACT_SHAPE") == "1";

        var (passed, message) = ContractShapeCheck.Verify(shape, baseline, informational.Split('+')[0], update);

        Assert.True(passed, message);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Join(directory.FullName, "HoneyDrunk.Memory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("HoneyDrunk.Memory.slnx was not found above the test output directory.");
    }
}
