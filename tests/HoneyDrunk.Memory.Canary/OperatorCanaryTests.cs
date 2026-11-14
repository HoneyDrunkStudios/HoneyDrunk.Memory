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

/// <summary>Operational reads against the real HoneyDrunk.Operator approval gate.</summary>
public sealed class OperatorCanaryTests
{
    /// <summary>
    /// Operator's default gate records the request as pending without a human decision, so no
    /// scope is issued; once the expiry passes the read reports expired.
    /// </summary>
    [Fact]
    public async Task Real_operator_gate_leaves_operational_read_pending()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));
        var telemetry = new NullTelemetryActivityFactory();
        var operatorTelemetry = new OperatorTelemetry(telemetry);
        var store = new InMemoryOperatorStore();
        var outbox = new OperatorAuditOutbox(store, new OperatorAuditWriter([]), time, NullLogger<OperatorAuditOutbox>.Instance);
        var gate = new DefaultApprovalGate(new ApprovalEventEmitter(new NullApprovalEventSink(), operatorTelemetry), operatorTelemetry, outbox, time, store);
        var resolver = new MemoryScopeResolver(new DefaultAuthorizationPolicy(telemetry, new RecordingAuditLog(), new FixedGridContextAccessor(), NullLogger<DefaultAuthorizationPolicy>.Instance), gate, Options.Create(new MemoryAuthorizationOptions()), telemetry, time, NullLogger<MemoryScopeResolver>.Instance);
        var key = new MemoryScopeKey(new TenantId("01JC0000000000000000000001"), new ProjectId("01JC0000000000000000000002"), "agent-a");
        var request = new MemoryOperationalReadRequest("operator-1", [key], "audit", time.GetUtcNow().AddMinutes(30), "corr-1");

        var first = await resolver.RequestOperationalScopeAsync(request);
        var second = await resolver.RequestOperationalScopeAsync(request);

        Assert.Equal(MemoryApprovalStatus.Pending, first.Status);
        Assert.Null(first.Scope);
        Assert.Equal(MemoryApprovalStatus.Pending, second.Status);
        Assert.Equal(first.ApprovalId, second.ApprovalId);
        Assert.Equal(first.ApprovalId, (await gate.CheckStatusAsync(first.ApprovalId))!.ApprovalId);

        time.Advance(TimeSpan.FromMinutes(31));
        var expired = await resolver.RequestOperationalScopeAsync(request);

        Assert.Equal(MemoryApprovalStatus.Expired, expired.Status);
        Assert.Null(expired.Scope);
    }
}
