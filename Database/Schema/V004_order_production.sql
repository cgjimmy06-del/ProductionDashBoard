-- =============================================================================
-- V004_order_production.sql
-- Creates the order_production table for tracking production orders per equipment.
-- Idempotent: guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V002 and V003 to be applied first.
-- =============================================================================
USE [dashboard_db]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ----------------------------------------------------------------------------
-- 1. order_production
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'order_production')
CREATE TABLE [dbo].[order_production] (
    [order_id]             [int]           IDENTITY(1,1) NOT NULL,
    [equipment_id]         [int]           NOT NULL,
    [equipment_product_id] [int]           NOT NULL,
    [status]               [nvarchar](20)  NOT NULL CONSTRAINT [DF_order_production_status]    DEFAULT ('Pending'),
    [quantity]             [int]           NULL,
    [created_by]           [int]           NOT NULL,
    [started_by]           [int]           NULL,
    [started_at]           [datetime2](7)  NULL,
    [ended_at]             [datetime2](7)  NULL,
    [schedule_id]          [int]           NULL,
    [description]          [nvarchar](255) NULL,
    [create_at]            [datetime2](7)  NOT NULL CONSTRAINT [DF_order_production_create_at] DEFAULT (sysdatetime()),
    [update_at]            [datetime2](7)  NOT NULL CONSTRAINT [DF_order_production_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_order_production]        PRIMARY KEY CLUSTERED ([order_id] ASC),
    CONSTRAINT [FK_op_equipment]            FOREIGN KEY ([equipment_id])         REFERENCES [dbo].[equipment]([equipment_id]),
    CONSTRAINT [FK_op_equipment_product]    FOREIGN KEY ([equipment_product_id]) REFERENCES [dbo].[equipment_product]([equipment_product_id]),
    CONSTRAINT [FK_op_created_by]           FOREIGN KEY ([created_by])           REFERENCES [dbo].[employee]([employee_id]),
    CONSTRAINT [FK_op_started_by]           FOREIGN KEY ([started_by])           REFERENCES [dbo].[employee]([employee_id])
    -- FK_op_schedule: to be added via V005 after the schedule table is created
);
GO

-- ----------------------------------------------------------------------------
-- 2. migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM _migration_history WHERE version = 'V004')
    INSERT INTO _migration_history (version, applied_at) VALUES ('V004', sysdatetime());
GO
