# HoneyDrunk.Memory.Providers.Data

A durable `IMemoryStore` over HoneyDrunk.Data's Entity Framework layer. Agent memory survives a restart and is shared by every host that uses the same database. It gives the same answers as `HoneyDrunk.Memory.Providers.InMemory`: one shared contract test suite runs against both, and against SQL Server.

The package holds no database driver. The host chooses the database:

```csharp
services.AddMemoryDataProvider(options => options.UseSqlServer(connectionString));
services.AddHoneyDrunkMemory<DataMemoryStore>();
```

The two calls work in either order. `IMemoryStore` still resolves to the runtime's scope-checked wrapper; this package registers only the provider behind it. The context, the entities and the data service are internal, so memory content leaves the package only through `IMemoryStore`.

## The tables

Three tables in a schema of their own, `memory`.

| Table | One row is |
|---|---|
| `memory.Entry` | One memory entry written under one scope key (tenant, project, agent) |
| `memory.EntryTag` | One tag on one entry, with its position |
| `memory.EntrySummarySource` | One entry that a summary entry replaced, in the order the summary listed it |

**The host's database project carries the tables.** The package ships their SQL Server scripts in its `sql` folder (`memory.sql`, `memory.Entry.sql`, `memory.EntryTag.sql`, `memory.EntrySummarySource.sql`); they are also in this repository under `src/HoneyDrunk.Memory.Providers.Data/Sql`. Copy the four files into your database project unchanged. Every table and column has a description, and every constraint and index is named.

- The package ships no migrations. `Database.EnsureCreated()` is for tests only.
- Text that is compared (ids, the scope key, the kind, tags) uses the binary collation `Latin1_General_100_BIN2` in the scripts, so it matches by exact value, as the in-memory provider does. If you create the tables another way, give those columns a binary collation.
- A later change to a table follows expand, then code, then contract, and is listed in the changelog under "Schema".

## What it guarantees

- **Isolation.** Every statement names the tenant, the project and the agent. An entry in another key is never returned, deleted or superseded.
- **Bounded reads.** Tag, kind and time filters, the order and the limit run in SQL. A read across several keys loads content only for the entries it returns.
- **Atomic summaries.** A summary and the supersession of its sources are one transaction. The sources are claimed by one conditional update, so two hosts that summarize the same entry cannot both count it; the loser is refused and changes nothing.
- **Retries.** Writes run under the database provider's execution strategy. When the host configures a retrying strategy (for example `EnableRetryOnFailure`), a write whose commit succeeded but whose reply was lost is not applied twice: the retry finds the entry by its id. Without a retrying strategy that error reaches the caller although the entry is stored, and a caller that writes again stores a second entry.
- **No content in logs.** The package has no logger and starts no activity. A context configured with `EnableSensitiveDataLogging` is refused with `InvalidOperationException`, because Entity Framework would then write memory content into logs. A `DbUpdateException` from a failed write carries no entities.

## Notes for hosts

- Entry content is 1 to 16,000 characters, as the contract requires. `MemoryEntry.EmbeddingModelId` is always `null`: no embedding is stored yet.
- Deleting a summary does not bring its sources back; they keep its id and stay hidden from default reads. This is the in-memory provider's behavior too.
- `AddMemoryDataProvider` registers the context through `AddHoneyDrunkDataEntityFramework`, with HoneyDrunk.Data's correlation interceptor off. It never takes HoneyDrunk.Data's non-generic `IUnitOfWork` or `IUnitOfWorkFactory`: those stay with the host's own context, whichever is registered first.
- A read takes several statements. An entry that another host deletes or supersedes while it runs comes back whole or not at all, never without its tags. So a read can return fewer entries than its limit; a short page is not proof that nothing older is left.
- Known limit: content must be well-formed text. An unpaired surrogate (for example an emoji cut in half at the length limit) is stored as U+FFFD by the SQL Server client; everything else, a null character included, comes back verbatim.
- Known limit: a delete that is retried after a lost reply reports what the retry removed, which can be nothing, although the rows are gone.

## Tests

`dotnet test HoneyDrunk.Memory.slnx -c Release` runs the contract on SQLite and needs no database. The SQL Server tests deploy the shipped scripts to a new database on a server you name, and drop it afterwards:

```bash
sqllocaldb create MemoryProviderTests -s
HONEYDRUNK_MEMORY_TEST_SQLSERVER='Server=(localdb)\MemoryProviderTests;Integrated Security=true;Encrypt=false' dotnet test tests/HoneyDrunk.Memory.Providers.Data.SqlServerTests -c Release
```

The connection string must not name a database: the tests never target an existing one.
