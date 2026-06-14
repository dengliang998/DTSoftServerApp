using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using System.Threading.Channels;

namespace DTSoftServerApp.Services
{
    /// <summary>
    /// 异步日志队列服务
    /// 使用 Channel 实现高性能的日志批量写入
    /// </summary>
    public interface ILogQueueService
    {
        /// <summary>
        /// 将日志添加到队列
        /// </summary>
        void Enqueue(SysActionLog logEntry);

        /// <summary>
        /// 启动后台处理
        /// </summary>
        void StartProcessing(CancellationToken stoppingToken);
    }

    /// <summary>
    /// 异步日志队列服务实现
    /// </summary>
    public class LogQueueService : BackgroundService, ILogQueueService
    {
        private readonly Channel<SysActionLog> _logChannel;
        private readonly ILogger<LogQueueService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private const int BatchSize = 100; // 批量写入大小
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5); // 最大刷新间隔

        public LogQueueService(ILogger<LogQueueService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            
            // 创建有界 Channel，容量为 10000
            var options = new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.DropOldest, // 队列满时丢弃最旧的日志
                SingleReader = true, // 单消费者模式
                SingleWriter = false // 多生产者模式
            };
            
            _logChannel = Channel.CreateBounded<SysActionLog>(options);
        }

        /// <summary>
        /// 将日志添加到队列
        /// </summary>
        public void Enqueue(SysActionLog logEntry)
        {
            if (!_logChannel.Writer.TryWrite(logEntry))
            {
                // 队列已满，记录警告
                _logger.LogWarning("日志队列已满，丢弃日志：{ActionName}", logEntry.ActionName);
            }
        }

        /// <summary>
        /// 启动后台处理
        /// </summary>
        public void StartProcessing(CancellationToken stoppingToken)
        {
            _ = ProcessLogsAsync(stoppingToken);
        }

        /// <summary>
        /// 后台处理日志队列
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ProcessLogsAsync(stoppingToken);
        }

        private async Task ProcessLogsAsync(CancellationToken stoppingToken)
        {
            var batch = new List<SysActionLog>(BatchSize);
            var flushTimer = new PeriodicTimer(FlushInterval);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    batch.Clear();

                    // 读取日志或使用定时器刷新
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeoutCts.CancelAfter(Timeout.Infinite); // 默认无限等待

                    while (batch.Count < BatchSize)
                    {
                        try
                        {
                            // 尝试从 Channel 读取日志
                            if (await _logChannel.Reader.WaitToReadAsync(stoppingToken))
                            {
                                if (_logChannel.Reader.TryRead(out var logEntry))
                                {
                                    batch.Add(logEntry);
                                }
                            }
                            else
                            {
                                break; // Channel 已关闭或取消
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            goto FLUSH; // 服务停止，跳出循环
                        }
                    }

                    // 如果批次为空，等待定时器触发
                    if (batch.Count == 0)
                    {
                        try
                        {
                            await flushTimer.WaitForNextTickAsync(stoppingToken);
                            continue;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            break; // 服务停止
                        }
                    }

                    FLUSH:
                    // 批量写入数据库
                    if (batch.Count > 0)
                    {
                        await FlushLogsAsync(batch);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "日志队列处理异常");
            }
            finally
            {
                flushTimer.Dispose();
                
                // 服务停止时，清空剩余日志
                await FlushRemainingLogsAsync();
            }
        }

        /// <summary>
        /// 批量写入日志到数据库
        /// </summary>
        private async Task FlushLogsAsync(List<SysActionLog> logs)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SysDbContext>();

                // 批量添加日志
                foreach (var log in logs)
                {
                    dbContext.SysActionLog?.Add(log);
                }

                await dbContext.SaveChangesAsync();
                
                _logger.LogDebug("成功写入 {Count} 条日志", logs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量写入日志失败，共 {Count} 条", logs.Count);
            }
        }

        /// <summary>
        /// 服务停止时清空剩余日志
        /// </summary>
        private async Task FlushRemainingLogsAsync()
        {
            var remainingLogs = new List<SysActionLog>();
            
            while (_logChannel.Reader.TryRead(out var log))
            {
                remainingLogs.Add(log);
            }

            if (remainingLogs.Count > 0)
            {
                _logger.LogInformation("服务停止前写入剩余的 {Count} 条日志", remainingLogs.Count);
                await FlushLogsAsync(remainingLogs);
            }
        }

        /// <summary>
        /// 获取当前队列长度
        /// </summary>
        public int QueueLength => _logChannel.Reader.Count;
    }
}
