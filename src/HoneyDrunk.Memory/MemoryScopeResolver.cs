using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace HoneyDrunk.Memory;

/// <summary>
/// The default <see cref="IMemoryScopeResolver"/>. Agent scopes are resolved with no authorization
/// question; escalation asks Auth's <see cref="IAuthorizationPolicy"/>, and operational reads ask
/// Operator's <see cref="IApprovalGate"/> (ADR-0022 D6, invariant 142). Both give read-only scopes.
/// </summary>
/// <param name="policy">The Auth policy that decides escalations.</param>
/// <param name="approvalGate">The Operator gate that decides operational reads.</param>
/// <param name="options">The escalation options.</param>
/// <param name="telemetry">The activity factory.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class MemoryScopeResolver(
    IAuthorizationPolicy policy,
    IApprovalGate approvalGate,
    IOptions<MemoryAuthorizationOptions> options,
    ITelemetryActivityFactory telemetry,
    TimeProvider timeProvider,
    ILogger<MemoryScopeResolver> logger) : IMemoryScopeResolver
{
    private const string EscalateAction = "memory.escalate";
    private const string OperationalReadAction = "memory.operational-read";

    // Each added key is one Auth evaluation (and one audit append), so one call is bounded like an operational read.
    private const int MaxEscalationKeys = MemoryOperationalReadRequest.MaxScopes;

    private static readonly TimeSpan MinimumOperationalExpiry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaximumOperationalExpiry = TimeSpan.FromDays(7);

    private readonly IAuthorizationPolicy policy = policy ?? throw new ArgumentNullException(nameof(policy));
    private readonly IApprovalGate approvalGate = approvalGate ?? throw new ArgumentNullException(nameof(approvalGate));
    private readonly MemoryAuthorizationOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ITelemetryActivityFactory telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<MemoryScopeResolver> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public IMemoryScope ResolveAgentScope(MemoryScopeKey home, MemoryCaller caller)
    {
        RequireKey(home, nameof(home));
        ArgumentNullException.ThrowIfNull(caller);
        return IssuedMemoryScope.ForAgent(this, home, caller);
    }

    /// <inheritdoc />
    public async Task<MemoryOperationalScopeResult> RequestOperationalScopeAsync(MemoryOperationalReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var approvalId = OperationalApprovalId.For(request);
        using var activity = this.telemetry.Start(
            MemoryTelemetry.OperationalReadActivity,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MemoryTelemetry.Operation] = "operational_read",
                [MemoryTelemetry.ReadableScopeCount] = request.Scopes.Count,
            });
        var now = this.timeProvider.GetUtcNow();
        if (request.Expiry <= now)
        {
            return this.OperationalResult(approvalId, MemoryApprovalStatus.Expired, request, activity);
        }

        // The longest lead time holds for every call. The shortest holds only when a request is first
        // raised, so a request raised in time can still be polled in its final minute.
        var ahead = request.Expiry - now;
        if (ahead > MaximumOperationalExpiry)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Error);
            throw new ArgumentException("The expiry must be at most 7 days ahead.", nameof(request));
        }

        var decision = await this.CallGateAsync(approvalId, activity, token => this.approvalGate.CheckStatusAsync(approvalId, token), cancellationToken).ConfigureAwait(false);
        if (decision is null)
        {
            if (ahead < MinimumOperationalExpiry)
            {
                activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Error);
                throw new ArgumentException("A new operational read must expire 1 minute to 7 days ahead.", nameof(request));
            }

            decision = await this.CallGateAsync(approvalId, activity, token => this.approvalGate.RequestAsync(this.ToApprovalRequest(approvalId, request), token), cancellationToken).ConfigureAwait(false);
        }

        var status = decision.Outcome switch
        {
            ApprovalOutcome.Pending => MemoryApprovalStatus.Pending,
            ApprovalOutcome.Approved => MemoryApprovalStatus.Approved,
            ApprovalOutcome.Expired => MemoryApprovalStatus.Expired,
            ApprovalOutcome.Denied => MemoryApprovalStatus.Denied,
            _ => MemoryApprovalStatus.Denied,
        };
        return this.OperationalResult(approvalId, status, request, activity);
    }

    /// <summary>Widens an agent scope after Auth allows every added key.</summary>
    /// <param name="from">The scope to widen.</param>
    /// <param name="additionalScopes">The keys to add.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The read-only escalated scope.</returns>
    internal async Task<IMemoryScope> EscalateAsync(IssuedMemoryScope from, IReadOnlyList<MemoryScopeKey> additionalScopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(additionalScopes);
        if (from.Kind != MemoryScopeKind.Agent || from.Caller is null)
        {
            throw new MemoryScopeException($"Only an agent scope can be widened; the {from.Kind} scope for '{from.Home}' cannot.");
        }

        foreach (var key in additionalScopes)
        {
            RequireKey(key, nameof(additionalScopes));
        }

        var keys = additionalScopes.Distinct().Where(key => key != from.Home).ToArray();
        if (keys.Length == 0)
        {
            throw new ArgumentException("At least one key other than the home key is required.", nameof(additionalScopes));
        }

        if (keys.Length > MaxEscalationKeys)
        {
            throw new ArgumentException("One escalation adds at most 50 distinct keys beyond the home key.", nameof(additionalScopes));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var activity = MemoryTelemetry.Start(this.telemetry, MemoryTelemetry.EscalateActivity, "escalate", from);
        activity?.SetTag(MemoryTelemetry.ReadableScopeCount, keys.Length + 1);

        var crossTenant = keys.Where(key => key.TenantId != from.Home.TenantId).ToArray();
        if (crossTenant.Length > 0)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Denied);
            MemoryLog.CrossTenantEscalationDenied(this.logger, crossTenant.Length);
            throw new MemoryEscalationDeniedException(DeniedMessage(crossTenant, keys.Length, "another tenant's memory is never readable through escalation"), crossTenant);
        }

        var identity = this.ToIdentity(from.Caller);
        var denied = new List<MemoryScopeKey>();
        Exception? failure = null;
        foreach (var key in keys)
        {
            try
            {
                var decision = await this.policy.EvaluateAsync(identity, this.ToRequest(key), cancellationToken).ConfigureAwait(false);
                if (!decision.IsAllowed)
                {
                    denied.Add(key);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                MemoryLog.EscalationPolicyFailed(this.logger, error.GetType().Name);
                failure ??= error;
                denied.Add(key);
            }
        }

        if (denied.Count > 0)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Denied);
            MemoryLog.EscalationDenied(this.logger, denied.Count, keys.Length);
            throw new MemoryEscalationDeniedException(DeniedMessage(denied, keys.Length, "Auth denied them"), denied, failure);
        }

        activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Success);
        return IssuedMemoryScope.ForEscalation(from, keys);
    }

    private static void RequireKey(MemoryScopeKey key, string paramName)
    {
        if (key.AgentId is null)
        {
            throw new ArgumentException("The scope key is not initialized.", paramName);
        }
    }

    private static string DeniedMessage(IReadOnlyList<MemoryScopeKey> denied, int requested, string reason) =>
        $"Memory scope escalation was denied for {denied.Count} of {requested} keys ({reason}): {string.Join(", ", denied)}.";

    private async Task<T> CallGateAsync<T>(string approvalId, Activity? activity, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Cancelled);
            throw;
        }
        catch (Exception error)
        {
            activity?.SetTag(MemoryTelemetry.Outcome, MemoryTelemetry.Error);
            MemoryLog.ApprovalGateFailed(this.logger, approvalId, error.GetType().Name);
            throw;
        }
    }

    private MemoryOperationalScopeResult OperationalResult(string approvalId, MemoryApprovalStatus status, MemoryOperationalReadRequest request, Activity? activity)
    {
        activity?.SetTag(MemoryTelemetry.ApprovalStatus, status.ToString());
        activity?.SetTag(MemoryTelemetry.Outcome, status == MemoryApprovalStatus.Approved ? MemoryTelemetry.Success : MemoryTelemetry.Denied);
        MemoryLog.OperationalReadStatus(this.logger, approvalId, status);
        var scope = status == MemoryApprovalStatus.Approved ? IssuedMemoryScope.ForOperation(this, request.Scopes, request.Expiry) : null;
        return new MemoryOperationalScopeResult(status, approvalId, scope);
    }

    private ApprovalRequest ToApprovalRequest(string approvalId, MemoryOperationalReadRequest request) =>
        new(
            approvalId,
            request.RequesterId,
            OperationalReadAction,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scope_count"] = request.Scopes.Count.ToString(CultureInfo.InvariantCulture),
                ["purpose"] = request.Purpose,
            },
            string.Join(" ", request.Scopes.Select(key => key.ToString()).Order(StringComparer.Ordinal)),
            request.Expiry,
            request.CorrelationId);

    private AuthenticatedIdentity ToIdentity(MemoryCaller caller) =>
        new(
            caller.SubjectId,
            this.options.IdentityScheme,
            displayName: null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [AuthClaimTypes.Scope] = caller.Scopes,
                [AuthClaimTypes.Role] = caller.Roles,
            });

    private AuthorizationRequest ToRequest(MemoryScopeKey key)
    {
        var requiredScope = this.options.EscalationScopeTemplate
            .Replace("{tenant}", key.TenantId.ToString(), StringComparison.Ordinal)
            .Replace("{project}", key.ProjectId.ToString(), StringComparison.Ordinal)
            .Replace("{agent}", key.AgentId, StringComparison.Ordinal);
        return new AuthorizationRequest(EscalateAction, "memory/" + key, [requiredScope]);
    }
}
