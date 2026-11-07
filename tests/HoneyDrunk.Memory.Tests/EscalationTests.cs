using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Operator.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Tests.TestKeys;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Agent scope resolution and Auth-gated escalation (ADR-0022 D6, invariant 142).</summary>
public sealed class EscalationTests
{
    private readonly FakeAuthorizationPolicy policy = new();
    private readonly RecordingTelemetryActivityFactory telemetry = new();
    private readonly ListLogger<MemoryScopeResolver> logger = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The agent path resolves a narrow, writable scope without asking Auth.</summary>
    [Fact]
    public void Agent_scope_needs_no_authorization()
    {
        var home = Key(Project1);

        var scope = this.Resolver().ResolveAgentScope(home, Caller());

        Assert.Equal(MemoryScopeKind.Agent, scope.Kind);
        Assert.Equal(home, scope.Home);
        Assert.Equal([home], scope.Readable);
        Assert.True(scope.CanWrite);
        Assert.Null(scope.ExpiresAt);
        Assert.Empty(this.policy.Calls);
    }

    /// <summary>An uninitialized home key is rejected.</summary>
    [Fact]
    public void Agent_scope_rejects_a_default_key()
    {
        Assert.Throws<ArgumentException>(() => this.Resolver().ResolveAgentScope(default, Caller()));
    }

    /// <summary>A caller holding the read scope gets a new, read-only scope with the same home.</summary>
    [Fact]
    public async Task Escalation_with_read_scope_widens_read_only()
    {
        var home = Key(Project1);
        var other = Key(Project2);
        var scope = this.Resolver().ResolveAgentScope(home, Caller(ReadScope(other)));

        var widened = await scope.EscalateAsync([other, home, other]);

        Assert.Equal(MemoryScopeKind.Escalated, widened.Kind);
        Assert.Equal(home, widened.Home);
        Assert.Equal([home, other], widened.Readable);
        Assert.False(widened.CanWrite);
        Assert.Null(widened.ExpiresAt);
        Assert.Equal([home], scope.Readable);
        Assert.Single(this.policy.Calls);
    }

    /// <summary>A caller without the read scope is denied and gets no scope.</summary>
    [Fact]
    public async Task Escalation_without_read_scope_is_denied()
    {
        var other = Key(Project2);
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller(ReadScope(Key(Project3))));

        var error = await Assert.ThrowsAsync<MemoryEscalationDeniedException>(() => scope.EscalateAsync([other]));

