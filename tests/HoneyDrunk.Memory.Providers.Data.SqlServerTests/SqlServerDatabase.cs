using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HoneyDrunk.Memory.Providers.Data.SqlServerTests;

/// <summary>
/// A throwaway SQL Server database built from the table scripts the package ships. The server comes
/// from the environment; the database is always new, so an existing database is never a target.
/// </summary>
internal sealed class SqlServerDatabase : IDisposable
{
    /// <summary>The environment variable that names the server.</summary>
    public const string Variable = "HONEYDRUNK_MEMORY_TEST_SQLSERVER";

    private static readonly string[] Scripts = ["memory.sql", "memory.Entry.sql", "memory.EntryTag.sql", "memory.EntrySummarySource.sql"];

    private readonly string name = "MemoryProviderTest_" + Guid.NewGuid().ToString("N");
    private readonly string master;

    /// <summary>Initializes a new instance of the <see cref="SqlServerDatabase"/> class and deploys the scripts.</summary>
    public SqlServerDatabase()
    {
        var server = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                $"Set {Variable} to a SQL Server connection string that names no database, for example 'Server=(localdb)\\MemoryProviderTests;Integrated Security=true;Encrypt=false' after 'sqllocaldb create MemoryProviderTests -s'.");
        }

        var builder = new SqlConnectionStringBuilder(server);
        if (!string.IsNullOrEmpty(builder.InitialCatalog) || !string.IsNullOrEmpty(builder.AttachDBFilename))
        {
            throw new InvalidOperationException($"{Variable} must not name a database. These tests create and drop their own; an existing database is never a target.");
        }

        // Read every script before the database exists, so a missing file leaves nothing behind.
        var batches = Scripts.SelectMany(script => Batches(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Sql", script)))).ToArray();
        this.master = new SqlConnectionStringBuilder(server) { InitialCatalog = "master", Pooling = false }.ConnectionString;
        this.ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = this.name, Pooling = false }.ConnectionString;
        using (var admin = this.Context(this.master))
        {
            // The name is a GUID generated above, never outside input.
            var create = "CREATE DATABASE " + this.name;
            admin.Database.ExecuteSqlRaw(create);
        }

        try
        {
            using var context = this.CreateContext();
            foreach (var batch in batches)
            {
                // ExecuteSqlRaw reads braces as placeholders; the scripts have none of their own.
                var statement = batch.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
                context.Database.ExecuteSqlRaw(statement);
            }
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    /// <summary>Gets the connection string of the test database.</summary>
    public string ConnectionString { get; }

    /// <summary>Creates a context over the test database.</summary>
    /// <param name="interceptors">Interceptors to attach.</param>
    /// <returns>The context.</returns>
    public MemoryDbContext CreateContext(params IInterceptor[] interceptors) => this.Context(this.ConnectionString, interceptors);

    /// <summary>Creates a store over the test database.</summary>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="interceptors">Interceptors to attach to every context the store opens.</param>
    /// <returns>The store.</returns>
    public DataMemoryStore CreateStore(TimeProvider timeProvider, params IInterceptor[] interceptors) =>
        new(new MemoryEntryDataService(new ContextFactory(() => this.CreateContext(interceptors))), timeProvider);

    /// <inheritdoc />
    public void Dispose()
    {
        using var admin = this.Context(this.master);
        var drop = "ALTER DATABASE " + this.name + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE " + this.name;
        admin.Database.ExecuteSqlRaw(drop);
    }

    private static IEnumerable<string> Batches(string script)
    {
        var batch = new List<string>();
        foreach (var line in script.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.Any(text => !string.IsNullOrWhiteSpace(text)))
                {
                    yield return string.Join('\n', batch);
                }

                batch.Clear();
            }
            else
            {
                batch.Add(line);
            }
        }

        if (batch.Any(text => !string.IsNullOrWhiteSpace(text)))
        {
            yield return string.Join('\n', batch);
        }
    }

    private MemoryDbContext Context(string connectionString, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MemoryDbContext>().UseSqlServer(connectionString);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new MemoryDbContext(options.Options);
    }

    private sealed class ContextFactory(Func<MemoryDbContext> create) : IDbContextFactory<MemoryDbContext>
    {
        public MemoryDbContext CreateDbContext() => create();
    }
}
