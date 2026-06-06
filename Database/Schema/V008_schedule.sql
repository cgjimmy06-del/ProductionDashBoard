-- =============================================================================
-- V008_schedule.sql
-- Creates the schedule table to track material intake-to-release lifecycle.
-- Also adds the FK constraint on order_production.schedule_id (deferred from V004).
-- Idempotent: every step is guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V007 to be applied first.
-- =============================================================================
USE [dashboard_db]
GO

-- ----------------------------------------------------------------------------
-- 1. schedule
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'schedule')
CREATE TABLE [dbo].[schedule] (
    [schedule_id]      [int]            NOT NULL IDENTITY(1,1),
    [product_id]       [int]            NOT NULL,
    [process_id]       [int]            NOT NULL,
    [quantity]         [int]            NOT NULL,
    [lot_no]           [varchar](50)    NULL,
    [status]           [nvarchar](20)   NOT NULL CONSTRAINT [DF_schedule_status]      DEFAULT ('Pending'),
    [received_by]      [int]            NOT NULL,
    [received_at]      [datetime2](7)   NOT NULL CONSTRAINT [DF_schedule_received_at] DEFAULT (sysdatetime()),
    [scheduled_by]     [int]            NULL,
    [scheduled_at]     [datetime2](7)   NULL,
    [verified_by]      [int]            NULL,
    [verified_at]      [datetime2](7)   NULL,
    [actual_quantity]  [int]            NULL,
    [released_by]      [int]            NULL,
    [released_at]      [datetime2](7)   NULL,
    [description]      [nvarchar](255)  NULL,
    [parent_id]        [int]            NULL,
    [create_at]        [datetime2](7)   NOT NULL CONSTRAINT [DF_schedule_create_at]   DEFAULT (sysdatetime()),
    [update_at]        [datetime2](7)   NOT NULL CONSTRAINT [DF_schedule_update_at]   DEFAULT (sysdatetime()),
    CONSTRAINT [PK_schedule]               PRIMARY KEY CLUSTERED ([schedule_id] ASC),
    CONSTRAINT [FK_schedule_product]       FOREIGN KEY ([product_id])   REFERENCES [dbo].[product]([product_id]),
    CONSTRAINT [FK_schedule_process]       FOREIGN KEY ([process_id])   REFERENCES [dbo].[work_process]([process_id]),
    CONSTRAINT [FK_schedule_received_by]   FOREIGN KEY ([received_by])  REFERENCES [dbo].[employee]([employee_id]),
    CONSTRAINT [FK_schedule_scheduled_by]  FOREIGN KEY ([scheduled_by]) REFERENCES [dbo].[employee]([employee_id]),
    CONSTRAINT [FK_schedule_verified_by]   FOREIGN KEY ([verified_by])  REFERENCES [dbo].[employee]([employee_id]),
    CONSTRAINT [FK_schedule_released_by]   FOREIGN KEY ([released_by])  REFERENCES [dbo].[employee]([employee_id]),
    CONSTRAINT [FK_schedule_parent]        FOREIGN KEY ([parent_id])    REFERENCES [dbo].[schedule]([schedule_id])
);
GO

-- ----------------------------------------------------------------------------
-- 2. Add FK on order_production.schedule_id (deferred from V004)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_op_schedule'
      AND parent_object_id = OBJECT_ID('dbo.order_production'))
ALTER TABLE [dbo].[order_production]
    ADD CONSTRAINT [FK_op_schedule]
    FOREIGN KEY ([schedule_id]) REFERENCES [dbo].[schedule]([schedule_id]);
GO

-- ----------------------------------------------------------------------------
-- 3. Migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [_migration_history] WHERE [version] = 'V008')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V008', 'create schedule table; add FK_op_schedule on order_production');
GO
