using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Memory.Tests;

/// <summary>
/// An authorization policy that records what it is asked and answers like Auth's default rule: every
/// required scope must be held. It can be told to throw instead.
/// </summary>
internal sealed class FakeAuthorizationPolicy : IAuthorizationPolicy
{
    private readonly List<(AuthenticatedIdentity? identity, AuthorizationRequest request)> calls = [];

    /// <summary>Gets or sets an exception to throw from every evaluation.</summary>
    public Exception? Throw { get; set; }

    /// <inheritdoc />
    public string PolicyName => "fake";

    /// <summary>Gets the evaluations received, in order.</summary>
    public IReadOnlyList<(AuthenticatedIdentity? identity, AuthorizationRequest request)> Calls
    {
        get
        {
            lock (this.calls)
            {
                return [.. this.calls];
            }
        }
    }

    /// <inheritdoc />
    public Task<AuthorizationDecision> EvaluateAsync(AuthenticatedIdentity? identity, AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        lock (this.calls)
        {
            this.calls.Add((identity, request));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (this.Throw is not null)
        {
            throw this.Throw;
        }

        var missing = request.RequiredScopes.Where(scope => identity is null || !identity.HasClaim(AuthClaimTypes.Scope, scope)).ToArray();
        return Task.FromResult(missing.Length == 0
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(missing.Select(scope => new DenyReason(AuthorizationDenyCode.MissingScope, "missing " + scope))));
    }
}
