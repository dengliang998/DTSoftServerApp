using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data.Common;

namespace DTSoft.Plugin.Abstractions;

/// <summary>
/// 插件可访问的数据库上下文能力。
/// </summary>
public interface IPluginDbContext
{
    DatabaseFacade Database { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    DbConnection GetDbConnection();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
