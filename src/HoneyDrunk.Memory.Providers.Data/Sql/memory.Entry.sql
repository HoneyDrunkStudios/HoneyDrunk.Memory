-- "One row is one memory entry written under one scope key."
-- Text that is compared uses a binary collation, so it matches by exact value as the contract requires.

CREATE TABLE [memory].[Entry] (
    [Id]                  char(32)      COLLATE Latin1_General_100_BIN2 NOT NULL,
    [TenantId]            char(26)      COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ProjectId]           char(26)      COLLATE Latin1_General_100_BIN2 NOT NULL,
    [AgentId]             varchar(64)   COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Kind]                varchar(32)   COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Content]             nvarchar(max) NOT NULL,
    [WrittenAt]           datetime2(7)  NOT NULL,
    [SupersededByEntryId] char(32)      COLLATE Latin1_General_100_BIN2 NULL,
    CONSTRAINT [PK_Entry] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_Entry_Id_Format] CHECK ([Id] NOT LIKE '%[^0-9a-f]%'),
    CONSTRAINT [CK_Entry_Content_Length] CHECK (DATALENGTH([Content]) BETWEEN 2 AND 32000),
    CONSTRAINT [CK_Entry_SupersededByEntryId_NotSelf] CHECK ([SupersededByEntryId] IS NULL OR [SupersededByEntryId] <> [Id])
);
GO

-- Serves every read: the entries of one tenant, project and agent, newest first, then by id descending.
-- The included columns let the kind and superseded filters be answered from the index.
CREATE INDEX [IX_Entry_TenantId_ProjectId_AgentId_WrittenAt_Id] ON [memory].[Entry] ([TenantId], [ProjectId], [AgentId], [WrittenAt] DESC, [Id] DESC) INCLUDE ([Kind], [SupersededByEntryId]);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description',
    @value = N'One memory entry written under one scope key (tenant, project, agent). Insert only, except SupersededByEntryId, which is set once when a summary replaces the entry; WrittenAt is the creation stamp and there is no row history. Growth: one row per write; rows leave only when the owning agent deletes one entry or every entry in its key. Classification: Confidential.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The entry id: a version 7 GUID as 32 lower-case hexadecimal digits, assigned by the provider at write time.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'Id';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The tenant the entry belongs to: a Kernel tenant id (ULID text). Tenants are not rows in this database.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'TenantId';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The project within the tenant: a Kernel project id (ULID text). Projects are not rows in this database.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'ProjectId';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The agent that owns the entry: 1 to 64 characters of a-z, 0-9, ''.'', ''_'' or ''-''. Agents are not rows in this database.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'AgentId';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The entry kind chosen by the writer, for example note or summary: 1 to 32 characters of a-z, 0-9, ''_'' or ''-''.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'Kind';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The entry text, verbatim: 1 to 16,000 UTF-16 code units. Never written to logs or telemetry.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'Content';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The instant the entry was written, in UTC.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'WrittenAt';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The id of the summary entry that replaced this one. Null means the entry is current. The id is kept if that summary is later deleted, so there is no foreign key.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'Entry', @level2type = N'COLUMN', @level2name = N'SupersededByEntryId';
GO
