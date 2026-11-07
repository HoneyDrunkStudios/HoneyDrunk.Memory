using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Tests.TestKeys;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Operational reads gated by Operator's approval gate (ADR-0022 D6, invariant 142).</summary>
public sealed class OperationalReadTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider time = new(Now);
    private readonly CountingApprovalGate gate;
    private readonly FakeAuthorizationPolicy policy = new();

    /// <summary>Initializes a new instance of the <see cref="OperationalReadTests"/> class.</summary>
    public OperationalReadTests() => this.gate = new CountingApprovalGate(new InMemoryApprovalGate(this.time));

    /// <summary>The same request, in any key order, always yields the same id.</summary>
    [Fact]
    public void Approval_id_is_stable_for_the_same_request()
    {
        var id = OperationalApprovalId.For(Request([Key(Project1), Key(Project2)]));

        Assert.Equal(id, OperationalApprovalId.For(Request([Key(Project1), Key(Project2)])));
        Assert.Equal(id, OperationalApprovalId.For(Request([Key(Project2), Key(Project1)])));
        Assert.StartsWith("memory-read-", id, StringComparison.Ordinal);
        Assert.Equal("memory-read-".Length + 32, id.Length);
        Assert.Matches("^memory-read-[0-9a-f]{32}$", id);
    }

    /// <summary>Any change to the keys, purpose, expiry or requester changes the id.</summary>
    [Fact]
    public void Approval_id_changes_with_any_scope_purpose_or_expiry()
    {
        var baseline = OperationalApprovalId.For(Request([Key(Project1)]));

        Assert.NotEqual(baseline, OperationalApprovalId.For(Request([Key(Project1), Key(Project2)])));
        Assert.NotEqual(baseline, OperationalApprovalId.For(Request([Key(Project1)], purpose: "another audit")));
        Assert.NotEqual(baseline, OperationalApprovalId.For(Request([Key(Project1)], expiry: Now.AddHours(2).AddTicks(1))));
        Assert.NotEqual(baseline, OperationalApprovalId.For(Request([Key(Project1)], requester: "operator-2")));
        Assert.Equal(baseline, OperationalApprovalId.For(Request([Key(Project1)], expiry: Now.AddHours(2).ToOffset(TimeSpan.FromHours(3)))));
    }

    /// <summary>The first request raises one approval request; asking again reads its status.</summary>
    [Fact]
    public async Task First_request_raises_one_approval_request()
    {
        this.gate.Inner.DefaultOutcome = ApprovalOutcome.Pending;
        var request = Request([Key(Project1)]);

        var first = await this.Resolver().RequestOperationalScopeAsync(request);
        var second = await this.Resolver().RequestOperationalScopeAsync(request);

        Assert.Equal(MemoryApprovalStatus.Pending, first.Status);
        Assert.Equal(MemoryApprovalStatus.Pending, second.Status);
        Assert.Equal(first.ApprovalId, second.ApprovalId);
        Assert.Null(second.Scope);
        Assert.Single(this.gate.Inner.ReceivedRequests);
        Assert.Equal(2, this.gate.StatusChecks);
    }

    /// <summary>An approval issues a read-only operational scope over exactly the requested keys.</summary>
    [Fact]
    public async Task Approved_gate_issues_a_read_only_operational_scope()
    {
        var keys = new[] { Key(Project2), Key(Project1, "agent-b"), Key(Project1, tenant: TenantB) };
        var request = Request(keys);

        var result = await this.Resolver().RequestOperationalScopeAsync(request);

        Assert.Equal(MemoryApprovalStatus.Approved, result.Status);
        var scope = Assert.IsAssignableFrom<IMemoryScope>(result.Scope);
        Assert.Equal(MemoryScopeKind.Operational, scope.Kind);
        Assert.Equal(keys[0], scope.Home);
        Assert.Equal(keys, scope.Readable);
        Assert.False(scope.CanWrite);
        Assert.Equal(request.Expiry, scope.ExpiresAt);
        await Assert.ThrowsAsync<MemoryScopeException>(() => scope.EscalateAsync([Key(Project3)]));

        var raised = Assert.Single(this.gate.Inner.ReceivedRequests);
        Assert.Equal(result.ApprovalId, raised.ApprovalId);
        Assert.Equal("operator-1", raised.Subject);
        Assert.Equal("memory.operational-read", raised.Action);
        Assert.Equal(request.Expiry, raised.Expiry);
        Assert.Equal("corr-1", raised.RequesterCorrelationId);
        Assert.Equal(string.Join(" ", keys.Select(key => key.ToString()).Order(StringComparer.Ordinal)), raised.RequestedScope);
    }

    /// <summary>Pending, denied and expired decisions issue no scope.</summary>
    /// <param name="outcome">The gate's outcome.</param>
    /// <param name="expected">The expected status.</param>
    [Theory]
    [InlineData(ApprovalOutcome.Pending, MemoryApprovalStatus.Pending)]
    [InlineData(ApprovalOutcome.Denied, MemoryApprovalStatus.Denied)]
    [InlineData(ApprovalOutcome.Expired, MemoryApprovalStatus.Expired)]
    public async Task Pending_denied_and_expired_issue_no_scope(ApprovalOutcome outcome, MemoryApprovalStatus expected)
    {
        this.gate.Inner.DefaultOutcome = outcome;

        var result = await this.Resolver().RequestOperationalScopeAsync(Request([Key(Project1)]));

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Scope);
    }

    /// <summary>An approval for one key set never opens another.</summary>
    [Fact]
    public async Task Approval_for_set_A_does_not_open_set_B()
    {
        var setA = await this.Resolver().RequestOperationalScopeAsync(Request([Key(Project1)]));
        this.gate.Inner.DefaultOutcome = ApprovalOutcome.Pending;

        var setB = await this.Resolver().RequestOperationalScopeAsync(Request([Key(Project1), Key(Project2)]));

        Assert.Equal(MemoryApprovalStatus.Approved, setA.Status);
        Assert.Equal([Key(Project1)], setA.Scope!.Readable);
        Assert.Equal(MemoryApprovalStatus.Pending, setB.Status);
        Assert.NotEqual(setA.ApprovalId, setB.ApprovalId);
        Assert.Null(setB.Scope);
    }

    /// <summary>An expiry already passed reads as expired without asking the gate.</summary>
    [Fact]
    public async Task Past_expiry_short_circuits()
    {
        var result = await this.Resolver().RequestOperationalScopeAsync(Request([Key(Project1)], expiry: Now.AddSeconds(-1)));

        Assert.Equal(MemoryApprovalStatus.Expired, result.Status);
        Assert.Null(result.Scope);
        Assert.Equal(0, this.gate.StatusChecks);
        Assert.Empty(this.gate.Inner.ReceivedRequests);
    }

    /// <summary>A new request under a minute ahead is rejected before any approval request is raised.</summary>
    [Fact]
    public async Task New_request_under_a_minute_ahead_is_rejected()
    {
        var request = Request([Key(Project1)], expiry: Now.AddSeconds(30));

        await Assert.ThrowsAsync<ArgumentException>(() => this.Resolver().RequestOperationalScopeAsync(request));
        Assert.Empty(this.gate.Inner.ReceivedRequests);
    }

    /// <summary>An expiry over seven days ahead is rejected without asking the gate.</summary>
    [Fact]
    public async Task Expiry_over_seven_days_ahead_is_rejected()
    {
        var request = Request([Key(Project1)], expiry: Now.AddSeconds((7 * 24 * 3600) + 1));

        await Assert.ThrowsAsync<ArgumentException>(() => this.Resolver().RequestOperationalScopeAsync(request));
        Assert.Equal(0, this.gate.StatusChecks);
        Assert.Empty(this.gate.Inner.ReceivedRequests);
    }

    /// <summary>A request raised in time can still be polled in its final minute, until it expires.</summary>
    [Fact]
    public async Task Existing_request_can_be_polled_in_its_final_minute()
    {
        var request = Request([Key(Project1)], expiry: Now.AddHours(2));
        var first = await this.Resolver().RequestOperationalScopeAsync(request);
        this.time.Advance(TimeSpan.FromHours(2) - TimeSpan.FromSeconds(30));

        var polled = await this.Resolver().RequestOperationalScopeAsync(request);

        Assert.Equal(MemoryApprovalStatus.Approved, polled.Status);
        Assert.Equal(first.ApprovalId, polled.ApprovalId);
        Assert.Equal(request.Expiry, polled.Scope!.ExpiresAt);
        Assert.Single(this.gate.Inner.ReceivedRequests);
    }

    /// <summary>
    /// Changing the request's keys while the gate is awaited cannot redirect the approval: the keys
    /// are a read-only snapshot, so the issued scope covers exactly the keys that were approved.
    /// </summary>
    [Fact]
    public async Task Keys_cannot_be_swapped_while_the_gate_is_awaited()
    {
        var approved = Key(Project1);
        var request = Request([approved]);
        var swapped = false;
        var swapping = new CallbackApprovalGate(this.gate, () => swapped = TrySwap(request.Scopes, Key(Project1, tenant: TenantB)));
        var resolver = new MemoryScopeResolver(this.policy, swapping, Options.Create(new MemoryAuthorizationOptions()), new RecordingTelemetryActivityFactory(), this.time, new ListLogger<MemoryScopeResolver>());

        var result = await resolver.RequestOperationalScopeAsync(request);

        Assert.False(swapped);
        Assert.Equal(MemoryApprovalStatus.Approved, result.Status);
        Assert.Equal([approved], result.Scope!.Readable);
        Assert.Equal([approved], request.Scopes);
    }

    /// <summary>The approval request carries only the key count, the purpose and the keys.</summary>
    [Fact]
    public async Task Approval_request_carries_no_content()
    {
        await this.Resolver().RequestOperationalScopeAsync(Request([Key(Project1), Key(Project2)]));

        var raised = Assert.Single(this.gate.Inner.ReceivedRequests);
        Assert.Equal(["purpose", "scope_count"], raised.Context.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("2", raised.Context["scope_count"]);
        Assert.Equal("quarterly audit", raised.Context["purpose"]);
    }

    /// <summary>A failing or cancelled gate call fails closed and still records an outcome.</summary>
    /// <param name="failOnRequest">Whether the gate fails on RequestAsync rather than CheckStatusAsync.</param>
    /// <param name="cancel">Whether the gate cancels instead of failing.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Gate_failure_is_tagged_logged_and_rethrown(bool failOnRequest, bool cancel)
    {
        var telemetry = new RecordingTelemetryActivityFactory();
        var log = new ListLogger<MemoryScopeResolver>();
        var failing = new ThrowingApprovalGate(failOnRequest, cancel ? new OperationCanceledException() : new InvalidOperationException("gate store unavailable"));
        var resolver = new MemoryScopeResolver(this.policy, failing, Options.Create(new MemoryAuthorizationOptions()), telemetry, this.time, log);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => resolver.RequestOperationalScopeAsync(Request([Key(Project1)])));

        var activity = Assert.Single(telemetry.Activities);
        if (cancel)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(error);
            Assert.Contains(activity.TagObjects, tag => tag.Key == "memory.outcome" && Equals(tag.Value, "cancelled"));
        }
        else
        {
            Assert.IsType<InvalidOperationException>(error);
            Assert.Contains(activity.TagObjects, tag => tag.Key == "memory.outcome" && Equals(tag.Value, "error"));
            Assert.Contains(log.Entries, entry => entry.message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
            Assert.DoesNotContain(log.Entries, entry => entry.message.Contains("gate store unavailable", StringComparison.Ordinal));
        }
    }

    private static MemoryOperationalReadRequest Request(MemoryScopeKey[] keys, string purpose = "quarterly audit", DateTimeOffset? expiry = null, string requester = "operator-1") =>
        new(requester, keys, purpose, expiry ?? Now.AddHours(2), "corr-1");

    private MemoryScopeResolver Resolver() =>
        new(this.policy, this.gate, Options.Create(new MemoryAuthorizationOptions()), new RecordingTelemetryActivityFactory(), this.time, new ListLogger<MemoryScopeResolver>());

    // Overwrites the first key through a cast back to IList<T>; false when the list is read-only.
    private static bool TrySwap(IReadOnlyList<MemoryScopeKey> keys, MemoryScopeKey replacement)
    {
        if (keys is not IList<MemoryScopeKey> list)
        {
            return false;
        }

        try
        {
            list[0] = replacement;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private sealed class CallbackApprovalGate(IApprovalGate inner, Action onCheck) : IApprovalGate
    {
        public Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
            inner.RequestAsync(request, cancellationToken);

        public Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default)
        {
            onCheck();
            return inner.CheckStatusAsync(approvalId, cancellationToken);
        }
    }

    private sealed class ThrowingApprovalGate(bool failOnRequest, Exception error) : IApprovalGate
    {
        public Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<ApprovalDecision>(error);

        public Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default) =>
            failOnRequest ? Task.FromResult<ApprovalDecision?>(null) : Task.FromException<ApprovalDecision?>(error);
    }

    private sealed class CountingApprovalGate(InMemoryApprovalGate inner) : IApprovalGate
    {
        private int statusChecks;

        public InMemoryApprovalGate Inner { get; } = inner;

        public int StatusChecks => Volatile.Read(ref this.statusChecks);

        public Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
            this.Inner.RequestAsync(request, cancellationToken);

        public Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.statusChecks);
            return this.Inner.CheckStatusAsync(approvalId, cancellationToken);
        }
    }
}
