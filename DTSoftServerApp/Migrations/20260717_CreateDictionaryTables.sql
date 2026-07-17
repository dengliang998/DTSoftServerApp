-- 创建数据字典表。
-- sys_dictionary_type：字典分类
-- sys_dictionary_data：字典项

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sys_dictionary_type]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[sys_dictionary_type] (
        [ItemId] BIGINT NOT NULL PRIMARY KEY,
        [DictCode] NVARCHAR(100) NOT NULL,
        [DictName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [Enabled] BIT NOT NULL DEFAULT 1,
        [Sort] INT NOT NULL DEFAULT 0,
        [CreateTime] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdateTime] DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX [IX_sys_dictionary_type_DictCode] ON [dbo].[sys_dictionary_type]([DictCode]);
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sys_dictionary_data]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[sys_dictionary_data] (
        [ItemId] BIGINT NOT NULL PRIMARY KEY,
        [DictTypeId] BIGINT NOT NULL,
        [DictCode] NVARCHAR(100) NOT NULL,
        [ItemLabel] NVARCHAR(100) NOT NULL,
        [ItemValue] NVARCHAR(200) NOT NULL,
        [TagType] NVARCHAR(50) NULL,
        [Remark] NVARCHAR(500) NULL,
        [Enabled] BIT NOT NULL DEFAULT 1,
        [Sort] INT NOT NULL DEFAULT 0,
        [CreateTime] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdateTime] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_sys_dictionary_data_sys_dictionary_type_DictTypeId]
            FOREIGN KEY ([DictTypeId]) REFERENCES [dbo].[sys_dictionary_type]([ItemId]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_sys_dictionary_data_DictCode_ItemValue] ON [dbo].[sys_dictionary_data]([DictCode], [ItemValue]);
    CREATE INDEX [IX_sys_dictionary_data_DictCode] ON [dbo].[sys_dictionary_data]([DictCode]);
    CREATE INDEX [IX_sys_dictionary_data_Enabled] ON [dbo].[sys_dictionary_data]([Enabled]);
END

-- 初始化“系统管理 / 数据字典”菜单，并给 Administrator 角色授权。
DECLARE @SystemManagementMenuId BIGINT;
DECLARE @DictionaryMenuId BIGINT;
DECLARE @AdministratorRoleId BIGINT;

SELECT TOP 1 @SystemManagementMenuId = [ItemId]
FROM [dbo].[sys_menu]
WHERE [MenuName] = N'系统管理'
  AND ([MenuPath] IS NULL OR [MenuPath] = N'');

SELECT TOP 1 @AdministratorRoleId = [ItemId]
FROM [dbo].[sys_role]
WHERE [RoleName] = N'Administrator';

IF @SystemManagementMenuId IS NOT NULL
BEGIN
    SELECT TOP 1 @DictionaryMenuId = [ItemId]
    FROM [dbo].[sys_menu]
    WHERE [MenuPath] = N'common/dictionaries';

    IF @DictionaryMenuId IS NULL
    BEGIN
        SET @DictionaryMenuId =
            DATEDIFF_BIG(MILLISECOND, CONVERT(datetime2, '2020-01-01'), SYSUTCDATETIME()) * 1000
            + ABS(CHECKSUM(NEWID())) % 1000;

        INSERT INTO [dbo].[sys_menu] (
            [ItemId],
            [Pid],
            [MenuName],
            [MenuPath],
            [Order],
            [Icon],
            [IsHidden],
            [MType]
        )
        VALUES (
            @DictionaryMenuId,
            @SystemManagementMenuId,
            N'数据字典',
            N'common/dictionaries',
            0,
            N'Collection',
            0,
            0
        );
    END

    IF @AdministratorRoleId IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM [dbo].[sys_menuauthority]
           WHERE [RoleID] = @AdministratorRoleId
             AND [MenuID] = @DictionaryMenuId
       )
    BEGIN
        INSERT INTO [dbo].[sys_menuauthority] (
            [ItemId],
            [RoleID],
            [MenuID]
        )
        VALUES (
            DATEDIFF_BIG(MILLISECOND, CONVERT(datetime2, '2020-01-01'), SYSUTCDATETIME()) * 1000
                + ABS(CHECKSUM(NEWID())) % 1000,
            @AdministratorRoleId,
            @DictionaryMenuId
        );
    END
END
