using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Providers.InMemory;
using HoneyDrunk.Operator.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HoneyDrunk.Memory.Tests;

/// <summary>The runtime composed by hand over the in-memory provider, with recording fakes.</summary>
internal sealed class MemoryHarness
{
    /// <summary>Initializes a new instance of the <see cref="MemoryHarness"/> class.</summary>
    /// <param name="reply">The text the chat client returns.</param>
    public MemoryHarness(string reply = "A short summary.")
    {
        this.Gate = new InMemoryApprovalGate(this.Time);
        this.Provider = new InMemoryMemoryStore(this.Time);
        this.Chat = new RecordingChatClient(reply);
        this.Resolver = new MemoryScopeResolver(this.Policy, this.Gate, Options.Create(new MemoryAuthorizationOptions()), this.Telemetry, this.Time, this.ResolverLog);
        this.Store = new ScopeCheckedMemoryStore(this.Provider, nameof(InMemoryMemoryStore), this.Telemetry, this.Time, this.StoreLog);
        this.Summarizer = new ChatMemorySummarizer(this.Store, this.Chat, Options.Create(new MemorySummarizerOptions()), this.Telemetry, this.SummarizerLog);
    }

    /// <summary>Gets the clock.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    /// <summary>Gets the Auth policy fake.</summary>
    public FakeAuthorizationPolicy Policy { get; } = new();

    /// <summary>Gets the approval gate fake.</summary>
    public InMemoryApprovalGate Gate { get; }

    /// <summary>Gets the recording activity factory.</summary>
    public RecordingTelemetryActivityFactory Telemetry { get; } = new();

    /// <summary>Gets the provider under the scope-checked store.</summary>
    public InMemoryMemoryStore Provider { get; }

    /// <summary>Gets the chat client fake.</summary>
    public RecordingChatClient Chat { get; }

    /// <summary>Gets the resolver.</summary>
    public MemoryScopeResolver Resolver { get; }

    /// <summary>Gets the scope-checked store.</summary>
    public ScopeCheckedMemoryStore Store { get; }

    /// <summary>Gets the summarizer.</summary>
    public ChatMemorySummarizer Summarizer { get; }

    /// <summary>Gets the resolver log.</summary>
    public ListLogger<MemoryScopeResolver> ResolverLog { get; } = new();

    /// <summary>Gets the store log.</summary>
    public ListLogger<ScopeCheckedMemoryStore> StoreLog { get; } = new();

    /// <summary>Gets the summarizer log.</summary>
    public ListLogger<ChatMemorySummarizer> SummarizerLog { get; } = new();

    /// <summary>Gets every captured log line.</summary>
    public IEnumerable<string> LogLines =>
        this.ResolverLog.Entries.Select(entry => entry.message + " " + entry.exception)
            .Concat(this.StoreLog.Entries.Select(entry => entry.message + " " + entry.exception))
            .Concat(this.SummarizerLog.Entries.Select(entry => entry.message + " " + entry.exception));

    /// <summary>Resolves an agent scope.</summary>
    /// <param name="home">The home key.</param>
    /// <param name="scopes">The caller's scopes.</param>
    /// <returns>The scope.</returns>
    public IMemoryScope Agent(MemoryScopeKey home, params string[] scopes) =>
        this.Resolver.ResolveAgentScope(home, TestKeys.Caller(scopes));
}
