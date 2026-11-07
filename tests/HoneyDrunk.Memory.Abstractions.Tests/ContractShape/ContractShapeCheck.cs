using System.Text;

namespace HoneyDrunk.Memory.Abstractions.Tests.ContractShape;

/// <summary>Compares a rendered contract shape with the committed baseline file.</summary>
internal static class ContractShapeCheck
{
    /// <summary>The command that regenerates the baseline.</summary>
    public const string UpdateCommand = "HONEYDRUNK_UPDATE_CONTRACT_SHAPE=1 dotnet test tests/HoneyDrunk.Memory.Abstractions.Tests -c Release --filter Category=ContractShape";

    private const string VersionHeader = "# version:";
    private const string PackageHeader = "# package:";
    private const int MaxListedLines = 20;

    /// <summary>
    /// Verifies the shape. The baseline header holds <c># package:</c>, <c># version:</c> and
    /// <c># roots:</c>; everything but the version line is the body.
    /// </summary>
    /// <param name="currentShape">The current shape, with its <c># package:</c> and <c># roots:</c> header lines.</param>
    /// <param name="baselineFile">The baseline file path.</param>
    /// <param name="assemblyVersion">The assembly version (the informational version without build metadata).</param>
    /// <param name="updateRequested">Whether regeneration was requested.</param>
    /// <returns>Whether the check passed, and why.</returns>
    public static (bool passed, string message) Verify(string currentShape, string? baselineFile, string assemblyVersion, bool updateRequested)
    {
        ArgumentNullException.ThrowIfNull(currentShape);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyVersion);
        var current = Body(currentShape);
        if (baselineFile is null || !File.Exists(baselineFile))
        {
            if (!updateRequested || baselineFile is null)
            {
                return (false, $"No contract-shape baseline exists at '{baselineFile}'. Generate it with: {UpdateCommand}");
            }

            Write(baselineFile, currentShape, assemblyVersion);
            return (true, $"Wrote the contract-shape baseline at version {assemblyVersion}.");
        }

        var baselineText = File.ReadAllText(baselineFile);
        var baseline = Body(baselineText);
        if (current.SequenceEqual(baseline, StringComparer.Ordinal))
        {
            return (true, "The contract shape matches the baseline.");
        }

        if (!updateRequested)
        {
            return (false, Difference(current, baseline));
        }

        var baselineVersion = Lines(baselineText).FirstOrDefault(line => line.StartsWith(VersionHeader, StringComparison.Ordinal))?[VersionHeader.Length..].Trim();
        if (!Version.TryParse(assemblyVersion, out var next) || !Version.TryParse(baselineVersion, out var previous) || next <= previous)
        {
            return (false, $"The contract shape changed, but the assembly version {assemblyVersion} is not greater than the baseline version {baselineVersion}. Raise Version in Directory.Build.props first.");
        }

        Write(baselineFile, currentShape, assemblyVersion);
        return (true, $"Updated the contract-shape baseline from {baselineVersion} to {assemblyVersion}.");
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0).ToArray();

    private static string[] Body(string text) =>
        Lines(text).Where(line => !line.StartsWith(VersionHeader, StringComparison.Ordinal)).ToArray();

    private static void Write(string file, string currentShape, string version)
    {
        var builder = new StringBuilder();
        var wroteVersion = false;
        foreach (var line in Body(currentShape))
        {
            builder.Append(line).Append('\n');
            if (!wroteVersion && line.StartsWith(PackageHeader, StringComparison.Ordinal))
            {
                builder.Append(VersionHeader).Append(' ').Append(version).Append('\n');
                wroteVersion = true;
            }
        }

        if (!wroteVersion)
        {
            builder.Insert(0, VersionHeader + " " + version + "\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file, builder.ToString());
    }

    private static string Difference(string[] current, string[] baseline)
    {
        var added = current.Except(baseline, StringComparer.Ordinal).ToArray();
        var removed = baseline.Except(current, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder("The contract shape drifted from the committed baseline. Bump Version and run: ")
            .Append(UpdateCommand).Append('\n');
        foreach (var line in added.Take(MaxListedLines))
        {
            builder.Append("+ ").Append(line.Trim()).Append('\n');
        }

        foreach (var line in removed.Take(MaxListedLines))
        {
            builder.Append("- ").Append(line.Trim()).Append('\n');
        }

        if (added.Length > MaxListedLines || removed.Length > MaxListedLines)
        {
            builder.Append('(').Append(added.Length).Append(" added and ").Append(removed.Length).Append(" removed lines in total)\n");
        }

        return builder.ToString();
    }
}
