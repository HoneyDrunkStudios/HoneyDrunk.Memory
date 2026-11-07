using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using System.Diagnostics;

namespace HoneyDrunk.Memory;

/// <summary>
/// Activity names and the tag allowlist. Only metadata crosses the telemetry boundary: never memory
/// content, entry tags or query tags, which are counted instead (invariant 141).
/// </summary>
internal static class MemoryTelemetry
{
    /// <summary>Activity for a write or a summary write.</summary>
    public const string WriteActivity = "memory.write";

    /// <summary>Activity for a read.</summary>
    public const string ReadActivity = "memory.read";

    /// <summary>Activity for a get by id.</summary>
    public const string GetActivity = "memory.get";

    /// <summary>Activity for a delete or a delete-all.</summary>
    public const string DeleteActivity = "memory.delete";

    /// <summary>Activity for a summarization.</summary>
    public const string SummarizeActivity = "memory.summarize";

    /// <summary>Activity for a scope escalation.</summary>
    public const string EscalateActivity = "memory.escalate";

    /// <summary>Activity for an operational read request.</summary>
    public const string OperationalReadActivity = "memory.operational_read";

    /// <summary>The operation tag.</summary>
    public const string Operation = "memory.operation";

    /// <summary>The home tenant tag.</summary>
    public const string TenantId = "memory.tenant_id";

    /// <summary>The home project tag.</summary>
    public const string ProjectId = "memory.project_id";

    /// <summary>The home agent tag.</summary>
    public const string AgentId = "memory.agent_id";

    /// <summary>The scope kind tag.</summary>
    public const string ScopeKind = "memory.scope_kind";

    /// <summary>The readable key count tag.</summary>
    public const string ReadableScopeCount = "memory.readable_scope_count";

    /// <summary>The entry id tag.</summary>
    public const string EntryId = "memory.entry_id";

    /// <summary>The entry kind tag.</summary>
    public const string EntryKind = "memory.entry_kind";

    /// <summary>The entry size tag, in characters.</summary>
    public const string EntrySize = "memory.entry_size";

    /// <summary>The tag count tag.</summary>
    public const string TagCount = "memory.tag_count";

    /// <summary>The result count tag.</summary>
    public const string ResultCount = "memory.result_count";

    /// <summary>The embedding model tag.</summary>
    public const string EmbeddingModelId = "memory.embedding_model_id";

    /// <summary>The provider tag.</summary>
    public const string Provider = "memory.provider";

    /// <summary>The outcome tag.</summary>
    public const string Outcome = "memory.outcome";

    /// <summary>The approval status tag.</summary>
    public const string ApprovalStatus = "memory.approval_status";

    /// <summary>The success outcome.</summary>
    public const string Success = "success";

    /// <summary>The denied outcome.</summary>
    public const string Denied = "denied";

    /// <summary>The error outcome.</summary>
    public const string Error = "error";

    /// <summary>Starts an activity carrying the scope's coordinates.</summary>
    /// <param name="telemetry">The activity factory.</param>
    /// <param name="activityName">The activity name.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="provider">The provider name, if any.</param>
    /// <returns>The activity, or <see langword="null"/> when not sampled.</returns>
    public static Activity? Start(ITelemetryActivityFactory telemetry, string activityName, string operation, IMemoryScope scope, string? provider = null)
    {
        var tags = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Operation] = operation,
            [TenantId] = scope.Home.TenantId.ToString(),
            [ProjectId] = scope.Home.ProjectId.ToString(),
            [AgentId] = scope.Home.AgentId,
            [ScopeKind] = scope.Kind.ToString(),
            [ReadableScopeCount] = scope.Readable.Count,
        };
        if (provider is not null)
        {
            tags[Provider] = provider;
        }

        return telemetry.Start(activityName, tags);
    }
}
