-- =============================================================================
-- V005_equipment_product_status.sql
-- Adds production_status column to equipment_product table.
-- Idempotent: guarded by COL_LENGTH check, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V001 and V002 to be applied first.
-- =============================================================================
USE [dashboard_db]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ----------------------------------------------------------------------------
-- 1. Add production_status to equipment_product
-- ----------------------------------------------------------------------------
IF COL_LENGTH('dbo.equipment_product', 'production_status') IS NULL
    ALTER TABLE [dbo].[equipment_product]
    ADD [production_status] [varchar](10) NOT NULL
        CONSTRAINT [DF_equipment_product_production_status] DEFAULT ('Infeasible');
GO

-- ----------------------------------------------------------------------------
-- 2. migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM _migration_history WHERE version = 'V005')
    INSERT INTO _migration_history (version, applied_at) VALUES ('V005', sysdatetime());
GO
