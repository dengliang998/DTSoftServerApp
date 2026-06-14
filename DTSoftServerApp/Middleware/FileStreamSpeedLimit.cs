namespace DTSoftServerApp.Middleware;

/// <summary>
/// Read改造-实现限速
/// 注意：此实现已弃用，请使用标准FileStream
/// </summary>
[Obsolete("Use standard FileStream instead")]
public class FileStreamSpeedLimit : FileStream
{
    /// <summary>
    /// 限制读取速度-KB
    /// </summary>
    private readonly int _speed;

    /// <summary>
    /// FileStreamSpeedLimit
    /// </summary>
    /// <param name="path">path</param>
    /// <param name="mode">mode</param>
    /// <param name="access">access</param>
    public FileStreamSpeedLimit(string path, FileMode mode, FileAccess access) : base(path, mode, access)
    {
        _speed = 256;
    }

    /// <summary>
    /// Read
    /// </summary>
    /// <param name="buffer">buffer</param>
    /// <param name="offset">offset</param>
    /// <param name="count">count</param>
    /// <returns></returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        Task.Delay((int)(100 / (_speed / 512d)));
        return base.Read(buffer, offset, count);
    }
}