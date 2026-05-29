-- =============================================================================
-- V007_product_model_remark.sql
-- Drops the obsolete tuning_record table (superseded by program_tuning_record),
-- and adds a nullable [remark] column to product_model.
-- Idempotent: every step is guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V006 to be applied first.
-- =============================================================================
USE [dashboard_db]
GO

-- ----------------------------------------------------------------------------
-- 1. Drop obsolete tuning_record (replaced by program_tuning_record)
--    No other table references it; DROP TABLE removes its own FKs
--    (employee_id, equipment_id, FK_tuning_record_product).
-- ----------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tuning_record')
    DROP TABLE [dbo].[tuning_record];
GO

-- ----------------------------------------------------------------------------
-- 2. product_model — add remark
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.product_model') AND name = 'remark')
ALTER TABLE [dbo].[product_model]
    ADD [remark] [varchar](20) NULL;
GO

-- ----------------------------------------------------------------------------
-- 3. Migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [_migration_history] WHERE [version] = 'V007')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V007', 'drop obsolete tuning_record; add remark to product_model');
GO
