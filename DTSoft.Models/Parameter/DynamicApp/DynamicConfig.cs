using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.DynamicApp
{
    #region 请求参数

    /// <summary>
    /// CRUD配置查询请求参数
    /// </summary>
    public class CrudConfigQueryParameter
    {
        /// <summary>
        /// 搜索关键词
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// 模块名称
        /// </summary>
        public string? ModelName { get; set; }

        /// <summary>
        /// 微应用路径
        /// </summary>
        public string? MicroAppPath { get; set; }

        /// <summary>
        /// 页码
        /// </summary>
        public int? PageNum { get; set; }

        /// <summary>
        /// 每页条数
        /// </summary>
        public int? PageSize { get; set; }
    }

    /// <summary>
    /// CRUD配置添加请求参数
    /// </summary>
    public class CrudConfigAddParameter
    {
        /// <summary>
        /// 配置名称
        /// </summary>
        [Required]
        [StringLength(20)]
        public required string ConfigName { get; set; }

        /// <summary>
        /// 数据模型名称
        /// </summary>
        [Required]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "模型名称只能包含英文、数字和下划线，且以英文开头")]
        public required string ModelName { get; set; }

        /// <summary>
        /// 配置描述
        /// </summary>
        public required string ConfigDesc { get; set; }

        /// <summary>
        /// 状态，1-启用，0-禁用
        /// </summary>
        [Required]
        public int Status { get; set; }

        /// <summary>
        /// 是否支持新增数据
        /// </summary>
        [Required]
        public bool SupportCreate { get; set; }

        /// <summary>
        /// 是否支持修改数据
        /// </summary>
        [Required]
        public bool SupportUpdate { get; set; }

        /// <summary>
        /// 是否支持删除数据
        /// </summary>
        [Required]
        public bool SupportDelete { get; set; }

        /// <summary>
        /// 是否支持批量删除数据
        /// </summary>
        [Required]
        public bool SupportBatchDelete { get; set; }

        /// <summary>
        /// 是否支持导入数据
        /// </summary>
        [Required]
        public bool SupportImport { get; set; }

        /// <summary>
        /// 是否支持导出数据
        /// </summary>
        [Required]
        public bool SupportExport { get; set; }

        /// <summary>
        /// 微应用路径
        /// </summary>
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "微应用路径只能包含英文、数字、中划线和下划线，且以英文开头")]
        public string? MicroAppPath { get; set; }

        /// <summary>
        /// 字段配置列表
        /// </summary>
        [Required]
        public required List<FieldConfig> Fields { get; set; }
    }

    /// <summary>
    /// CRUD配置更新请求参数
    /// </summary>
    public class CrudConfigUpdateParameter
    {
        /// <summary>
        /// 配置 ID
        /// </summary>
        [Required]
        public long ItemId { get; set; }

        /// <summary>
        /// 配置名称
        /// </summary>
        [Required]
        [StringLength(20)]
        public required string ConfigName { get; set; }

        /// <summary>
        /// 数据模型名称
        /// </summary>
        [Required]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "模型名称只能包含英文、数字和下划线，且以英文开头")]
        public required string ModelName { get; set; }

        /// <summary>
        /// 配置描述
        /// </summary>
        public string? ConfigDesc { get; set; }

        /// <summary>
        /// 状态，1-启用，0-禁用
        /// </summary>
        [Required]
        public int Status { get; set; }

        /// <summary>
        /// 是否支持新增数据
        /// </summary>
        [Required]
        public bool SupportCreate { get; set; }

        /// <summary>
        /// 是否支持修改数据
        /// </summary>
        [Required]
        public bool SupportUpdate { get; set; }

        /// <summary>
        /// 是否支持删除数据
        /// </summary>
        [Required]
        public bool SupportDelete { get; set; }

        /// <summary>
        /// 是否支持批量删除数据
        /// </summary>
        [Required]
        public bool SupportBatchDelete { get; set; }

        /// <summary>
        /// 是否支持导入数据
        /// </summary>
        [Required]
        public bool SupportImport { get; set; }

        /// <summary>
        /// 是否支持导出数据
        /// </summary>
        [Required]
        public bool SupportExport { get; set; }

        /// <summary>
        /// 微应用路径
        /// </summary>
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "微应用路径只能包含英文、数字、中划线和下划线，且以英文开头")]
        public string? MicroAppPath { get; set; }

        /// <summary>
        /// 字段配置列表
        /// </summary>
        [Required]
        public required List<FieldConfig> Fields { get; set; }
    }

    /// <summary>
    /// CRUD配置删除请求参数
    /// </summary>
    public class CrudConfigDeleteParameter
    {
        /// <summary>
        /// 配置 ID
        /// </summary>
        [Required]
        public long ItemId { get; set; }
    }

    /// <summary>
    /// 字段配置对象
    /// </summary>
    public class FieldConfig
    {
        /// <summary>
        /// 字段显示名称
        /// </summary>
        [Required]
        public required string Label { get; set; }

        /// <summary>
        /// 字段标识
        /// </summary>
        [Required]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "字段标识只能包含英文、数字和下划线，且以英文开头")]
        public required string FieldName { get; set; }

        /// <summary>
        /// 字段类型
        /// </summary>
        [Required]
        public required string FieldType { get; set; }

        /// <summary>
        /// 是否必填
        /// </summary>
        [Required]
        public bool Required { get; set; }

        /// <summary>
        /// 是否显示在列表中
        /// </summary>
        [Required]
        public bool ShowInList { get; set; }

        /// <summary>
        /// 是否可编辑
        /// </summary>
        [Required]
        public bool Editable { get; set; }

        /// <summary>
        /// 字段验证规则，JSON格式
        /// </summary>
        public required string Validation { get; set; }

        /// <summary>
        /// 字段默认值
        /// </summary>
        public required string DefaultValue { get; set; }

        /// <summary>
        /// 选项列表，用于select等类型字段
        /// </summary>
        public List<OptionItem>? Options { get; set; }
    }

    /// <summary>
    /// 选项项
    /// </summary>
    public class OptionItem
    {
        /// <summary>
        /// 选项显示文本
        /// </summary>
        public required string Label { get; set; }

        /// <summary>
        /// 选项值
        /// </summary>
        public required string Value { get; set; }
    }

    #endregion

    #region 响应数据

    /// <summary>
    /// CRUD配置响应数据
    /// </summary>
    public class CrudConfigResponse
    {
        /// <summary>
        /// 配置ID
        /// </summary>
        public long ItemId { get; set; }

        /// <summary>
        /// 配置名称
        /// </summary>
        public required string ConfigName { get; set; }

        /// <summary>
        /// 数据模型名称
        /// </summary>
        public required string ModelName { get; set; }

        /// <summary>
        /// 配置描述
        /// </summary>
        public string? ConfigDesc { get; set; }

        /// <summary>
        /// 状态，1-启用，0-禁用
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 是否支持新增数据
        /// </summary>
        public bool SupportCreate { get; set; }

        /// <summary>
        /// 是否支持修改数据
        /// </summary>
        public bool SupportUpdate { get; set; }

        /// <summary>
        /// 是否支持删除数据
        /// </summary>
        public bool SupportDelete { get; set; }

        /// <summary>
        /// 是否支持批量删除数据
        /// </summary>
        public bool SupportBatchDelete { get; set; }

        /// <summary>
        /// 是否支持导入数据
        /// </summary>
        public bool SupportImport { get; set; }

        /// <summary>
        /// 是否支持导出数据
        /// </summary>
        public bool SupportExport { get; set; }

        /// <summary>
        /// 微应用路径
        /// </summary>
        public string? MicroAppPath { get; set; }

        /// <summary>
        /// 字段配置列表
        /// </summary>
        public List<FieldConfig>? Fields { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>
    /// 分页响应数据
    /// </summary>
    public class PagedResponse<T>
    {
        /// <summary>
        /// 数据列表
        /// </summary>
        public required List<T> Data { get; init; }

        /// <summary>
        /// 总数
        /// </summary>
        public int Total { get; init; }
    }

    #endregion
}
