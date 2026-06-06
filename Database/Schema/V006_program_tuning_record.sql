-- =============================================================================
-- V006_program_tuning_record.sql
-- Creates the program_tuning_record table for tracking in-progress tuning sessions.
-- Idempotent: guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V005 to be applied first.
-- =============================================================================
USE [dashboard_db]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ----------------------------------------------------------------------------
-- 1. program_tuning_record
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'program_tuning_record')
CREATE TABLE [dbo].[program_tuning_record] (
    [program_tuning_id]    [int]           IDENTITY(1,1) NOT NULL,
    [equipment_id]         [int]           NOT NULL,
    [equipment_product_id] [int]           NOT NULL,
    [tuning_type]          [varchar](10)   NOT NULL,
    [status]               [varchar](20)   NOT NULL CONSTRAINT [DF_ptr_status]    DEFAULT ('InProgress'),
    [started_by]           [int]           NOT NULL,
    [started_at]           [datetime2](7)  NOT NULL,
    [ended_at]             [datetime2](7)  NULL,
    [description]          [nvarchar](255) NULL,
    [create_at]            [datetime2](7)  NOT NULL CONSTRAINT [DF_ptr_create_at] DEFAULT (sysdatetime()),
    [update_at]            [datetime2](7)  NOT NULL CONSTRAINT [DF_ptr_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_program_tuning_record]  PRIMARY KEY CLUSTERED ([program_tuning_id] ASC),
    CONSTRAINT [FK_ptr_equipment]          FOREIGN KEY ([equipment_id])         REFERENCES [dbo].[equipment]([equipment_id]),
    CONSTRAINT [FK_ptr_equipment_product]  FOREIGN KEY ([equipment_product_id]) REFERENCES [dbo].[equipment_product]([equipment_product_id]),
    CONSTRAINT [FK_ptr_started_by]         FOREIGN KEY ([started_by])           REFERENCES [dbo].[employee]([employee_id])
);
GO

-- ----------------------------------------------------------------------------
-- 2. migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM _migration_history WHERE version = 'V006')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V006', 'create program_tuning_record table');
GO
