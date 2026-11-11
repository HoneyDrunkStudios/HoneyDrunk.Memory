using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>
/// Reports one commit as failed after it succeeded, as a connection dropping before the server's reply
/// would. Paired with <see cref="RetryOnLostReply"/>, which retries that failure the way a provider's
/// retrying strategy retries a transient one.
/// </summary>
internal sealed class LostCommitReply : DbTransactionInterceptor
{
    /// <summary>Gets or sets a value indicating whether the next commit's reply is lost.</summary>
    public bool Armed { get; set; }

    /// <inheritdoc />
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (this.Armed)
        {
            this.Armed = false;
            throw new LostReplyException();
        }

        return Task.CompletedTask;
    }

    /// <summary>The lost reply.</summary>
    internal sealed class LostReplyException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="LostReplyException"/> class.</summary>
        public LostReplyException()
            : base("the commit's reply was lost")
        {
        }

        /// <summary>Initializes a new instance of the <see cref="LostReplyException"/> class.</summary>
        /// <param name="message">The message.</param>
        public LostReplyException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="LostReplyException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public LostReplyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
