-- "One row is one tag on one entry."
-- The rows are parts of one entry and are only read through it, so the key is the entry and the position.

CREATE TABLE [memory].[EntryTag] (
    [EntryId]   char(32)     COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Ordinal]   tinyint      NOT NULL,
    [Tag]       nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CreatedAt] datetime2(7) NOT NULL,
    CONSTRAINT [PK_EntryTag] PRIMARY KEY CLUSTERED ([EntryId], [Ordinal]),
    CONSTRAINT [FK_EntryTag_Entry] FOREIGN KEY ([EntryId]) REFERENCES [memory].[Entry] ([Id]) ON DELETE CASCADE,
    -- Rule: a tag appears at most once on an entry. Its index also serves the "entry holds this tag" filter.
    CONSTRAINT [UQ_EntryTag_EntryId_Tag] UNIQUE ([EntryId], [Tag]),
    CONSTRAINT [CK_EntryTag_Ordinal] CHECK ([Ordinal] BETWEEN 0 AND 15),
    CONSTRAINT [CK_EntryTag_Tag_Length] CHECK (DATALENGTH([Tag]) BETWEEN 2 AND 128)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description',
    @value = N'One tag on one memory entry. Insert only; rows are deleted with their entry and there is no row history. Growth: at most 16 rows per entry. Classification: Confidential.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntryTag';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The entry the tag is on.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntryTag', @level2type = N'COLUMN', @level2name = N'EntryId';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The tag''s position on the entry, 0 to 15.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntryTag', @level2type = N'COLUMN', @level2name = N'Ordinal';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The tag, already trimmed and lower-cased by the contract: 1 to 64 characters with no whitespace.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntryTag', @level2type = N'COLUMN', @level2name = N'Tag';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The instant the row was written, in UTC: the creation time of its entry, which is written in the same transaction.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntryTag', @level2type = N'COLUMN', @level2name = N'CreatedAt';
GO
