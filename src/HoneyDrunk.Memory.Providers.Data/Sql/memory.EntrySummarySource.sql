-- "One row is one entry that a summary entry replaced, in the order the summary listed it."
-- The rows are parts of one summary entry and are only read through it, so the key is the summary and the position.

CREATE TABLE [memory].[EntrySummarySource] (
    [SummaryEntryId] char(32)     COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Ordinal]        int          NOT NULL,
    [SourceEntryId]  char(32)     COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CreatedAt]      datetime2(7) NOT NULL,
    CONSTRAINT [PK_EntrySummarySource] PRIMARY KEY CLUSTERED ([SummaryEntryId], [Ordinal]),
    CONSTRAINT [FK_EntrySummarySource_Entry_Summary] FOREIGN KEY ([SummaryEntryId]) REFERENCES [memory].[Entry] ([Id]) ON DELETE CASCADE,
    -- Rule: an entry appears at most once in a summary's list.
    CONSTRAINT [UQ_EntrySummarySource_SummaryEntryId_SourceEntryId] UNIQUE ([SummaryEntryId], [SourceEntryId]),
    CONSTRAINT [CK_EntrySummarySource_Ordinal] CHECK ([Ordinal] >= 0)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description',
    @value = N'One entry that a summary entry replaced, in the order the summary listed it. Insert only; rows are deleted with their summary entry and there is no row history. Growth: one row per summarized entry. Classification: Confidential.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntrySummarySource';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The summary entry that replaced the source.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntrySummarySource', @level2type = N'COLUMN', @level2name = N'SummaryEntryId';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The source''s position in the summary''s list, from 0.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntrySummarySource', @level2type = N'COLUMN', @level2name = N'Ordinal';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The id of an entry the summary replaced. The id is kept if that entry is later deleted, so there is no foreign key.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntrySummarySource', @level2type = N'COLUMN', @level2name = N'SourceEntryId';
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The instant the row was written, in UTC: the creation time of its summary entry, which is written in the same transaction.',
    @level0type = N'SCHEMA', @level0name = N'memory', @level1type = N'TABLE', @level1name = N'EntrySummarySource', @level2type = N'COLUMN', @level2name = N'CreatedAt';
GO
