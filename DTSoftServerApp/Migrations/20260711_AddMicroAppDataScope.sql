-- 为微应用配置表补齐数据权限范围字段。
-- all：全部数据，self：本人创建的数据，department：同部门用户创建的数据。

IF COL_LENGTH('dbo.sys_microappconfig', 'DataScope') IS NULL
BEGIN
    ALTER TABLE [dbo].[sys_microappconfig]
    ADD [DataScope] NVARCHAR(20) NULL CONSTRAINT [DF_sys_microappconfig_DataScope] DEFAULT 'all';
END
