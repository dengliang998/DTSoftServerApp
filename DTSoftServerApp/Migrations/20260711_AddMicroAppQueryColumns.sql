-- 为微应用配置表补齐搜索字段每行列数字段。

IF COL_LENGTH('dbo.sys_microappconfig', 'QueryColumns') IS NULL
BEGIN
    ALTER TABLE [dbo].[sys_microappconfig]
    ADD [QueryColumns] INT NOT NULL CONSTRAINT [DF_sys_microappconfig_QueryColumns] DEFAULT 1;
END
