using DTSoft.Models.Parameter.MicroApp;
using MiniExcelLibs;
using System.Data;

namespace DTSoft.Core.Common.Excel
{
    /// <summary>
    /// Excel导出工具类
    /// </summary>
    public static class ExcelExportHelper
    {
        /// <summary>
        /// 将数据列表导出为Excel
        /// </summary>
        /// <param name="data">数据列表</param>
        /// <param name="fileName">文件名</param>
        /// <returns>Excel文件的字节数组</returns>
        public static async Task<byte[]> ExportToExcel<T>(List<T> data, string fileName = "data.xlsx")
        {
            using var memoryStream = new MemoryStream();

            // 将对象列表转换为DataTable以便导出
            var dataTable = ConvertToDataTable(data);

            // 使用MiniExcel导出到内存流
            await memoryStream.SaveAsAsync(dataTable);

            return memoryStream.ToArray();
        }

        /// <summary>
        /// 将字典列表导出为Excel
        /// </summary>
        /// <param name="data">字典数据列表</param>
        /// <param name="fileName">文件名</param>
        /// <returns>Excel文件的字节数组</returns>
        public static async Task<byte[]> ExportDictionaryToExcelAsync(List<Dictionary<string, object>> data, string fileName = "data.xlsx")
        {
            using var memoryStream = new MemoryStream();

            // 将字典列表转换为DataTable
            var dataTable = ConvertDictionaryToDataTable(data);

            // 使用MiniExcel导出到内存流
            await memoryStream.SaveAsAsync(dataTable);

            return memoryStream.ToArray();
        }

        /// <summary>
        /// 将字典列表导出为Excel（使用字段配置）
        /// </summary>
        /// <param name="data">字典数据列表</param>
        /// <param name="fields">字段配置列表</param>
        /// <param name="fileName">文件名</param>
        /// <returns>Excel文件的字节数组</returns>
        public static async Task<byte[]> ExportDictionaryToExcelWithFieldConfigAsync(List<Dictionary<string, object>> data, List<FieldConfig> fields, string fileName = "data.xlsx")
        {
            using var memoryStream = new MemoryStream();

            // 将字典列表转换为DataTable，并使用字段配置
            var dataTable = ConvertDictionaryToDataTableWithFieldConfig(data, fields);

            // 使用MiniExcel导出到内存流
            await memoryStream.SaveAsAsync(dataTable);

            return memoryStream.ToArray();
        }

