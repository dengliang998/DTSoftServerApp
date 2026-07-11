using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities
{
    /// <summary>
    /// 微应用配置表
    /// </summary>
    [Table("sys_microappconfig")]
    public class SysMicroAppConfig
    {
        /// <summary>
        /// 配置 ID
        /// </summary>
        [Key]
        [Column("ID")]
        public long ItemId { get; init; }

        /// <summary>
        /// 配置名称
        /// </summary>
        [Column("ConfigName")]
        [StringLength(50)]
        public required string ConfigName { get; set; }

        /// <summary>
        /// 数据模型名称
        /// </summary>
        [Column("ModelName")]
        [StringLength(50)]
        public required string ModelName { get; set; }

        /// <summary>
        /// 配置描述
        /// </summary>
        [Column("ConfigDesc")]
        [StringLength(200)]
        public string? ConfigDesc { get; set; }

        /// <summary>
        /// 状态，1-启用，0-禁用
        /// </summary>
        [Column("Status")]
        public int Status { get; set; }

        /// <summary>
        /// 是否支持新增数据
        /// </summary>
        [Column("SupportCreate")]
        public bool SupportCreate { get; set; }

        /// <summary>
        /// 是否支持修改数据
        /// </summary>
        [Column("SupportUpdate")]
        public bool SupportUpdate { get; set; }

        /// <summary>
        /// 是否支持删除数据
        /// </summary>
        [Column("SupportDelete")]
        public bool SupportDelete { get; set; }

        /// <summary>
        /// 是否支持批量删除数据
        /// </summary>
        [Column("SupportBatchDelete")]
        public bool SupportBatchDelete { get; set; }

        /// <summary>
        /// 是否支持导入数据
        /// </summary>
        [Column("SupportImport")]
        public bool SupportImport { get; set; }

        /// <summary>
        /// 是否支持导出数据
        /// </summary>
        [Column("SupportExport")]
        public bool SupportExport { get; set; }

        /// <summary>
        /// 数据权限范围，all-全部数据，self-本人数据，department-部门数据
        /// </summary>
        [Column("DataScope")]
        [StringLength(20)]
        public string? DataScope { get; set; }

        /// <summary>
        /// 数据表单每行列数
        /// </summary>
        [Column("FormColumns")]
        public int FormColumns { get; set; } = 1;

        /// <summary>
        /// 搜索字段每行列数
        /// </summary>
        [Column("QueryColumns")]
        public int QueryColumns { get; set; } = 1;

        /// <summary>
        /// 接口前缀
        /// </summary>
        [Column("ApiPrefix")]
        [StringLength(100)]
        public string? ApiPrefix { get; set; }

        /// <summary>
        /// 字段配置列表(JSON格式)
        /// </summary>
        [Column("Fields")]
        public string? Fields { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("CreateTime")]
        public DateTime CreateTime { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [Column("UpdateTime")]
        public DateTime UpdateTime { get; set; }
    }
}
