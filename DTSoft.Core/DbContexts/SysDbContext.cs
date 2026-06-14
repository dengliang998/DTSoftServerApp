using DTSoft.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DTSoft.Core.DbContexts;

public class SysDbContext(DbContextOptions<SysDbContext> options) : DbContext(options)
{
    public virtual DbSet<SysUser> SysUser { get; set; }
    public virtual DbSet<SysAttachments>? Attachments { get; set; }
    public virtual DbSet<SysMenu> SysMenu { get; set; }
    public virtual DbSet<SysMenuAuthority>? SysMenuAuthority { get; set; }
    public virtual DbSet<SysRole>? SysRole { get; set; }
    public virtual DbSet<SysRoleMember>? SysRoleMember { get; set; }
    public virtual DbSet<SysSystemUrl>? SysSystemUrl { get; set; }
    public virtual DbSet<SysActionLog>? SysActionLog { get; set; }
    public virtual DbSet<SysResultLog>? SysResultLog { get; set; }
    public virtual DbSet<SysConfig>? SysConfig { get; set; }
    public virtual DbSet<SysDynamicAppConfig>? SysDynamicAppConfig { get; set; }
    public virtual DbSet<SysOu>? SysOu { get; set; }
    public virtual DbSet<SysUserMember>? SysUserMember { get; set; }
    public virtual DbSet<SysUserSupervisor>? SysUserSupervisor { get; set; }
    public virtual DbSet<SysApiKey>? SysApiKey { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isPostgreSql = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        // 配置所有表的主键为非自增
        modelBuilder.Entity<SysUser>().Property(p => p.Account).ValueGeneratedNever().HasMaxLength(50);
        modelBuilder.Entity<SysRole>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysMenu>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysAttachments>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysConfig>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysActionLog>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysResultLog>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysRoleMember>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysMenuAuthority>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysSystemUrl>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysDynamicAppConfig>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysOu>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysUserMember>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysUserSupervisor>().Property(p => p.ItemId).ValueGeneratedNever();
        modelBuilder.Entity<SysApiKey>().Property(p => p.ItemId).ValueGeneratedNever();

        //配置外键字段长度
        modelBuilder.Entity<SysRoleMember>().Property(p => p.UserAcc).HasMaxLength(50);
        modelBuilder.Entity<SysUserMember>().Property(p => p.UserAcc).HasMaxLength(50);
        modelBuilder.Entity<SysUserSupervisor>().Property(p => p.UserAcc).HasMaxLength(50);
        modelBuilder.Entity<SysUserSupervisor>().Property(p => p.SupervisorAcc).HasMaxLength(50);
        modelBuilder.Entity<SysApiKey>().Property(p => p.KeyName).HasMaxLength(100);
        modelBuilder.Entity<SysApiKey>().Property(p => p.SecretKey).HasMaxLength(128);
        modelBuilder.Entity<SysApiKey>().Property(p => p.CreatedBy).HasMaxLength(50);
        modelBuilder.Entity<SysUserSupervisor>().HasIndex(p => p.UserAcc).IsUnique();

        //建立主外键关系
        //SYS_RoleMember--SYS_Role
        modelBuilder.Entity<SysRoleMember>()
            .HasOne(p => p.SysRole)
            .WithMany(b => b.SysRoleMember)
            .HasForeignKey(p => p.RoleId);

        //SYS_MenuAuthority--SYS_Menu
        modelBuilder.Entity<SysMenuAuthority>()
            .HasOne(p => p.SysMenu)
            .WithMany(b => b.SysMenuAuthority)
            .HasForeignKey(p => p.MenuID);

        //SYS_MenuAuthority--SYS_Role
        modelBuilder.Entity<SysMenuAuthority>()
            .HasOne(p => p.SysRole)
            .WithMany(b => b.SysMenuAuthority)
            .HasForeignKey(p => p.RoleID);

        //SYS_RoleMember--SYS_USER
        modelBuilder.Entity<SysRoleMember>()
            .HasOne(p => p.SysUser)
            .WithMany(b => b.SysRoleMember)
            .HasForeignKey(p => p.UserAcc);

        //SYS_UserMember--SYS_Ou
        modelBuilder.Entity<SysUserMember>()
            .HasOne(p => p.SysOu)
            .WithMany(b => b.SysUserMember)
            .HasForeignKey(p => p.DepartmentId);

        //SYS_UserMember--SYS_USER
        modelBuilder.Entity<SysUserMember>()
            .HasOne(p => p.SysUser)
            .WithMany(b => b.SysUserMember)
            .HasForeignKey(p => p.UserAcc);

        //SYS_UserSupervisor--SYS_USER（直属主管关系：一对一 / 多对一）
        //注意：双外键指向 sys_user，避免多级联路径问题，使用 Restrict，删除用户时由业务代码清理关联
        modelBuilder.Entity<SysUserSupervisor>()
            .HasOne(p => p.SysUser)
            .WithMany()
            .HasForeignKey(p => p.UserAcc)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SysUserSupervisor>()
            .HasOne(p => p.SupervisorUser)
            .WithMany()
            .HasForeignKey(p => p.SupervisorAcc)
            .OnDelete(DeleteBehavior.Restrict);

        if (isPostgreSql)
        {
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => ToPostgreSqlTimestampWithoutTimeZone(v),
                v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));

            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? ToPostgreSqlTimestampWithoutTimeZone(v.Value) : null,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : null);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetColumnType("timestamp without time zone");
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetColumnType("timestamp without time zone");
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }
    }

    private static DateTime ToPostgreSqlTimestampWithoutTimeZone(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified) return value;
        if (value.Kind == DateTimeKind.Utc) return DateTime.SpecifyKind(value.ToLocalTime(), DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }
}
