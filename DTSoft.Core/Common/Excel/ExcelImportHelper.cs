using DTSoft.Models.Parameter.DynamicApp;

namespace DTSoft.Core.Common.Excel
{
    /// <summary>
    /// Excel导入工具类
    /// </summary>
    public static class ExcelImportHelper
    {
        /// <summary>
        /// 从Excel导入数据
        /// </summary>
        /// <param name="fileStream">Excel文件流</param>
        /// <returns>字典数据列表</returns>
        public static async Task<List<Dictionary<string, object>>> ImportFromExcelAsync(Stream fileStream)
        {
            // 使用MiniExcel查询Excel数据，返回 List<IDictionary<string, object>>
            var data = await MiniExcelLibs.MiniExcel.QueryAsync(fileStream);

            // 将读取的数据转换为 List<Dictionary<string, object>>
            var result = new List<Dictionary<string, object>>();
            if (data != null)
            {
                // 跳过第一行（列标题行）
                var dataWithoutHeader = data.Skip(1);

                foreach (IDictionary<string, object> row in dataWithoutHeader)
                {
                    if (row.Values.All(value => string.IsNullOrEmpty(value.ToString()))) // 跳过空行
                        continue;

                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in row)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                    result.Add(dict);
                }
            }

            return result;
        }

        /// <summary>
        /// 从Excel导入数据（使用字段配置，按列顺序映射）
        /// </summary>
        /// <param name="fileStream">Excel文件流</param>
        /// <param name="fields">字段配置列表</param>
        /// <returns>字典数据列表</returns>
        private static async Task<List<Dictionary<string, object>>> ImportFromExcelWithFieldConfigAsync(Stream fileStream, List<FieldConfig> fields)
        {
            // 使用MiniExcel查询Excel数据，返回 List<IDictionary<string, object>>
            var data = await MiniExcelLibs.MiniExcel.QueryAsync(fileStream);

            // 过滤掉字段配置中不需要导入的系统字段
            var importableFields = fields
                .Where(f => f.FieldName != "Id" && f.FieldName != "CreateTime" && f.FieldName != "UpdateTime")
                .ToList();

            // 将读取的数据转换为字典列表，按列顺序映射，跳过第一行（标题行）
            var result = new List<Dictionary<string, object>>();
            if (data != null)
            {
                // 跳过第一行（列标题行）
                var dataWithoutHeader = data.Skip(1);

                foreach (IDictionary<string, object> row in dataWithoutHeader)
                {
                    if (row.Values.All(value => string.IsNullOrEmpty(value.ToString()))) // 跳过空行
                        continue;

                    var dict = new Dictionary<string, object>();

                    // 按顺序映射列：Excel的第一列对应配置的第一个字段，第二列对应第二个字段，以此类推
                    var values = row.Values.ToList();
                    for (int i = 0; i < importableFields.Count && i < values.Count; i++)
                    {
                        var fieldConfig = importableFields[i];
                        var value = (i < values.Count) ? values[i] : null;

                        // 处理空值情况
                        if (value == null || string.IsNullOrEmpty(value.ToString()))
                        {
                            // 如果字段不是必填的，设置为空字符串或其他默认值
                            if (!fieldConfig.Required)
                            {
                                // 根据字段类型设置适当的默认值，避免数据库的非空约束
                                dict[fieldConfig.FieldName] = GetDefaultValueByFieldType(fieldConfig.FieldType);
                            }
                            else
                            {
                                // 如果是必填字段但为空，则使用默认值
                                dict[fieldConfig.FieldName] = GetDefaultValueByFieldType(fieldConfig.FieldType);
                            }
                        }
                        else
                        {
                            dict[fieldConfig.FieldName] = value;
                        }
                    }

                    result.Add(dict);
                }
            }

            return result;
        }

        /// <summary>
        /// 从Excel导入数据并转换为指定类型
        /// </summary>
        /// <param name="fileStream">Excel文件流</param>
        /// <param name="fields">字段配置列表</param>
        /// <returns>转换后的数据列表</returns>
        public static async Task<List<Dictionary<string, object>>> ImportAndValidateDataAsync(Stream fileStream, List<FieldConfig> fields)
        {
            // 首先导入数据
            var importedData = await ImportFromExcelWithFieldConfigAsync(fileStream, fields);

            // 验证数据（根据字段配置）
            var validatedData = new List<Dictionary<string, object>>();
            foreach (var row in importedData)
            {
                var validatedRow = new Dictionary<string, object?>();
                bool isValid = true;
                var validationErrors = new List<string>();

                foreach (var kvp in row)
                {
                    // 根据字段配置验证数据
                    var fieldConfig = fields.FirstOrDefault(f => f.FieldName == kvp.Key);
                    if (fieldConfig != null)
                    {
                        // 验证必填字段
                        if (fieldConfig.Required && (string.IsNullOrEmpty(kvp.Value.ToString()) || (kvp.Value.ToString() == "")))
                        {
                            validationErrors.Add($"字段 '{fieldConfig.Label}' 是必填项，不能为null或空");
                            isValid = false;
                        }
                        else
                        {
                            // 根据字段类型转换值
                            var convertedValue = ConvertValueByFieldType(kvp.Value, fieldConfig.FieldType);
                            validatedRow[kvp.Key] = convertedValue;
                        }
                    }
                    else
                    {
                        // 如果没有找到字段配置，直接添加
                        validatedRow[kvp.Key] = kvp.Value;
                    }
                }

                // 如果验证失败，抛出异常
                if (!isValid)
                {
                    throw new Exception($"数据验证失败: {string.Join("; ", validationErrors)}");
                }

                validatedData.Add(validatedRow!);
            }

            return validatedData;
        }

        /// <summary>
        /// 根据字段类型获取默认值
        /// </summary>
        /// <param name="fieldType">字段类型</param>
        /// <returns>默认值</returns>
        private static object GetDefaultValueByFieldType(string fieldType)
        {
            switch (fieldType.ToLower())
            {
                case "string":
                case "text":
                case "textarea":
                    return string.Empty; // 空字符串而不是null
                case "number":
                case "int":
                case "integer":
                    return 0;
                case "decimal":
                case "float":
                case "double":
                    return 0.0;
                case "boolean":
                case "bool":
                    return false;
                case "date":
                case "datetime":
                    return DateTime.Now;
                default:
                    return string.Empty; // 默认返回空字符串
            }
        }

        /// <summary>
        /// 根据字段类型转换值
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="fieldType">字段类型</param>
        /// <returns>转换后的值</returns>
        private static object? ConvertValueByFieldType(object? value, string fieldType)
        {
            if (value == null) return string.Empty; // 返回空字符串而不是null

            var stringValue = value.ToString();

            // 根据字段类型进行转换
            switch (fieldType.ToLower())
            {
                case "string":
                case "text":
                case "textarea":
                    return stringValue;
                case "number":
                case "int":
                case "integer":
                    if (int.TryParse(stringValue, out int intValue))
                        return intValue;
                    else
                        return stringValue; // 如果转换失败，返回原始字符串
                case "decimal":
                case "float":
                case "double":
                    if (double.TryParse(stringValue, out double doubleValue))
                        return doubleValue;
                    else
                        return stringValue;
                case "boolean":
                case "bool":
                    if (bool.TryParse(stringValue, out bool boolValue))
                        return boolValue;
                    else
                        return stringValue!.ToLower() == "true" || stringValue == "1";
                case "date":
                case "datetime":
                    if (DateTime.TryParse(stringValue, out DateTime dateValue))
                        return dateValue;
                    else
                        return stringValue;
                default:
                    return stringValue;
            }
        }
    }
}
