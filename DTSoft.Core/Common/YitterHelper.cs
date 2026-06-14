using Yitter.IdGenerator;

namespace DTSoft.Core.Common;

/// <summary>
/// Yitter IdGenerator 配置帮助类
/// </summary>
public static class YitterHelper
{
    /// <summary>
    /// 初始化 IdGenerator（在应用程序启动时调用一次）
    /// </summary>
    /// <param name="workerId">工作机器 ID (0-255)</param>
    public static void Initialize(ushort workerId = 1)
    {
        // 创建 IdGeneratorOptions 对象，可自定义参数（如果不设置，就是默认值）
        var options = new IdGeneratorOptions(workerId)
        {
            WorkerIdBitLength = 8,         // 工作机器 ID 位数（默认 6），支持 2^6=64 台机器
            SeqBitLength = 6               // 序列数长度（默认 6），单台机器每毫秒生成 2^6=64 个 ID
        };

        // 保存参数（务必全局唯一实例，否则可能产生重复 ID）
        YitIdHelper.SetIdGenerator(options);
        
        Console.WriteLine($"✅ Yitter IdGenerator 初始化成功 (WorkerId={workerId})");
    }

    /// <summary>
    /// 生成新的 ID
    /// </summary>
    /// <returns>long 类型的唯一 ID</returns>
    public static long NewId()
    {
        return YitIdHelper.NextId();
    }

    /// <summary>
    /// 批量生成 ID
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>ID 数组</returns>
    public static long[] NewIds(int count)
    {
        var ids = new long[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = YitIdHelper.NextId();
        }
        return ids;
    }
}
