using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    public Task<MemoryOperationalScopeResult> RequestOperationalScopeAsync(MemoryOperationalReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = this.approvalGate;
        _ = this.timeProvider;
        throw new NotSupportedException("Operational reads are not available yet.");
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
