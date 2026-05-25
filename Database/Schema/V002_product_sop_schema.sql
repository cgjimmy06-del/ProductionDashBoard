-- =============================================================================
-- V002_product_sop_schema.sql
-- Adds product / SOP checklist / equipment-producible-list schema.
-- Tables: product_part, product_model, work_process, product,
--         sop_checklist, sop_checklist_item, equipment_product
-- Idempotent: every object is guarded by IF NOT EXISTS, safe to re-run.
-- NOTE: select the target database in SSMS before executing.
--       Requires V001 (provides [_migration_history]) to be applied first.
-- =============================================================================
USE [dashboard_db]

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ----------------------------------------------------------------------------
-- 1. product_part  -- part master
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'product_part')
CREATE TABLE [dbo].[product_part] (
    [part_id]     [int]           IDENTITY(1,1) NOT NULL,
    [part_no]     [varchar](8)    NOT NULL,
    [brand]       [varchar](10)   NULL,
    [name]        [nvarchar](50)  NULL,
    [create_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_product_part_create_at] DEFAULT (sysdatetime()),
    [update_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_product_part_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_product_part] PRIMARY KEY CLUSTERED ([part_id] ASC),
    CONSTRAINT [UQ_product_part_part_no] UNIQUE NONCLUSTERED ([part_no] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- 2. product_model  -- model lookup (IDENTITY key, UI inline add supported)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'product_model')
CREATE TABLE [dbo].[product_model] (
    [model_id]    [int]           IDENTITY(1,1) NOT NULL,
    [name]        [varchar](10)   NOT NULL,
    [create_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_product_model_create_at] DEFAULT (sysdatetime()),
    [update_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_product_model_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_product_model] PRIMARY KEY CLUSTERED ([model_id] ASC),
    CONSTRAINT [UQ_product_model_name] UNIQUE NONCLUSTERED ([name] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- 3. work_process  -- process lookup (manually-assigned key)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'work_process')
CREATE TABLE [dbo].[work_process] (
    [process_id]  [int]           NOT NULL,
    [name]        [nvarchar](50)  NOT NULL,
    [erp_code]    [varchar](20)   NULL,
    [description] [nvarchar](100) NULL,
    [create_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_work_process_create_at] DEFAULT (sysdatetime()),
    [update_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_work_process_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_work_process] PRIMARY KEY CLUSTERED ([process_id] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- 4. product  -- part x model
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'product')
CREATE TABLE [dbo].[product] (
    [product_id]  [int]          IDENTITY(1,1) NOT NULL,
    [part_id]     [int]          NOT NULL,
    [model_id]    [int]          NOT NULL,
    [create_at]   [datetime2](7) NOT NULL CONSTRAINT [DF_product_create_at] DEFAULT (sysdatetime()),
    [update_at]   [datetime2](7) NOT NULL CONSTRAINT [DF_product_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_product] PRIMARY KEY CLUSTERED ([product_id] ASC),
    CONSTRAINT [UQ_product_part_model] UNIQUE NONCLUSTERED ([part_id] ASC, [model_id] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- 5. sop_checklist  -- product x process x type
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sop_checklist')
CREATE TABLE [dbo].[sop_checklist] (
    [sop_id]      [int]           IDENTITY(1,1) NOT NULL,
    [product_id]  [int]           NOT NULL,
    [process_id]  [int]           NOT NULL,
    [sop_type]    [varchar](10)   NOT NULL,
    [remark]      [nvarchar](100) NULL,
    [create_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_sop_checklist_create_at] DEFAULT (sysdatetime()),
    [update_at]   [datetime2](7)  NOT NULL CONSTRAINT [DF_sop_checklist_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_sop_checklist] PRIMARY KEY CLUSTERED ([sop_id] ASC),
    CONSTRAINT [UQ_sop_checklist_product_process_type] UNIQUE NONCLUSTERED
        ([product_id] ASC, [process_id] ASC, [sop_type] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- 6. sop_checklist_item  -- SOP checklist detail (check_type discriminator)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sop_checklist_item')
CREATE TABLE [dbo].[sop_checklist_item] (
    [item_id]        [int]           IDENTITY(1,1) NOT NULL,
    [sop_id]         [int]           NOT NULL,
    [seq]            [int]           NOT NULL,
    [check_type]     [varchar](10)   NOT NULL,
    [workstation_no] [int]           NULL,
    [material_id]    [int]           NULL,
    [quantity]       [int]           NULL,
    [content]        [nvarchar](500) NULL,
    [remark]         [nvarchar](100) NULL,
    CONSTRAINT [PK_sop_checklist_item] PRIMARY KEY CLUSTERED ([item_id] ASC),
    CONSTRAINT [UQ_sop_checklist_item_sop_seq] UNIQUE NONCLUSTERED ([sop_id] ASC, [seq] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- 7. equipment_product  -- equipment producible-product list
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'equipment_product')
CREATE TABLE [dbo].[equipment_product] (
    [equipment_product_id] [int]          IDENTITY(1,1) NOT NULL,
    [equipment_id]         [int]          NOT NULL,
    [seq_no]               [int]          NOT NULL,
    [sop_id]               [int]          NOT NULL,
    [create_at]            [datetime2](7) NOT NULL CONSTRAINT [DF_equipment_product_create_at] DEFAULT (sysdatetime()),
    [update_at]            [datetime2](7) NOT NULL CONSTRAINT [DF_equipment_product_update_at] DEFAULT (sysdatetime()),
    CONSTRAINT [PK_equipment_product] PRIMARY KEY CLUSTERED ([equipment_product_id] ASC),
    CONSTRAINT [UQ_equipment_product_equipment_seq] UNIQUE NONCLUSTERED ([equipment_id] ASC, [seq_no] ASC)
);
GO

-- ----------------------------------------------------------------------------
-- Foreign keys
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_product_part')
ALTER TABLE [dbo].[product] ADD CONSTRAINT [FK_product_part]
    FOREIGN KEY ([part_id]) REFERENCES [dbo].[product_part] ([part_id]);
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_product_model')
ALTER TABLE [dbo].[product] ADD CONSTRAINT [FK_product_model]
    FOREIGN KEY ([model_id]) REFERENCES [dbo].[product_model] ([model_id]);
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_sop_checklist_product')
ALTER TABLE [dbo].[sop_checklist] ADD CONSTRAINT [FK_sop_checklist_product]
    FOREIGN KEY ([product_id]) REFERENCES [dbo].[product] ([product_id]);
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_sop_checklist_process')
ALTER TABLE [dbo].[sop_checklist] ADD CONSTRAINT [FK_sop_checklist_process]
    FOREIGN KEY ([process_id]) REFERENCES [dbo].[work_process] ([process_id]);
GO

-- sop_checklist_item -> sop_checklist : cascade so items are removed with the SOP
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_sop_checklist_item_sop')
ALTER TABLE [dbo].[sop_checklist_item] ADD CONSTRAINT [FK_sop_checklist_item_sop]
    FOREIGN KEY ([sop_id]) REFERENCES [dbo].[sop_checklist] ([sop_id]) ON DELETE CASCADE;
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_sop_checklist_item_material')
ALTER TABLE [dbo].[sop_checklist_item] ADD CONSTRAINT [FK_sop_checklist_item_material]
    FOREIGN KEY ([material_id]) REFERENCES [dbo].[material] ([material_id]);
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_equipment_product_equipment')
ALTER TABLE [dbo].[equipment_product] ADD CONSTRAINT [FK_equipment_product_equipment]
    FOREIGN KEY ([equipment_id]) REFERENCES [dbo].[equipment] ([equipment_id]);
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_equipment_product_sop')
ALTER TABLE [dbo].[equipment_product] ADD CONSTRAINT [FK_equipment_product_sop]
    FOREIGN KEY ([sop_id]) REFERENCES [dbo].[sop_checklist] ([sop_id]);
GO

-- ----------------------------------------------------------------------------
-- migration history
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [_migration_history] WHERE [version] = 'V002')
    INSERT INTO [_migration_history] ([version], [description])
    VALUES ('V002', 'add product / sop / equipment_product schema');
GO
