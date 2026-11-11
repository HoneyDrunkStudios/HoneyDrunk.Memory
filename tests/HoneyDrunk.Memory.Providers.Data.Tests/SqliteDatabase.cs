using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>A throwaway SQLite file with the memory tables; each context opens its own connection.</summary>
internal sealed class SqliteDatabase : IDisposable
{
    private readonly string path = Path.Join(Path.GetTempPath(), "memory-" + Guid.NewGuid().ToString("N") + ".db");

    /// <summary>Initializes a new instance of the <see cref="SqliteDatabase"/> class and creates the tables.</summary>
    public SqliteDatabase()
    {
        this.ConnectionString = $"Data Source={this.path};Default Timeout=30;Pooling=False";
        using var context = this.CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Gets the connection string of the file.</summary>
    public string ConnectionString { get; }

    /// <summary>Points context options at this file.</summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="interceptors">Interceptors to attach.</param>
    public void Configure(DbContextOptionsBuilder options, params IInterceptor[] interceptors)
    {
        options.UseSqlite(this.ConnectionString);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }
    }

    /// <summary>Creates a context over this file.</summary>
    /// <param name="interceptors">Interceptors to attach.</param>
    /// <returns>The context.</returns>
    public MemoryDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MemoryDbContext>();
        this.Configure(options, interceptors);
        return new MemoryDbContext(options.Options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            File.Delete(this.path);
        }
        catch (IOException)
        {
            // A connection is still closing on another thread; the temp folder is cleaned eventually.
        }
    }
}