        Assert.Equal([other], error.DeniedScopes);
        Assert.Contains(other.ToString(), error.Message, StringComparison.Ordinal);
    }

    /// <summary>A key in another tenant is denied before Auth is asked, even if the caller claims its scope.</summary>
    [Fact]
    public async Task Cross_tenant_escalation_denied_without_auth_call()
    {
        var foreign = Key(Project1, tenant: TenantB);
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller(ReadScope(foreign)));

        var error = await Assert.ThrowsAsync<MemoryEscalationDeniedException>(() => scope.EscalateAsync([Key(Project2), foreign]));

        Assert.Equal([foreign], error.DeniedScopes);
        Assert.Empty(this.policy.Calls);
    }

    /// <summary>One denied key out of three denies the whole escalation.</summary>
    [Fact]
    public async Task One_denied_key_denies_all()
    {
        var keys = new[] { Key(Project2), Key(Project3), Key(Project1, "agent-b") };
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller(ReadScope(keys[0]), ReadScope(keys[2])));

        var error = await Assert.ThrowsAsync<MemoryEscalationDeniedException>(() => scope.EscalateAsync(keys));

        Assert.Equal([keys[1]], error.DeniedScopes);
        Assert.Equal(3, this.policy.Calls.Count);
    }

    /// <summary>A policy failure denies (fail closed) and keeps the failure as the inner exception.</summary>
    [Fact]
    public async Task Policy_exception_denies()
    {
        var other = Key(Project2);
        this.policy.Throw = new InvalidOperationException("policy store unavailable");
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller(ReadScope(other)));

        var error = await Assert.ThrowsAsync<MemoryEscalationDeniedException>(() => scope.EscalateAsync([other]));

        Assert.Equal([other], error.DeniedScopes);
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains(this.logger.Entries, entry => entry.message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
    }

    /// <summary>Only an agent scope can be widened.</summary>
    [Fact]
    public async Task Escalated_scope_cannot_escalate_again()
    {
        var other = Key(Project2);
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller(ReadScope(other), ReadScope(Key(Project3))));
        var widened = await scope.EscalateAsync([other]);

        await Assert.ThrowsAsync<MemoryScopeException>(() => widened.EscalateAsync([Key(Project3)]));
        Assert.Single(this.policy.Calls);
    }

    /// <summary>The caller maps to an Auth identity and each key to one authorization request.</summary>
    [Fact]
    public async Task Escalation_maps_caller_to_identity_claims()
    {
        var other = Key(Project2);
        var caller = new MemoryCaller("agent:agent-a", [ReadScope(other), "unrelated"], ["memory-reader"]);
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), caller);

        await scope.EscalateAsync([other]);

        var (identity, request) = Assert.Single(this.policy.Calls);
        Assert.NotNull(identity);
        Assert.Equal("agent:agent-a", identity.SubjectId);
        Assert.Equal("honeydrunk-memory-caller", identity.Scheme);
        Assert.Equal([ReadScope(other), "unrelated"], identity.GetClaimValues(AuthClaimTypes.Scope));
        Assert.Equal(["memory-reader"], identity.GetClaimValues(AuthClaimTypes.Role));
        Assert.Equal("memory.escalate", request.Action);
        Assert.Equal("memory/" + other, request.Resource);
        Assert.Equal([ReadScope(other)], request.RequiredScopes);
    }

    /// <summary>The scope template and identity scheme come from the options.</summary>
    [Fact]
    public async Task Escalation_uses_the_configured_template_and_scheme()
    {
        var other = Key(Project2);
        var options = new MemoryAuthorizationOptions { EscalationScopeTemplate = "read:{agent}@{project}/{tenant}", IdentityScheme = "custom-scheme" };
        var scope = this.Resolver(options).ResolveAgentScope(Key(Project1), Caller($"read:{other.AgentId}@{other.ProjectId}/{other.TenantId}"));

        await scope.EscalateAsync([other]);

        var (identity, request) = Assert.Single(this.policy.Calls);
        Assert.Equal("custom-scheme", identity!.Scheme);
        Assert.Equal([$"read:{other.AgentId}@{other.ProjectId}/{other.TenantId}"], request.RequiredScopes);
    }

    /// <summary>Cancellation propagates rather than reading as a denial.</summary>
    [Fact]
    public async Task Cancellation_propagates()
    {
        var other = Key(Project2);
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller(ReadScope(other)));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.EscalateAsync([other], cancelled.Token));

        this.policy.Throw = new OperationCanceledException();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.EscalateAsync([other]));
    }

    /// <summary>More than fifty distinct added keys are rejected before Auth is asked; fifty are allowed.</summary>
    [Fact]
    public async Task Escalation_rejects_more_than_fifty_keys_without_auth_calls()
    {
        var keys = Enumerable.Range(0, 51).Select(i => Key(Project2, $"agent-{i}")).ToArray();
        var scope = this.Resolver().ResolveAgentScope(Key(Project1), Caller([.. keys.Select(ReadScope)]));

        await Assert.ThrowsAsync<ArgumentException>(() => scope.EscalateAsync([.. keys, .. keys, Key(Project1)]));
        Assert.Empty(this.policy.Calls);

        var widened = await scope.EscalateAsync([.. keys.Take(50), Key(Project1)]);
        Assert.Equal(51, widened.Readable.Count);
        Assert.Equal(50, this.policy.Calls.Count);
    }

    /// <summary>An escalation that adds no key is rejected.</summary>
    [Fact]
    public async Task Escalation_needs_a_key_beyond_home()
    {
        var home = Key(Project1);
        var scope = this.Resolver().ResolveAgentScope(home, Caller());

        await Assert.ThrowsAsync<ArgumentException>(() => scope.EscalateAsync([home]));
        Assert.Empty(this.policy.Calls);
    }

    private MemoryScopeResolver Resolver(MemoryAuthorizationOptions? options = null) =>
        new(this.policy, new InMemoryApprovalGate(this.time), Options.Create(options ?? new MemoryAuthorizationOptions()), this.telemetry, this.time, this.logger);
}
