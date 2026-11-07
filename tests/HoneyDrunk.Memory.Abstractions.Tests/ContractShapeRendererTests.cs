using HoneyDrunk.Memory.Abstractions.Tests.ContractShape;
using Xunit;

namespace HoneyDrunk.Memory.Abstractions.Tests;

/// <summary>The contract-shape renderer and the baseline check.</summary>
public sealed class ContractShapeRendererTests : IDisposable
{
    private const string Header = "# package: Fixture\n# roots: IFixtureRoot\n";

    private readonly string directory = Path.Join(Path.GetTempPath(), "memory-shape-" + Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a new instance of the <see cref="ContractShapeRendererTests"/> class.</summary>
    public ContractShapeRendererTests() => Directory.CreateDirectory(this.directory);

    /// <summary>Nullability, defaults and enum numbers appear in the shape.</summary>
    [Fact]
    public void Render_records_nullability_and_defaults()
    {
        var shape = ContractShapeRenderer.Render([typeof(ShapeFixtures.IFixtureRoot)]);

        Assert.Contains("System.Threading.Tasks.Task<HoneyDrunk.Memory.Abstractions.Tests.ContractShape.ShapeFixtures+FixtureRecord?> FindAsync(System.String? name, System.Int32 count = 3, System.Threading.CancellationToken cancellationToken = default)", shape, StringComparison.Ordinal);
        Assert.Contains("System.Collections.Generic.IReadOnlyList<System.String?> Values { get; init; }", shape, StringComparison.Ordinal);
        Assert.Contains("HoneyDrunk.Memory.Abstractions.Tests.ContractShape.ShapeFixtures+FixtureRecord? Current { get; }", shape, StringComparison.Ordinal);
        Assert.Contains("Second = 5", shape, StringComparison.Ordinal);
    }

    /// <summary>Only the roots and the public types in their signatures are rendered.</summary>
    [Fact]
    public void Render_follows_the_signature_closure_only()
    {
        var shape = ContractShapeRenderer.Render([typeof(ShapeFixtures.IFixtureRoot)]);

        Assert.Contains("type HoneyDrunk.Memory.Abstractions.Tests.ContractShape.ShapeFixtures+FixtureRecord:", shape, StringComparison.Ordinal);
        Assert.Contains("type HoneyDrunk.Memory.Abstractions.Tests.ContractShape.ShapeFixtures+FixtureKind:", shape, StringComparison.Ordinal);
        Assert.DoesNotContain("FixtureUnreferenced", shape, StringComparison.Ordinal);
        Assert.DoesNotContain("EqualityContract", shape, StringComparison.Ordinal);
    }

    /// <summary>Two renders are identical.</summary>
    [Fact]
    public void Render_is_stable_across_runs()
    {
        Assert.Equal(ContractShapeRenderer.Render([typeof(ShapeFixtures.IFixtureRoot)]), ContractShapeRenderer.Render([typeof(ShapeFixtures.IFixtureRoot)]));
    }

    /// <summary>A changed body fails without the update flag and lists the difference.</summary>
    [Fact]
    public void Verify_fails_on_drift_without_update()
    {
        var file = this.Baseline("0.1.0", "line-a\n");

        var (passed, message) = ContractShapeCheck.Verify(Header + "line-b\n", file, "0.1.0", updateRequested: false);

        Assert.False(passed);
        Assert.Contains("+ line-b", message, StringComparison.Ordinal);
        Assert.Contains("- line-a", message, StringComparison.Ordinal);
    }

    /// <summary>An update at the same version is refused and the file is unchanged.</summary>
    [Fact]
    public void Verify_refuses_update_at_the_same_version()
    {
        var file = this.Baseline("0.1.0", "line-a\n");
        var before = File.ReadAllText(file);

        var (passed, _) = ContractShapeCheck.Verify(Header + "line-b\n", file, "0.1.0", updateRequested: true);

        Assert.False(passed);
        Assert.Equal(before, File.ReadAllText(file));
    }

    /// <summary>An update at a higher version rewrites the baseline with the new version.</summary>
    [Fact]
    public void Verify_accepts_update_at_a_higher_version()
    {
        var file = this.Baseline("0.1.0", "line-a\n");

        var (passed, _) = ContractShapeCheck.Verify(Header + "line-b\n", file, "0.2.0", updateRequested: true);

        Assert.True(passed);
        var written = File.ReadAllText(file);
        Assert.Contains("# version: 0.2.0", written, StringComparison.Ordinal);
        Assert.Contains("line-b", written, StringComparison.Ordinal);
        Assert.True(ContractShapeCheck.Verify(Header + "line-b\n", file, "0.2.0", updateRequested: false).passed);
    }

    /// <summary>Only the version moving is not drift.</summary>
    [Fact]
    public void Verify_passes_when_only_the_version_moved()
    {
        var file = this.Baseline("0.1.0", "line-a\n");

        Assert.True(ContractShapeCheck.Verify(Header + "line-a\n", file, "0.3.0", updateRequested: false).passed);
    }

    /// <summary>A missing baseline fails and names the update command.</summary>
    [Fact]
    public void Verify_fails_without_a_baseline()
    {
        var file = Path.Join(this.directory, "missing.txt");

        var (passed, message) = ContractShapeCheck.Verify(Header + "line-a\n", file, "0.1.0", updateRequested: false);

        Assert.False(passed);
        Assert.Contains("HONEYDRUNK_UPDATE_CONTRACT_SHAPE=1", message, StringComparison.Ordinal);
        Assert.False(File.Exists(file));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }

    private string Baseline(string version, string body)
    {
        var file = Path.Join(this.directory, "shape.txt");
        File.WriteAllText(file, $"# package: Fixture\n# version: {version}\n# roots: IFixtureRoot\n{body}");
        return file;
    }
}
