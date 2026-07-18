-- 创建 ESB 服务连接与数据源配置表。

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sys_esb_serviceconnection]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[sys_esb_serviceconnection] (
        [ItemId] BIGINT NOT NULL PRIMARY KEY,
        [Code] NVARCHAR(100) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [ServiceType] NVARCHAR(20) NOT NULL,
        [DbType] NVARCHAR(50) NULL,
        [ConnectionString] NVARCHAR(MAX) NULL,
        [WebApiConfig] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL DEFAULT 1,
        [TimeoutSeconds] INT NOT NULL DEFAULT 30,
        [Remark] NVARCHAR(500) NULL,
        [CreateTime] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdateTime] DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX [IX_sys_esb_serviceconnection_Code] ON [dbo].[sys_esb_serviceconnection]([Code]);
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sys_esb_datasource]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[sys_esb_datasource] (
        [ItemId] BIGINT NOT NULL PRIMARY KEY,
        [Code] NVARCHAR(100) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [ConnectionId] BIGINT NULL,
        [SourceType] NVARCHAR(20) NOT NULL,
        [ExecuteMode] NVARCHAR(20) NOT NULL,
        [SqlText] NVARCHAR(MAX) NULL,
        [HttpConfig] NVARCHAR(MAX) NULL,
        [ParameterConfig] NVARCHAR(MAX) NULL,
        [ResultMapping] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL DEFAULT 1,
        [MaxRows] INT NOT NULL DEFAULT 500,
        [TimeoutSeconds] INT NOT NULL DEFAULT 30,
        [Remark] NVARCHAR(500) NULL,
        [CreateTime] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdateTime] DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX [IX_sys_esb_datasource_Code] ON [dbo].[sys_esb_datasource]([Code]);
END
