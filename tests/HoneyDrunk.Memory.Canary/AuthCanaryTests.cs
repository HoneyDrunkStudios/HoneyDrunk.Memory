using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Authorization;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Operator.Approval;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Events;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HoneyDrunk.Memory.Canary;

/// <summary>Escalation against the real HoneyDrunk.Auth default policy.</summary>
public sealed class AuthCanaryTests
{
    private static readonly TenantId Tenant = new("01JC0000000000000000000001");
    private static readonly MemoryScopeKey Home = new(Tenant, new ProjectId("01JC0000000000000000000002"), "agent-a");
    private static readonly MemoryScopeKey Other = new(Tenant, new ProjectId("01JC0000000000000000000003"), "agent-a");

    private readonly RecordingAuditLog audit = new();

    /// <summary>The real policy denies a caller without the read scope, and audits the denial.</summary>
    [Fact]
    public async Task Real_auth_policy_denies_escalation_without_the_read_scope()
    {
        var scope = this.Resolver().ResolveAgentScope(Home, new MemoryCaller("agent:agent-a", ["memory.read:unrelated"], []));

        var error = await Assert.ThrowsAsync<MemoryEscalationDeniedException>(() => scope.EscalateAsync([Other]));

        Assert.Equal([Other], error.DeniedScopes);
        var entry = Assert.Single(this.audit.Entries);
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
        Assert.Equal("auth.authorize.memory.escalate", entry.EventName);
    }

    /// <summary>The real policy allows a caller holding the read scope.</summary>
    [Fact]
    public async Task Real_auth_policy_allows_escalation_with_it()
    {
        var readScope = $"memory.read:{Other.TenantId}:{Other.ProjectId}:{Other.AgentId}";
        var scope = this.Resolver().ResolveAgentScope(Home, new MemoryCaller("agent:agent-a", [readScope], []));

        var widened = await scope.EscalateAsync([Other]);

        Assert.Equal([Home, Other], widened.Readable);
        Assert.False(widened.CanWrite);
        Assert.Equal(AuditOutcome.Succeeded, Assert.Single(this.audit.Entries).Outcome);
    }

    private MemoryScopeResolver Resolver()
    {
        var telemetry = new NullTelemetryActivityFactory();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));
        var policy = new DefaultAuthorizationPolicy(telemetry, this.audit, new FixedGridContextAccessor(), NullLogger<DefaultAuthorizationPolicy>.Instance);
        var operatorTelemetry = new OperatorTelemetry(telemetry);
        var store = new InMemoryOperatorStore();
        var outbox = new OperatorAuditOutbox(store, new OperatorAuditWriter([]), time, NullLogger<OperatorAuditOutbox>.Instance);
        var gate = new DefaultApprovalGate(new ApprovalEventEmitter(new NullApprovalEventSink(), operatorTelemetry), operatorTelemetry, outbox, time, store);
        return new MemoryScopeResolver(policy, gate, Options.Create(new MemoryAuthorizationOptions()), telemetry, time, NullLogger<MemoryScopeResolver>.Instance);
    }
}
