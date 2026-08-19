-- =============================================================================
-- V010_storage_location.sql
-- Creates warehouse tables: storage_location (location master) and
-- location_assignment (occupancy/history; released_at IS NULL = currently stored).
-- A schedule occupies at most one active location at a time, enforced by a
-- filtered unique index on (schedule_id) WHERE released_at IS NULL.
-- Idempotent: every step is guarded by existence checks, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V008 (schedule) to be applied first.
-- =============================================================================
USE [dashboard_db]
GO

-- ----------------------------------------------------------------------------
-- 1. storage_location
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'storage_location')
CREATE TABLE [dbo].[storage_location] (
    [location_id]       [int]            NOT NULL IDENTITY(1,1),
    [code]              [varchar](50)    NOT NULL,
    [amr_station_code]  [varchar](50)    NULL,
    [zone]              [varchar](50)    NULL,
    [location_type]     [nvarchar](20)   NOT NULL CONSTRAINT [DF_storage_location_location_type] DEFAULT ('Buffer'),
    [capacity]          [int]            NULL,
    [map_x]             [float]          NULL,
    [map_y]             [float]          NULL,
    [equipment_id]      [int]            NULL,
    [is_enabled]        [bit]            NOT NULL CONSTRAINT [DF_storage_location_is_enabled]  DEFAULT (1),
    [create_at]         [datetime2](7)   NOT NULL CONSTRAINT [DF_storage_location_create_at]  DEFAULT (sysdatetime()),
    [update_at]         [datetime2](7)   NOT NULL CONSTRAINT [DF_storage_location_update_at]  DEFAULT (sysdatetime()),
    CONSTRAINT [PK_storage_location]           PRIMARY KEY CLUSTERED ([location_id] ASC),
    CONSTRAINT [UQ_storage_location_code]      UNIQUE NONCLUSTERED ([code] ASC),
    CONSTRAINT [FK_storage_location_equipment] FOREIGN KEY ([equipment_id]) REFERENCES [dbo].[equipment]([equipment_id])
);
GO

-- ----------------------------------------------------------------------------
-- 2. location_assignment
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'location_assignment')
CREATE TABLE [dbo].[location_assignment] (
    [assignment_id]  [int]            NOT NULL IDENTITY(1,1),
    [location_id]    [int]            NOT NULL,
    [schedule_id]    [int]            NOT NULL,
    [assigned_by]    [int]            NOT NULL,
    [assigned_at]    [datetime2](7)   NOT NULL CONSTRAINT [DF_location_assignment_assigned_at] DEFAULT (sysdatetime()),
    [released_by]    [int]            NULL,
    [released_at]    [datetime2](7)   NULL,
    CONSTRAINT [PK_location_assignment]             PRIMARY KEY CLUSTERED ([assignment_id] ASC),
    CONSTRAINT [FK_location_assignment_location]    FOREIGN KEY ([location_id])  REFERENCES [dbo].[storage_location]([location_id]),
    CONSTRAINT [FK_location_assignment_schedule]    FOREIGN KEY ([schedule_id])  REFERENCES [dbo].[schedule]([schedule_id]),
    CONSTRAINT [FK_location_assignment_assigned_by] FOREIGN KEY ([assigned_by]) REFERENCES [dbo].[employee]([employee_id]),
    CONSTRAINT [FK_location_assignment_released_by] FOREIGN KEY ([released_by]) REFERENCES [dbo].[employee]([employee_id])
);
GO

-- ----------------------------------------------------------------------------
-- 3. Filtered unique index: at most one active (not yet released) assignment
--    per schedule. Partial uniqueness that a table-level UNIQUE cannot express.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_location_assignment_active_schedule'
      AND object_id = OBJECT_ID('dbo.location_assignment'))
CREATE UNIQUE INDEX [UX_location_assignment_active_schedule]
    ON [dbo].[location_assignment] ([schedule_id])
    WHERE [released_at] IS NULL;
GO

-- ----------------------------------------------------------------------------
-- 4. Migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [_migration_history] WHERE [version] = 'V010')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V010', 'create storage_location and location_assignment tables for warehouse management');
GO
