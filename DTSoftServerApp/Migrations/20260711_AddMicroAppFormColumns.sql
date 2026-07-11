-- 为微应用配置表补齐数据表单每行列数字段。

IF COL_LENGTH('dbo.sys_microappconfig', 'FormColumns') IS NULL
BEGIN
    ALTER TABLE [dbo].[sys_microappconfig]
    ADD [FormColumns] INT NOT NULL CONSTRAINT [DF_sys_microappconfig_FormColumns] DEFAULT 1;
END
