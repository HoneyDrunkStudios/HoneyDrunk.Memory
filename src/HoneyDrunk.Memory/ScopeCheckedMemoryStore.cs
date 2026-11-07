using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;

namespace HoneyDrunk.Memory;

/// <summary>
/// The runtime's default <see cref="IMemoryStore"/>. It wraps a provider and, before every call,
/// accepts only scopes this runtime issued, refuses an expired scope and refuses writes, summaries
/// and deletes through a read-only scope (invariant 139). Each call runs in one activity carrying
/// only metadata (invariant 141).
/// </summary>
/// <param name="inner">The provider.</param>
/// <param name="providerName">The provider name for telemetry and logs.</param>
/// <param name="telemetry">The activity factory.</param>
/// <param name="timeProvider">The clock used for scope expiry.</param>
/// <param name="logger">The logger.</param>
public sealed class ScopeCheckedMemoryStore(
    IMemoryStore inner,
    string providerName,
    ITelemetryActivityFactory telemetry,
    TimeProvider timeProvider,
    ILogger<ScopeCheckedMemoryStore> logger) : IMemoryStore
{
    private readonly IMemoryStore inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly string providerName = string.IsNullOrWhiteSpace(providerName) ? throw new ArgumentException("A provider name is required.", nameof(providerName)) : providerName;
    private readonly ITelemetryActivityFactory telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<ScopeCheckedMemoryStore> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<MemoryEntry> WriteAsync(IMemoryScope scope, NewMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        var issued = this.Check(scope, write: true);
        ArgumentNullException.ThrowIfNull(entry);
        using var activity = this.Start(MemoryTelemetry.WriteActivity, "write", issued);
        DescribeEntry(activity, entry);
        var written = await this.RunAsync(activity, "write", () => this.inner.WriteAsync(issued, entry, cancellationToken)).ConfigureAwait(false);
        DescribeResult(activity, written);
        return written;
    }

    /// <inheritdoc />
    public async Task<MemoryEntry?> GetAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default)
    {
        var issued = this.Check(scope, write: false);
        ArgumentNullException.ThrowIfNull(entryId);
        using var activity = this.Start(MemoryTelemetry.GetActivity, "get", issued);
        var found = await this.RunAsync(activity, "get", () => this.inner.GetAsync(issued, entryId, cancellationToken)).ConfigureAwait(false);
        activity?.SetTag(MemoryTelemetry.ResultCount, found is null ? 0 : 1);
        if (found is not null)
        {
            DescribeResult(activity, found);
        }

        return found;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryEntry>> ReadAsync(IMemoryScope scope, MemoryQuery query, CancellationToken cancellationToken = default)
    {
        var issued = this.Check(scope, write: false);
        ArgumentNullException.ThrowIfNull(query);
        using var activity = this.Start(MemoryTelemetry.ReadActivity, "read", issued);
        activity?.SetTag(MemoryTelemetry.TagCount, query.AllTags?.Count ?? 0);
        var read = await this.RunAsync(activity, "read", () => this.inner.ReadAsync(issued, query, cancellationToken)).ConfigureAwait(false);
        activity?.SetTag(MemoryTelemetry.ResultCount, read.Count);
        return read;
    }

    /// <inheritdoc />
    public async Task<MemoryEntry> WriteSummaryAsync(IMemoryScope scope, NewMemoryEntry summary, IReadOnlyList<string> supersededIds, CancellationToken cancellationToken = default)
    {
        var issued = this.Check(scope, write: true);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(supersededIds);
        using var activity = this.Start(MemoryTelemetry.WriteActivity, "write_summary", issued);
        DescribeEntry(activity, summary);
        activity?.SetTag(MemoryTelemetry.ResultCount, supersededIds.Count);
        var written = await this.RunAsync(activity, "write_summary", () => this.inner.WriteSummaryAsync(issued, summary, supersededIds, cancellationToken)).ConfigureAwait(false);
        DescribeResult(activity, written);
        return written;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default)
    {
        var issued = this.Check(scope, write: true);
        ArgumentNullException.ThrowIfNull(entryId);
        using var activity = this.Start(MemoryTelemetry.DeleteActivity, "delete", issued);
        var deleted = await this.RunAsync(activity, "delete", () => this.inner.DeleteAsync(issued, entryId, cancellationToken)).ConfigureAwait(false);
        activity?.SetTag(MemoryTelemetry.ResultCount, deleted ? 1 : 0);
        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllAsync(IMemoryScope scope, CancellationToken cancellationToken = default)
    {
        var issued = this.Check(scope, write: true);
        using var activity = this.Start(MemoryTelemetry.DeleteActivity, "delete_all", issued);
        var deleted = await this.RunAsync(activity, "delete_all", () => this.inner.DeleteAllAsync(issued, cancellationToken)).ConfigureAwait(false);
        activity?.SetTag(MemoryTelemetry.ResultCount, deleted);
        return deleted;
    }

    private static void DescribeEntry(Activity? activity, NewMemoryEntry entry)
    {
        activity?.SetTag(MemoryTelemetry.EntryKind, entry.Kind);
        activity?.SetTag(MemoryTelemetry.EntrySize, entry.Content.Length);
        activity?.SetTag(MemoryTelemetry.TagCount, entry.Tags.Count);
    }

    private static void DescribeResult(Activity? activity, MemoryEntry entry)
    {
        activity?.SetTag(MemoryTelemetry.EntryId, entry.Id);
        if (entry.EmbeddingModelId is not null)
        {
            activity?.SetTag(MemoryTelemetry.EmbeddingModelId, entry.EmbeddingModelId);
        }
    }

    private IssuedMemoryScope Check(IMemoryScope scope, bool write)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope is not IssuedMemoryScope issued)
        {
            throw new ArgumentException("The scope was not issued by HoneyDrunk.Memory. Resolve scopes through IMemoryScopeResolver.", nameof(scope));
        }

        if (issued.ExpiresAt is { } expiresAt && this.timeProvider.GetUtcNow() >= expiresAt)
        {
            throw new MemoryScopeException($"The {issued.Kind} scope for '{issued.Home}' expired at {expiresAt.ToString("O", CultureInfo.InvariantCulture)}.");
        }

        if (write && !issued.CanWrite)
        {
            throw new MemoryScopeException($"The {issued.Kind} scope for '{issued.Home}' is read-only.");
        }

        return issued;
    }

    private Activity? Start(string activityName, string operation, IssuedMemoryScope scope) =>
        MemoryTelemetry.Start(this.telemetry, activityName, operation, scope, this.providerName);

    private async Task<T> RunAsync<T>(Activity? activity, string operation, Func<Task<T>> call)
    {
        try
        {
            var result = await call().ConfigureAwait(false);
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Success);
            return result;
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Cancelled);
            throw;
        }
        catch (Exception error)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Error);
            MemoryLog.StoreOperationFailed(this.logger, operation, this.providerName, error.GetType().Name);
            throw;
        }
    }
}
