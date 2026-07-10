-- =============================================================================
-- V009_program_tuning_managed_by.sql
-- Adds managed_by column to program_tuning_record to distinguish the arranger
-- (ManagedBy) from the actual executor (StartedBy).
-- Idempotent: guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V008 to be applied first.
-- =============================================================================
USE [dashboard_db]
GO

-- ----------------------------------------------------------------------------
-- 1. add managed_by column (NULL, history rows left unfilled)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.program_tuning_record')
      AND name = 'managed_by')
ALTER TABLE [dbo].[program_tuning_record]
    ADD [managed_by] [int] NULL;
GO

-- ----------------------------------------------------------------------------
-- 2. add FK managed_by -> employee
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_ptr_managed_by'
      AND parent_object_id = OBJECT_ID('dbo.program_tuning_record'))
ALTER TABLE [dbo].[program_tuning_record]
    ADD CONSTRAINT [FK_ptr_managed_by]
    FOREIGN KEY ([managed_by]) REFERENCES [dbo].[employee]([employee_id]);
GO

-- ----------------------------------------------------------------------------
-- 3. migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [_migration_history] WHERE [version] = 'V009')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V009', 'add managed_by to program_tuning_record');
GO