        /// <summary>
        /// 将对象列表转换为DataTable
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="data">对象列表</param>
        /// <returns>DataTable</returns>
        private static DataTable ConvertToDataTable<T>(List<T>? data)
        {
            var dataTable = new DataTable();

            if (data == null || !data.Any())
                return dataTable;

            // 获取对象的属性信息
            var properties = typeof(T).GetProperties();

            // 添加列
            foreach (var prop in properties)
            {
                var columnType = GetColumnType(prop.PropertyType);
                dataTable.Columns.Add(prop.Name, columnType);
            }

            // 添加行
            foreach (var item in data)
            {
                var row = dataTable.NewRow();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    row[prop.Name] = value ?? DBNull.Value;
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        /// <summary>
        /// 将字典列表转换为DataTable
        /// </summary>
        /// <param name="data">字典数据列表</param>
        /// <returns>DataTable</returns>
        private static DataTable ConvertDictionaryToDataTable(List<Dictionary<string, object>>? data)
        {
            var dataTable = new DataTable();

            if (data == null || !data.Any())
                return dataTable;

            // 添加列 - 使用第一个字典的键作为列名
            var firstDict = data.First();
            foreach (var key in firstDict.Keys)
            {
                // 尝试根据值的类型确定列类型
                var sampleValue = firstDict[key];
                var columnType = GetColumnTypeFromValue(sampleValue);
                dataTable.Columns.Add(key, columnType);
            }

            // 添加行
            foreach (var dict in data)
            {
                var row = dataTable.NewRow();
                foreach (var kvp in dict)
                {
                    if (dataTable.Columns.Contains(kvp.Key))
                    {
                        var value = kvp.Value;
                        row[kvp.Key] = value;
                    }
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        /// <summary>
        /// 将字典列表转换为DataTable（使用字段配置）
        /// </summary>
        /// <param name="data">字典数据列表</param>
        /// <param name="fields">字段配置列表</param>
        /// <returns>DataTable</returns>
        private static DataTable ConvertDictionaryToDataTableWithFieldConfig(List<Dictionary<string, object>>? data, List<FieldConfig> fields)
        {
            var dataTable = new DataTable();

            if (data == null || !data.Any())
                return dataTable;

            // 创建字段标识到显示名称的映射，并过滤掉系统字段
            var fieldMapping = fields
                .Where(f =>
                    f.FieldName != MicroTableSystemColumns.Id &&
                    f.FieldName != MicroTableSystemColumns.CreatedTime &&
                    f.FieldName != MicroTableSystemColumns.UpdatedTime &&
                    f.FieldName != MicroTableSystemColumns.CreatedBy &&
                    f.FieldName != MicroTableSystemColumns.UpdatedBy)
                .ToDictionary(f => f.FieldName, f => f.Label);

            // 添加列 - 使用字段显示名称作为列名
            foreach (var field in fields.Where(f =>
                         f.FieldName != MicroTableSystemColumns.Id &&
                         f.FieldName != MicroTableSystemColumns.CreatedTime &&
                         f.FieldName != MicroTableSystemColumns.UpdatedTime &&
                         f.FieldName != MicroTableSystemColumns.CreatedBy &&
                         f.FieldName != MicroTableSystemColumns.UpdatedBy))
            {
                // 获取第一个数据行中该字段的值类型作为列类型
                var sampleValue = data.FirstOrDefault()?[field.FieldName];
                var columnType = GetColumnTypeFromValue(sampleValue);
                dataTable.Columns.Add(field.Label, columnType); // 使用Label作为列名
            }

            // 添加行
            foreach (var dict in data)
            {
                var row = dataTable.NewRow();
                foreach (var kvp in dict)
                {
                    // 检查字段是否为系统字段，如果是则跳过
                    if (kvp.Key.Equals(MicroTableSystemColumns.Id, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals(MicroTableSystemColumns.CreatedTime, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals(MicroTableSystemColumns.UpdatedTime, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals(MicroTableSystemColumns.CreatedBy, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals(MicroTableSystemColumns.UpdatedBy, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 使用字段显示名称作为列名
                    if (fieldMapping.TryGetValue(kvp.Key, out var displayName))
                    {
                        if (dataTable.Columns.Contains(displayName))
                        {
                            var value = kvp.Value;
                            row[displayName] = value;
                        }
                    }
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        /// <summary>
        /// 根据类型获取列类型
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns>列类型</returns>
        private static Type GetColumnType(Type type)
        {
            // 处理可空类型
            type = Nullable.GetUnderlyingType(type) ?? type;

            // 基本数据类型映射
            if (type == typeof(string) || type == typeof(char))
                return typeof(string);
            if (type == typeof(int) || type == typeof(short) || type == typeof(long))
                return typeof(int);
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
                return typeof(double);
            if (type == typeof(bool))
                return typeof(bool);
            if (type == typeof(DateTime))
                return typeof(DateTime);
            if (type == typeof(byte))
                return typeof(byte);
            if (type == typeof(Guid))
                return typeof(Guid);

            // 默认返回字符串类型
            return typeof(string);
        }

        /// <summary>
        /// 根据值获取列类型
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>列类型</returns>
        private static Type GetColumnTypeFromValue(object? value)
        {
            if (value == null)
                return typeof(string);

            var type = value.GetType();
            return GetColumnType(type);
        }
    }
}
