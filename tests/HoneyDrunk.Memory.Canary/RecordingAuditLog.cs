using HoneyDrunk.Audit.Abstractions;

namespace HoneyDrunk.Memory.Canary;

/// <summary>An audit log that keeps what it is given.</summary>
internal sealed class RecordingAuditLog : IAuditLog
{
    private readonly List<AuditEntry> entries = [];

    /// <summary>Gets the appended entries.</summary>
    public IReadOnlyList<AuditEntry> Entries
    {
        get
        {
            lock (this.entries)
            {
                return [.. this.entries];
            }
        }
    }

    /// <inheritdoc />
    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        lock (this.entries)
        {
            this.entries.Add(entry);
        }

        return Task.CompletedTask;
    }
}
