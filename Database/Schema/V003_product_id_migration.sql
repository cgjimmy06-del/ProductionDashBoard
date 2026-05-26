-- =============================================================================
-- V003_product_id_migration.sql
-- Replaces the free-text `product` column in inspection_record and
-- tuning_record with a nullable FK `product_id` referencing product(product_id).
-- Idempotent: every step is guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V002 to be applied first.
-- =============================================================================
USE [dashboard_db]
GO

-- ----------------------------------------------------------------------------
-- 1. inspection_record — add product_id
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.inspection_record') AND name = 'product_id')
ALTER TABLE [dbo].[inspection_record]
    ADD [product_id] [int] NULL;
GO

-- ----------------------------------------------------------------------------
-- 2. tuning_record — add product_id
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.tuning_record') AND name = 'product_id')
ALTER TABLE [dbo].[tuning_record]
    ADD [product_id] [int] NULL;
GO

-- ----------------------------------------------------------------------------
-- 3. Foreign keys
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_inspection_record_product')
ALTER TABLE [dbo].[inspection_record] ADD CONSTRAINT [FK_inspection_record_product]
    FOREIGN KEY ([product_id]) REFERENCES [dbo].[product] ([product_id]);
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_tuning_record_product')
ALTER TABLE [dbo].[tuning_record] ADD CONSTRAINT [FK_tuning_record_product]
    FOREIGN KEY ([product_id]) REFERENCES [dbo].[product] ([product_id]);
GO

-- ----------------------------------------------------------------------------
-- 4. Drop obsolete product column
-- ----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.inspection_record') AND name = 'product')
ALTER TABLE [dbo].[inspection_record] DROP COLUMN [product];
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.tuning_record') AND name = 'product')
ALTER TABLE [dbo].[tuning_record] DROP COLUMN [product];
GO

-- ----------------------------------------------------------------------------
-- 5. Migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [_migration_history] WHERE [version] = 'V003')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V003', 'replace product string with product_id FK in inspection/tuning records');
GO
