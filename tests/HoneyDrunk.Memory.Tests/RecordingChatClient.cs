using HoneyDrunk.AI.Abstractions.Chat;

namespace HoneyDrunk.Memory.Tests;

/// <summary>A chat client that records each call and answers with scripted text.</summary>
/// <param name="reply">The text every completion returns.</param>
internal sealed class RecordingChatClient(string reply) : IChatClient
{
    private readonly List<(IReadOnlyList<ChatMessage> messages, ChatOptions? options)> calls = [];

    /// <summary>Gets the calls received, in order.</summary>
    public IReadOnlyList<(IReadOnlyList<ChatMessage> messages, ChatOptions? options)> Calls
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
    public Task<ChatCompletion> CompleteAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        lock (this.calls)
        {
            this.calls.Add(([.. messages], options));
        }

        return Task.FromResult(new ChatCompletion(new ChatMessage(ChatRole.Assistant, reply), "test-model", 10, 5));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatCompletionUpdate> CompleteStreamingAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The summarizer does not stream.");
}
