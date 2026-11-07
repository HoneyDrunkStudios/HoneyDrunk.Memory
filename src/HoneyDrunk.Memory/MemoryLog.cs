using HoneyDrunk.Memory.Abstractions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Memory;

/// <summary>Log messages. They carry counts, ids, statuses and exception type names, never content or tags.</summary>
internal static partial class MemoryLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Memory scope escalation denied for {DeniedCount} of {RequestedCount} keys")]
    public static partial void EscalationDenied(ILogger logger, int deniedCount, int requestedCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Memory scope escalation denied: {CrossTenantCount} keys are in another tenant")]
    public static partial void CrossTenantEscalationDenied(ILogger logger, int crossTenantCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "The authorization policy failed during memory scope escalation ({ExceptionType}); the key is denied")]
    public static partial void EscalationPolicyFailed(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Operational memory read {ApprovalId} is {Status}")]
    public static partial void OperationalReadStatus(ILogger logger, string approvalId, MemoryApprovalStatus status);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Memory store operation {Operation} failed in provider {Provider} ({ExceptionType})")]
    public static partial void StoreOperationFailed(ILogger logger, string operation, string provider, string exceptionType);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "The summarizer model {ModelId} returned an empty summary")]
    public static partial void EmptySummary(ILogger logger, string modelId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "The summary from model {ModelId} was cut to {Length} characters")]
    public static partial void SummaryTruncated(ILogger logger, string modelId, int length);
}
