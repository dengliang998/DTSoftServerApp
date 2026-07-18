using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.MicroApp
{
    #region 请求参数

    /// <summary>
    /// 微应用配置查询请求参数
    /// </summary>
    public class MicroConfigQueryParameter
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
    /// 微应用配置添加请求参数
    /// </summary>
    public class MicroConfigAddParameter
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
        /// 数据权限范围，all-全部数据，self-本人数据，department-部门数据
        /// </summary>
        public string? DataScope { get; set; }

        /// <summary>
        /// 数据表单每行列数，1-4
        /// </summary>
        public int? FormColumns { get; set; }

        /// <summary>
        /// 搜索字段每行列数，1-4
        /// </summary>
        public int? QueryColumns { get; set; }

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
    /// 微应用配置更新请求参数
    /// </summary>
    public class MicroConfigUpdateParameter
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
        /// 数据权限范围，all-全部数据，self-本人数据，department-部门数据
        /// </summary>
        public string? DataScope { get; set; }

        /// <summary>
        /// 数据表单每行列数，1-4
        /// </summary>
        public int? FormColumns { get; set; }

        /// <summary>
        /// 搜索字段每行列数，1-4
        /// </summary>
        public int? QueryColumns { get; set; }

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
    /// 微应用配置删除请求参数
    /// </summary>
    public class MicroConfigDeleteParameter
    {
        /// <summary>
        /// 配置 ID
        /// </summary>
        [Required]
        public long ItemId { get; set; }
    }

    /// <summary>
    /// 微应用数据字段查询条件
    /// </summary>
    public class MicroQueryFilter
    {
        /// <summary>
        /// 字段标识
        /// </summary>
        public string? FieldName { get; set; }

        /// <summary>
        /// 查询模式，exact/fuzzy/range
        /// </summary>
        public string? Mode { get; set; }

        /// <summary>
        /// 精确或模糊查询值
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// 范围起始值
        /// </summary>
        public object? StartValue { get; set; }

        /// <summary>
        /// 范围结束值
        /// </summary>
        public object? EndValue { get; set; }
    }

    /// <summary>
    /// 微应用数据批量删除参数
    /// </summary>
    public class MicroBatchDeleteParameter
    {
        /// <summary>
        /// 数据 ID 列表
        /// </summary>
        [Required]
        public required List<long> Ids { get; set; }
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
        /// 列宽，单位 px
        /// </summary>
        public int? ColumnWidth { get; set; }

        /// <summary>
        /// 是否支持排序
        /// </summary>
        public bool Sortable { get; set; }

        /// <summary>
        /// 固定列位置，none/left/right
        /// </summary>
        public string? Fixed { get; set; }

        /// <summary>
        /// 查询模式，none/exact/fuzzy/range
        /// </summary>
        public string? QueryMode { get; set; }

        /// <summary>
        /// 查询控件宽度，单位 px
        /// </summary>
        public int? QueryWidth { get; set; }

        /// <summary>
        /// 日期格式，year-年，month-年月，date-年月日，datetime-时间
        /// </summary>
        public string? DateFormat { get; set; }

        /// <summary>
        /// 字段显示顺序
        /// </summary>
        public int? SortOrder { get; set; }

        /// <summary>
        /// 最小文本长度
        /// </summary>
        public int? MinLength { get; set; }

        /// <summary>
        /// 最大文本长度
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// 数据库存储长度，仅用于短文本字段
        /// </summary>
        public int? ColumnLength { get; set; }

        /// <summary>
        /// 最小数值
        /// </summary>
        public decimal? MinValue { get; set; }

        /// <summary>
        /// 最大数值
        /// </summary>
        public decimal? MaxValue { get; set; }

        /// <summary>
        /// 正则表达式
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// 字段默认值
        /// </summary>
        public required string DefaultValue { get; set; }

        /// <summary>
        /// 选项列表，用于select等类型字段
        /// </summary>
        public List<OptionItem>? Options { get; set; }

        /// <summary>
        /// 选项来源，manual-手动维护，dictionary-数据字典，esb-ESB数据源
        /// </summary>
        public string? OptionSource { get; set; }

        /// <summary>
        /// 字典编码，当选项来源为数据字典时使用
        /// </summary>
        public string? DictCode { get; set; }

        /// <summary>
        /// ESB 数据源编码，当选项来源为 ESB 时使用。
        /// </summary>
        public string? EsbDataSourceCode { get; set; }

        /// <summary>
        /// ESB 选项显示字段。
        /// </summary>
        public string? EsbLabelField { get; set; }

        /// <summary>
        /// ESB 选项值字段。
        /// </summary>
        public string? EsbValueField { get; set; }

        /// <summary>
        /// ESB 参数 JSON。
        /// </summary>
        public string? EsbParams { get; set; }
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
    /// 微应用配置响应数据
    /// </summary>
    public class MicroConfigResponse
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
        /// 数据权限范围，all-全部数据，self-本人数据，department-部门数据
        /// </summary>
        public string? DataScope { get; set; }

        /// <summary>
        /// 数据表单每行列数，1-4
        /// </summary>
        public int FormColumns { get; set; }

        /// <summary>
        /// 搜索字段每行列数，1-4
        /// </summary>
        public int QueryColumns { get; set; }

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
