using DTSoft.AppService.Attachment;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Core.Licensing;
using DTSoft.AppService.Localization;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Attachment;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.AppService.SysConfig;

public class SysConfigApp(
    SysDbContext dbContext,
    ConfigHelper configHelper,
    AttachmentApp att,
    IDtSoftCache dtSoftCache,
    LicenseService licenseService,
    IAppLocalizer localizer)
{
    private const string SysConfigCacheKey = "SysConfig:Info";
    private const long LoginImgMaxSize = 1024 * 1024;
    private const long BrowserLogoMaxSize = 256 * 1024;
    private static readonly object ResourceSampleLock = new();
    private static DateTime _lastCpuSampleAt = DateTime.MinValue;
    private static TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private static CpuSample? _lastSystemCpuSample;
    private static readonly HashSet<string> LoginImgContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };
    private static readonly HashSet<string> LoginImgExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };
    private static readonly HashSet<string> BrowserLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/svg+xml",
        "image/x-icon",
        "image/vnd.microsoft.icon"
    };
    private static readonly HashSet<string> BrowserLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".svg",
        ".ico"
    };

    /// <summary>
    /// 设置系统信息
    /// </summary>
    public async Task<JsonObject> SetSysConfig(Models.Parameter.SysConfig.Config systemInfo)
    {
        var loginImgValidation = ValidateUploadImage(
            systemInfo.LoginImg,
            LoginImgMaxSize,
            "1MB",
            LoginImgContentTypes,
            LoginImgExtensions,
            localizer["sysConfig.loginBackgroundImage"],
            localizer);
        if (loginImgValidation is not null) return loginImgValidation;

        var browserLogoValidation = ValidateUploadImage(
            systemInfo.BrowserLogo,
            BrowserLogoMaxSize,
            "256KB",
            BrowserLogoContentTypes,
            BrowserLogoExtensions,
            localizer["sysConfig.browserTabLogo"],
            localizer);
        if (browserLogoValidation is not null) return browserLogoValidation;

        Models.Entities.SysConfig? sysConfig = dbContext.SysConfig!
            .OrderBy(p => p.ItemId)
            .FirstOrDefault();
        
        if (sysConfig is null)
        {
            Models.Entities.SysConfig info = new()
            {
                SystemName = systemInfo.SystemName,
                LoginCaptchaEnabled = systemInfo.LoginCaptchaEnabled ?? true,
                ThemeConfig = NormalizeThemeConfigJson(systemInfo.ThemeConfig)
            };
            
            // 如果有上传文件，处理文件
            if (systemInfo.LoginImg != null)
            {
                string filePath = configHelper.RootPath;
                var attachment = att.CreateFile(new BaseFileParameter() { Files = systemInfo.LoginImg, Path = filePath });
                info.LoginImg = attachment.FileFullName;
            }

            if (systemInfo.BrowserLogo != null)
            {
                string filePath = configHelper.RootPath;
                var attachment = att.CreateFile(new BaseFileParameter() { Files = systemInfo.BrowserLogo, Path = filePath });
                info.BrowserLogo = attachment.FileFullName;
            }
            
            dbContext.SysConfig!.Add(info);
        }
        else
        {
            sysConfig.SystemName = systemInfo.SystemName;
            sysConfig.LoginCaptchaEnabled = systemInfo.LoginCaptchaEnabled ?? true;
            sysConfig.ThemeConfig = NormalizeThemeConfigJson(systemInfo.ThemeConfig);
            
            // 如果有上传文件，处理文件
            if (systemInfo.LoginImg != null)
            {
                string filePath = configHelper.RootPath;
                var attachment = att.CreateFile(new BaseFileParameter() { Files = systemInfo.LoginImg, Path = filePath });
                sysConfig.LoginImg = attachment.FileFullName;
            }

            if (systemInfo.BrowserLogo != null)
            {
                string filePath = configHelper.RootPath;
                var attachment = att.CreateFile(new BaseFileParameter() { Files = systemInfo.BrowserLogo, Path = filePath });
                sysConfig.BrowserLogo = attachment.FileFullName;
            }
            
            dbContext.SysConfig!.Update(sysConfig);
        }
        
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(SysConfigCacheKey);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0
        };
    }

    private static JsonObject? ValidateUploadImage(
        IFormFile? file,
        long maxSize,
        string maxSizeText,
        HashSet<string> allowedContentTypes,
        HashSet<string> allowedExtensions,
        string label,
        IAppLocalizer localizer)
    {
        if (file is null) return null;

        var extension = Path.GetExtension(file.FileName);
        var contentType = file.ContentType ?? string.Empty;
        if (!allowedContentTypes.Contains(contentType) || !allowedExtensions.Contains(extension))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 400,
                ["Msg"] = localizer.Format("file.typeUnsupported", label)
            };
        }

        if (file.Length > maxSize)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 400,
                ["Msg"] = localizer.Format("file.sizeExceeded", label, maxSizeText)
            };
        }

        return null;
    }

    /// <summary>
    /// 获取系统信息
    /// </summary>
    public JsonObject GetSysConfig()
    {
        var dataJson = dtSoftCache.GetOrCreateAsync(SysConfigCacheKey, TimeSpan.FromMinutes(5), BuildSysConfigDataJson)
            .GetAwaiter()
            .GetResult();

        JsonObject data;
        try
        {
            data = JsonNode.Parse(dataJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            data = new JsonObject();
        }

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = data
        };
    }

    /// <summary>
    /// 获取系统运行信息
    /// </summary>
    public JsonObject GetSystemRuntimeInfo()
    {
        var process = Process.GetCurrentProcess();
        var now = DateTime.Now;
        var entryAssembly = Assembly.GetEntryAssembly();
        var assemblyName = entryAssembly?.GetName();
        var dbConnection = dbContext.Database.GetDbConnection();
        var databaseProviderName = dbContext.Database.ProviderName;

        var data = new JsonObject
        {
            ["Application"] = new JsonObject
            {
                ["Name"] = assemblyName?.Name ?? AppDomain.CurrentDomain.FriendlyName,
                ["Version"] = assemblyName?.Version?.ToString() ?? "-",
                ["EnvironmentName"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                ["BaseDirectory"] = AppContext.BaseDirectory,
                ["RootPath"] = configHelper.RootPath
            },
            ["Runtime"] = new JsonObject
            {
                ["FrameworkDescription"] = RuntimeInformation.FrameworkDescription,
                ["RuntimeIdentifier"] = RuntimeInformation.RuntimeIdentifier,
                ["OSDescription"] = RuntimeInformation.OSDescription,
                ["OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
                ["ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString()
            },
            ["Server"] = new JsonObject
            {
                ["MachineName"] = Environment.MachineName,
                ["ProcessorCount"] = Environment.ProcessorCount,
                ["TimeZone"] = TimeZoneInfo.Local.DisplayName,
                ["CurrentTime"] = now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["StartedAt"] = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["UptimeSeconds"] = Convert.ToInt64((now - process.StartTime).TotalSeconds)
            },
            ["Memory"] = new JsonObject
            {
                ["WorkingSetBytes"] = process.WorkingSet64,
                ["PrivateMemoryBytes"] = process.PrivateMemorySize64,
                ["GCTotalMemoryBytes"] = GC.GetTotalMemory(false)
            },
            ["Resource"] = BuildResourceInfo(process, now),
            ["Database"] = new JsonObject
            {
                ["ProviderName"] = databaseProviderName ?? "-",
                ["DataSource"] = dbConnection.DataSource,
                ["Database"] = dbConnection.Database,
                ["Version"] = GetDatabaseVersion(dbConnection, databaseProviderName)
            },
            ["License"] = BuildLicenseInfo()
        };

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = data
        };
    }

    private JsonObject BuildLicenseInfo()
    {
        if (!licenseService.IsValid)
        {
            return new JsonObject
            {
                ["IsValid"] = false,
                ["Status"] = localizer["license.errorStatus"],
                ["Message"] = licenseService.ErrorMessage ?? localizer["license.fileInvalid"]
            };
        }

        var license = licenseService.Current;
        var isTemporary = license.HasType(LicenseType.Temporary);
        var maxConcurrentUsers = license.MaxConcurrentUsers;

        return new JsonObject
        {
            ["IsValid"] = true,
            ["Status"] = localizer["license.authorizedStatus"],
            ["LicenseId"] = license.LicenseId,
            ["Customer"] = license.Customer,
            ["LicenseType"] = isTemporary ? "Temporary" : "Official",
            ["LicenseTypeName"] = isTemporary ? localizer["license.temporaryTypeName"] : localizer["license.officialTypeName"],
            ["ExpireAt"] = license.ExpireAt?.ToString("yyyy-MM-dd"),
            ["ExpireAtText"] = license.ExpireAt.HasValue ? license.ExpireAt.Value.ToString("yyyy-MM-dd") : localizer["license.unlimitedTime"],
            ["MaxConcurrentUsers"] = JsonValue.Create(maxConcurrentUsers),
            ["MaxConcurrentUsersText"] = isTemporary
                ? localizer["license.notControlled"]
                : maxConcurrentUsers == -1 ? localizer["license.unlimited"] : maxConcurrentUsers?.ToString() ?? "-"
        };
    }

    private static string GetDatabaseVersion(System.Data.Common.DbConnection dbConnection, string? providerName)
    {
        var shouldClose = false;

        try
        {
            if (dbConnection.State != ConnectionState.Open)
            {
                dbConnection.Open();
                shouldClose = true;
            }

            var query = GetDatabaseVersionQuery(providerName);
            if (!string.IsNullOrWhiteSpace(query))
            {
                using var command = dbConnection.CreateCommand();
                command.CommandText = query;
                command.CommandTimeout = 5;

                var version = Convert.ToString(command.ExecuteScalar());
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version.Trim();
                }
            }

            return GetDatabaseServerVersion(dbConnection);
        }
        catch
        {
            return GetDatabaseServerVersion(dbConnection);
        }
        finally
        {
            if (shouldClose)
            {
                dbConnection.Close();
            }
        }
    }

    private static string GetDatabaseServerVersion(System.Data.Common.DbConnection dbConnection)
    {
        try
        {
            return string.IsNullOrWhiteSpace(dbConnection.ServerVersion) ? "-" : dbConnection.ServerVersion;
        }
        catch
        {
            return "-";
        }
    }

    private static string? GetDatabaseVersionQuery(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;

        if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return """
                   SELECT CONCAT(
                       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
                       ' (', CAST(SERVERPROPERTY('ProductLevel') AS nvarchar(128)),
                       ', ', CAST(SERVERPROPERTY('Edition') AS nvarchar(256)), ')')
                   """;
        }

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
            providerName.Contains("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT version()";
        }

        if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT VERSION()";
        }

        if (providerName.Contains("Oracle", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT banner FROM v$version WHERE banner LIKE 'Oracle Database%'";
        }

        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT sqlite_version()";
        }

        return null;
    }

    private static JsonObject BuildResourceInfo(Process process, DateTime now)
    {
        var processCpuUsagePercent = CalculateProcessCpuUsagePercent(process, now);
        var systemCpuUsagePercent = CalculateSystemCpuUsagePercent();
        var memoryInfo = TryReadSystemMemoryInfo();
        var gcTotalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var fallbackTotalMemoryBytes = gcTotalAvailableMemoryBytes > 0 ? (ulong)gcTotalAvailableMemoryBytes : (ulong?)null;
        var processMemoryUsagePercent = GetProcessMemoryUsagePercent(process, memoryInfo?.TotalBytes ?? fallbackTotalMemoryBytes);
        double? systemMemoryUsagePercent = memoryInfo is { TotalBytes: > 0, AvailableBytes: not null }
            ? (double)(memoryInfo.TotalBytes - memoryInfo.AvailableBytes.Value) / memoryInfo.TotalBytes * 100
            : null;

        return new JsonObject
        {
            ["CpuUsagePercent"] = JsonValue.Create(RoundPercent(systemCpuUsagePercent)),
            ["MemoryUsagePercent"] = JsonValue.Create(RoundPercent(systemMemoryUsagePercent)),
            ["ServerCpuUsagePercent"] = JsonValue.Create(RoundPercent(systemCpuUsagePercent)),
            ["ServerMemoryUsagePercent"] = JsonValue.Create(RoundPercent(systemMemoryUsagePercent)),
            ["ProcessCpuUsagePercent"] = JsonValue.Create(RoundPercent(processCpuUsagePercent)),
            ["ProcessMemoryUsagePercent"] = JsonValue.Create(RoundPercent(processMemoryUsagePercent)),
            ["TotalMemoryBytes"] = JsonValue.Create(ToNullableInt64(memoryInfo?.TotalBytes ?? fallbackTotalMemoryBytes)),
            ["AvailableMemoryBytes"] = JsonValue.Create(ToNullableInt64(memoryInfo?.AvailableBytes)),
            ["CollectedAt"] = now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    private static double? CalculateProcessCpuUsagePercent(Process process, DateTime now)
    {
        var currentTotalProcessorTime = process.TotalProcessorTime;
        var processorCount = Math.Max(Environment.ProcessorCount, 1);

        lock (ResourceSampleLock)
        {
            if (_lastCpuSampleAt == DateTime.MinValue)
            {
                _lastCpuSampleAt = now;
                _lastTotalProcessorTime = currentTotalProcessorTime;

                var elapsedSinceStart = now - process.StartTime;
                if (elapsedSinceStart.TotalMilliseconds <= 0) return 0;

                return currentTotalProcessorTime.TotalMilliseconds / elapsedSinceStart.TotalMilliseconds / processorCount * 100;
            }

            var elapsed = now - _lastCpuSampleAt;
            var cpuDelta = currentTotalProcessorTime - _lastTotalProcessorTime;

            _lastCpuSampleAt = now;
            _lastTotalProcessorTime = currentTotalProcessorTime;

            if (elapsed.TotalMilliseconds <= 0 || cpuDelta.TotalMilliseconds < 0) return 0;

            return cpuDelta.TotalMilliseconds / elapsed.TotalMilliseconds / processorCount * 100;
        }
    }

    private static double? CalculateSystemCpuUsagePercent()
    {
        var firstSample = TryReadSystemCpuSample();
        if (firstSample is null) return null;

        lock (ResourceSampleLock)
        {
            if (_lastSystemCpuSample is null)
            {
                Thread.Sleep(100);
                var secondSample = TryReadSystemCpuSample();
                if (secondSample is null) return null;

                _lastSystemCpuSample = secondSample.Value;
                return CalculateCpuPercent(firstSample.Value, secondSample.Value);
            }

            var previousSample = _lastSystemCpuSample.Value;
            _lastSystemCpuSample = firstSample.Value;
            return CalculateCpuPercent(previousSample, firstSample.Value);
        }
    }

    private static CpuSample? TryReadSystemCpuSample()
    {
        if (OperatingSystem.IsWindows()) return TryReadWindowsCpuSample();
        if (OperatingSystem.IsLinux()) return TryReadLinuxCpuSample();
        if (OperatingSystem.IsMacOS()) return TryReadMacOSCpuSample();

        return null;
    }

    private static CpuSample? TryReadWindowsCpuSample()
    {
        try
        {
            return GetSystemTimes(out var idleTime, out var kernelTime, out var userTime)
                ? new CpuSample(idleTime.ToUInt64(), kernelTime.ToUInt64() + userTime.ToUInt64())
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static CpuSample? TryReadLinuxCpuSample()
    {
        try
        {
            var firstLine = File.ReadLines("/proc/stat").FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLine) || !firstLine.StartsWith("cpu ")) return null;

            var values = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(value => ulong.TryParse(value, out var number) ? number : 0)
                .ToArray();
            if (values.Length < 4) return null;

            var idle = values[3] + (values.Length > 4 ? values[4] : 0);
            var total = values.Aggregate<ulong, ulong>(0, (sum, value) => sum + value);

            return new CpuSample(idle, total);
        }
        catch
        {
            return null;
        }
    }

    private static double? CalculateCpuPercent(CpuSample previousSample, CpuSample currentSample)
    {
        if (currentSample.Total < previousSample.Total || currentSample.Idle < previousSample.Idle) return null;

        var idleDelta = currentSample.Idle - previousSample.Idle;
        var totalDelta = currentSample.Total - previousSample.Total;
        if (totalDelta == 0 || idleDelta > totalDelta) return null;

        return (double)(totalDelta - idleDelta) / totalDelta * 100;
    }

    private static SystemMemoryInfo? TryReadSystemMemoryInfo()
    {
        if (OperatingSystem.IsLinux()) return TryReadLinuxMemoryInfo();
        if (OperatingSystem.IsWindows()) return TryReadWindowsMemoryInfo();
        if (OperatingSystem.IsMacOS()) return TryReadMacOSMemoryInfo();

        return null;
    }

    private static SystemMemoryInfo? TryReadLinuxMemoryInfo()
    {
        try
        {
            ulong? totalKb = null;
            ulong? availableKb = null;

            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    totalKb = ParseLinuxMemoryKb(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    availableKb = ParseLinuxMemoryKb(line);
                }

                if (totalKb is not null && availableKb is not null) break;
            }

            return totalKb is > 0 && availableKb is not null
                ? new SystemMemoryInfo(totalKb.Value * 1024, availableKb.Value * 1024)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static ulong? ParseLinuxMemoryKb(string line)
    {
        var value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
        return ulong.TryParse(value, out var number) ? number : null;
    }

    private static SystemMemoryInfo? TryReadWindowsMemoryInfo()
    {
        try
        {
            var memoryStatus = new MemoryStatusEx
            {
                dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            return GlobalMemoryStatusEx(ref memoryStatus)
                ? new SystemMemoryInfo(memoryStatus.ullTotalPhys, memoryStatus.ullAvailPhys)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static CpuSample? TryReadMacOSCpuSample()
    {
        IntPtr processorInfo = IntPtr.Zero;
        uint processorInfoCount = 0;

        try
        {
            var result = HostProcessorInfo(
                MachHostSelf(),
                ProcessorCpuLoadInfo,
                out var processorCount,
                out processorInfo,
                out processorInfoCount);
            if (
                result != 0 ||
                processorInfo == IntPtr.Zero ||
                processorInfoCount == 0 ||
                processorInfoCount > int.MaxValue ||
                processorCount == 0)
            {
                return null;
            }

            var cpuInfo = new int[(int)processorInfoCount];
            Marshal.Copy(processorInfo, cpuInfo, 0, cpuInfo.Length);

            ulong idle = 0;
            ulong total = 0;
            for (var index = 0; index + CpuStateMax <= cpuInfo.Length; index += CpuStateMax)
            {
                var user = ToUInt64(cpuInfo[index + CpuStateUser]);
                var system = ToUInt64(cpuInfo[index + CpuStateSystem]);
                var idleTicks = ToUInt64(cpuInfo[index + CpuStateIdle]);
                var nice = ToUInt64(cpuInfo[index + CpuStateNice]);

                idle += idleTicks;
                total += user + system + idleTicks + nice;
            }

            return total > 0 ? new CpuSample(idle, total) : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (processorInfo != IntPtr.Zero)
            {
                _ = VmDeallocate(MachTaskSelf(), processorInfo, new UIntPtr((ulong)processorInfoCount * sizeof(int)));
            }
        }
    }

    private static SystemMemoryInfo? TryReadMacOSMemoryInfo()
    {
        try
        {
            var totalMemoryBytes = TryReadMacOSTotalMemoryBytes();
            if (totalMemoryBytes is not > 0) return null;

            var host = MachHostSelf();
            if (HostPageSize(host, out var pageSize) != 0 || pageSize == 0)
            {
                return new SystemMemoryInfo(totalMemoryBytes.Value, null);
            }

            var vmStats = new int[MacOSHostVmInfoCount];
            var vmStatsCount = (uint)vmStats.Length;
            if (HostStatistics(host, HostVmInfo, vmStats, ref vmStatsCount) != 0 || vmStatsCount < 4)
            {
                return new SystemMemoryInfo(totalMemoryBytes.Value, null);
            }

            var freeCount = ToUInt64(vmStats[0]);
            var inactiveCount = ToUInt64(vmStats[2]);
            var speculativeCount = vmStatsCount > 14 ? ToUInt64(vmStats[14]) : 0;
            var availableBytes = (freeCount + inactiveCount + speculativeCount) * pageSize;

            return new SystemMemoryInfo(totalMemoryBytes.Value, availableBytes);
        }
        catch
        {
            return null;
        }
    }

    private static ulong? TryReadMacOSTotalMemoryBytes()
    {
        try
        {
            ulong totalMemoryBytes;
            var size = new UIntPtr((uint)sizeof(ulong));
            var result = SysctlByName("hw.memsize", out totalMemoryBytes, ref size, IntPtr.Zero, UIntPtr.Zero);

            return result == 0 ? totalMemoryBytes : null;
        }
        catch
        {
            return null;
        }
    }

    private static double? GetProcessMemoryUsagePercent(Process process, ulong? totalMemoryBytes)
    {
        if (totalMemoryBytes > 0)
        {
            return (double)process.WorkingSet64 / totalMemoryBytes.Value * 100;
        }

        return null;
    }

    private static double? RoundPercent(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;

        return Math.Round(Math.Clamp(value.Value, 0, 100), 1);
    }

    private static long? ToNullableInt64(ulong? value)
    {
        if (value is null) return null;

        return value.Value > long.MaxValue ? long.MaxValue : (long)value.Value;
    }

    private static ulong ToUInt64(int value)
    {
        return value < 0 ? 0 : (ulong)value;
    }

    private readonly record struct CpuSample(ulong Idle, ulong Total);

    private sealed record SystemMemoryInfo(ulong TotalBytes, ulong? AvailableBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public readonly ulong ToUInt64()
        {
            return ((ulong)dwHighDateTime << 32) | dwLowDateTime;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private const int ProcessorCpuLoadInfo = 2;
    private const int CpuStateUser = 0;
    private const int CpuStateSystem = 1;
    private const int CpuStateIdle = 2;
    private const int CpuStateNice = 3;
    private const int CpuStateMax = 4;
    private const int HostVmInfo = 2;
    private const int MacOSHostVmInfoCount = 64;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatus);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "mach_host_self")]
    private static extern IntPtr MachHostSelf();

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "mach_task_self_")]
    private static extern IntPtr MachTaskSelf();

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "host_processor_info")]
    private static extern int HostProcessorInfo(
        IntPtr host,
        int flavor,
        out uint processorCount,
        out IntPtr processorInfo,
        out uint processorInfoCount);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "vm_deallocate")]
    private static extern int VmDeallocate(IntPtr targetTask, IntPtr address, UIntPtr size);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "host_page_size")]
    private static extern int HostPageSize(IntPtr host, out uint pageSize);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "host_statistics")]
    private static extern int HostStatistics(IntPtr host, int flavor, [Out] int[] hostInfo, ref uint hostInfoCount);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "sysctlbyname")]
    private static extern int SysctlByName(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        out ulong oldValue,
        ref UIntPtr oldLength,
        IntPtr newValue,
        UIntPtr newLength);

    private string BuildSysConfigDataJson()
    {
        Models.Entities.SysConfig? systemInfo = dbContext.SysConfig!
            .AsNoTracking()
            .OrderBy(p => p.ItemId)
            .FirstOrDefault();

        var obj = new JsonObject
        {
            ["systemName"] = systemInfo?.SystemName,
            ["loginCaptchaEnabled"] = systemInfo?.LoginCaptchaEnabled ?? true,
            ["loginImg"] = ReadImageAsDataUrl(systemInfo?.LoginImg),
            ["browserLogo"] = ReadImageAsDataUrl(systemInfo?.BrowserLogo),
            ["themeConfig"] = ParseThemeConfig(systemInfo?.ThemeConfig)
        };

        return obj.ToJsonString();
    }

    /// <summary>
    /// 获取登录验证码开关状态
    /// </summary>
    public bool IsLoginCaptchaEnabled()
    {
        var dataJson = dtSoftCache.GetOrCreateAsync(SysConfigCacheKey, TimeSpan.FromMinutes(5), BuildSysConfigDataJson)
            .GetAwaiter()
            .GetResult();

        try
        {
            var data = JsonNode.Parse(dataJson) as JsonObject;
            return data?["loginCaptchaEnabled"]?.GetValue<bool>() ?? true;
        }
        catch
        {
            return true;
        }
    }

    private string? ReadImageAsDataUrl(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName)) return null;

        var filePath = Path.Combine(configHelper.RootPath, storedFileName);
        if (!File.Exists(filePath)) return null;

        var bytes = File.ReadAllBytes(filePath);
        var ext = Path.GetExtension(storedFileName).TrimStart('.').ToLowerInvariant();
        var mimeType = ext switch
        {
            "png" => "image/png",
            "gif" => "image/gif",
            "svg" => "image/svg+xml",
            "webp" => "image/webp",
            "ico" => "image/x-icon",
            "jpg" => "image/jpeg",
            "jpeg" => "image/jpeg",
            _ => "image/png"
        };

        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static string? NormalizeThemeConfigJson(string? themeConfig)
    {
        if (string.IsNullOrWhiteSpace(themeConfig)) return null;

        try
        {
            return JsonNode.Parse(themeConfig)?.ToJsonString();
        }
        catch
        {
            return null;
        }
    }

    private static JsonNode? ParseThemeConfig(string? themeConfig)
    {
        if (string.IsNullOrWhiteSpace(themeConfig)) return null;

        try
        {
            return JsonNode.Parse(themeConfig);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 初始化系统数据库
    /// </summary>
    public JsonObject InitializationSystem()
    {
        // 检查数据库是否已存在（不抛出异常）
        if (IsDatabaseInitialized())
        {
            return new JsonObject
            {
                ["StateCode"] = 0,
                ["success"] = true,
                ["Msg"] = localizer["sysConfig.databaseAlreadyInitialized"]
            };
        }

        try
        {
            // 创建数据库（这会创建数据库和表结构）
            bool created = dbContext.Database.EnsureCreated();

            // 检查是否创建成功或数据库已存在，然后插入初始数据
            if (dbContext.Database.CanConnect())
            {
                // 再次检查是否已有数据，防止重复初始化
                if (dbContext.SysUser.Any(u => u.Account == "admin"))
                {
                    return new JsonObject
                    {
                        ["StateCode"] = 0,
                        ["success"] = true,
                        ["Msg"] = localizer["sysConfig.databaseAlreadyInitialized"]
                    };
                }

                // 添加初始数据
                AddInitialData();

                return new JsonObject
                {
                    ["StateCode"] = 0,
                    ["success"] = true,
                    ["Msg"] = localizer["sysConfig.initializationSuccess"]
                };
            }
            else
            {
                return new JsonObject
                {
                    ["StateCode"] = 1,
                    ["success"] = false,
                    ["Msg"] = localizer["sysConfig.databaseConnectionFailed"]
                };
            }
        }
        catch (Exception ex) when (IsDatabaseNotFound(ex))
        { // 捕获数据库不存在的特定错误并处理
            try
            {
                // 直接尝试创建数据库
                dbContext.Database.EnsureCreated();

                // 检查是否创建成功，然后插入初始数据
                if (dbContext.Database.CanConnect())
                {
                    // 再次检查是否已有数据，防止重复初始化
                    if (dbContext.SysUser.Any(u => u.Account == "admin"))
                    {
                        return new JsonObject
                        {
                        ["StateCode"] = 0,
                        ["success"] = true,
                        ["Msg"] = localizer["sysConfig.databaseAlreadyInitialized"]
                    };
                    }

                    // 添加初始数据
                    AddInitialData();

                    return new JsonObject
                    {
                        ["StateCode"] = 0,
                        ["success"] = true,
                        ["Msg"] = localizer["sysConfig.initializationSuccess"]
                    };
                }
                else
                {
                    return new JsonObject
                    {
                        ["StateCode"] = 1,
                        ["success"] = false,
                        ["Msg"] = localizer["sysConfig.databaseConnectionFailed"]
                    };
                }
            }
            catch (Exception innerEx)
            {
                return new JsonObject
                {
                    ["StateCode"] = 1,
                    ["success"] = false,
                    ["Msg"] = innerEx.Message
                };
            }
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["StateCode"] = 1,
                ["success"] = false,
                ["Msg"] = ex.Message
            };
        }
    }

    /// <summary>
    /// 添加初始数据
    /// </summary>
    private void AddInitialData()
    {
        //添加用户
        SysUser user = new()
        {
            Account = "admin",
            PassWord = Encrypt.HashPassword("admin123"),
            DisplayName = localizer["user.systemAdministrator"],
            Disable = false
        };
        dbContext.SysUser.Add(user);
        
        // 先保存用户
        dbContext.SaveChanges();

        //添加角色 - 使用 ItemId 而不是自增 ID
        var adminRole = new SysRole
        {
            ItemId = YitterHelper.NewId(),  // 管理员角色
            RoleName = "Administrator"
        };
        
        var everyoneRole = new SysRole
        {
            ItemId = YitterHelper.NewId(),  // 普通用户角色
            RoleName = "Everyone"
        };
        
        dbContext.SysRole!.AddRange(adminRole, everyoneRole);
        dbContext.SaveChanges();  // 保存角色以生成 ItemId

        //把用户添加到角色（使用实际的角色 ItemId）
        SysRoleMember rolemember = new()
        {
            ItemId = YitterHelper.NewId(),  // 生成唯一 ID
            RoleId = adminRole.ItemId,  // 使用实际生成的 ItemId
            UserAcc = "admin"
        };
        dbContext.SysRoleMember!.Add(rolemember);
        dbContext.SaveChanges();  // 保存角色成员关系

        dbContext.SysLanguage!.AddRange(
            new SysLanguage
            {
                ItemId = YitterHelper.NewId(),
                LanguageCode = "zh-CN",
                LanguageName = localizer["language.zhCn"],
                NativeName = localizer["language.zhCnNative"],
                IsEnabled = true,
                IsDefault = true,
                Sort = 10
            },
            new SysLanguage
            {
                ItemId = YitterHelper.NewId(),
                LanguageCode = "en-US",
                LanguageName = "English",
                NativeName = "English",
                IsEnabled = true,
                IsDefault = false,
                Sort = 20
            });
        dbContext.SaveChanges();

        //创建菜单 - 使用 ItemId，并正确处理层级关系
        var menuList = new List<SysMenu>();
        
        // 一级菜单
        var platform = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = localizer["menu.platform"], I18nKey = "menu.platform", Icon = "Platform", Order = 10, MType = 0 };
        var organization = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = localizer["menu.organization"], I18nKey = "menu.organization", Icon = "UserFilled", Order = 20, MType = 0 };
        var systemManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = localizer["menu.systemManagement"], I18nKey = "menu.systemManagement", Icon = "Setting", Order = 30, MType = 0 };
        var applicationIntegration = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = localizer["menu.applicationIntegration"], I18nKey = "menu.applicationIntegration", Icon = "Connection", Order = 40, MType = 0 };
        
        // 二级菜单
        var platformHome = new SysMenu { ItemId = YitterHelper.NewId(), Pid = platform.ItemId, MenuName = localizer["menu.welcome"], I18nKey = "menu.welcome", MenuPath = "welcome", Icon = "House", Order = 10, MType = 0 };

        var organizationList = new SysMenu { ItemId = YitterHelper.NewId(), Pid = organization.ItemId, MenuName = localizer["menu.organizationList"], I18nKey = "menu.organizationList", MenuPath = "user/organization", Icon = "User", Order = 10, MType = 0 };
        var roleList = new SysMenu { ItemId = YitterHelper.NewId(), Pid = organization.ItemId, MenuName = localizer["menu.roles"], I18nKey = "menu.roles", MenuPath = "role/rolesmenu", Icon = "UserFilled", Order = 20, MType = 0 };

        var systemSettingsPage = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.systemSettings"], I18nKey = "menu.systemSettings", MenuPath = "common/systemsettings", Icon = "Setting", Order = 10, MType = 0 };
        var systemInfoPage = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.systemInfo"], I18nKey = "menu.systemInfo", MenuPath = "common/systeminfo", Icon = "Monitor", Order = 20, MType = 0 };
        var onlineUsers = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.onlineUsers"], I18nKey = "menu.onlineUsers", MenuPath = "common/onlineusers", Icon = "User", Order = 30, MType = 0 };
        var pluginManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.plugins"], I18nKey = "menu.plugins", MenuPath = "common/plugins", Icon = "Connection", Order = 40, MType = 0 };
        var systemLog = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.systemLog"], I18nKey = "menu.systemLog", MenuPath = "log/logaction", Icon = "List", Order = 50, MType = 0 };
        var dictionaryManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.dictionaries"], I18nKey = "menu.dictionaries", MenuPath = "common/dictionaries", Icon = "Collection", Order = 60, MType = 0 };
        var attachmentList = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.attachments"], I18nKey = "menu.attachments", MenuPath = "attachment/attachmentlist", Icon = "Paperclip", Order = 70, MType = 0 };
        var menuMaintenance = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.menus"], I18nKey = "menu.menus", MenuPath = "common/menus", Icon = "Menu", Order = 80, MType = 0 };
        var languageConfig = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = localizer["menu.languages"], I18nKey = "menu.languages", MenuPath = "common/languages", Icon = "Tickets", Order = 90, MType = 0 };

        var appConfig = new SysMenu { ItemId = YitterHelper.NewId(), Pid = applicationIntegration.ItemId, MenuName = localizer["menu.microAppConfig"], I18nKey = "menu.microAppConfig", MenuPath = "MicroApp/MicroApiConfig", Icon = "Coin", Order = 10, MType = 0 };
        var esbServiceConnections = new SysMenu { ItemId = YitterHelper.NewId(), Pid = applicationIntegration.ItemId, MenuName = localizer["menu.esbConnections"], I18nKey = "menu.esbConnections", MenuPath = "common/esb-connections", Icon = "Link", Order = 20, MType = 0 };
        var esbDataSources = new SysMenu { ItemId = YitterHelper.NewId(), Pid = applicationIntegration.ItemId, MenuName = localizer["menu.esbDataSources"], I18nKey = "menu.esbDataSources", MenuPath = "common/esb", Icon = "Connection", Order = 30, MType = 0 };
        var apiKeyManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = applicationIntegration.ItemId, MenuName = localizer["menu.apiKeys"], I18nKey = "menu.apiKeys", MenuPath = "integration/api-keys", Icon = "Key", Order = 40, MType = 0 };
        
        menuList.AddRange([platform, platformHome, organization, organizationList, roleList, systemManagement, systemSettingsPage, systemInfoPage, onlineUsers, pluginManagement, systemLog, dictionaryManagement, attachmentList, menuMaintenance, languageConfig, applicationIntegration, appConfig, esbServiceConnections, esbDataSources, apiKeyManagement]);
        
        // 批量添加菜单
        dbContext.SysMenu.AddRange(menuList);
        dbContext.SaveChanges();  // 保存所有菜单以生成 ItemId
        
        // 菜单授权（使用实际的菜单 ItemId）
        foreach (var item in menuList)
        {
            dbContext.SysMenuAuthority!.Add(new SysMenuAuthority 
            { 
                ItemId = YitterHelper.NewId(),  // 为每个授权记录生成唯一 ID
                RoleID = adminRole.ItemId,  // 使用实际的角色 ItemId
                MenuID = item.ItemId 
            });
        }
        dbContext.SaveChanges();
        
        // 注意：admin 是超级管理员账号，不属于任何部门
        // 部门数据为空，需要在部门管理功能中手动创建
    }

    /// <summary>
    /// 检查数据库是否已经初始化
    /// </summary>
    /// <returns></returns>
    private bool IsDatabaseInitialized()
    {
        try
        {
            // 尝试打开数据库连接
            dbContext.Database.OpenConnection();
            dbContext.Database.CloseConnection();

            // 检查管理员用户是否存在
            var adminUser = dbContext.SysUser.FirstOrDefault(u => u.Account == "admin");
            if (adminUser != null)
            {
                return true; // 如果管理员用户存在，则认为数据库已初始化
            }

            // 检查数据库是否存在相关表并是否有数据
            var userCount = dbContext.SysUser.Count();
            if (dbContext.SysRole == null)
            {
                return false;
            }
            var roleCount = dbContext.SysRole.Count();

            // 如果用户表或角色表中有数据，则认为数据库已初始化
            return userCount > 0 || roleCount > 0;
        }
        catch (Exception ex) when (IsDatabaseNotFound(ex))
        {
            // 数据库不存在
            return false;
        }
        catch (Exception)
        {
            // 其他异常也认为是未初始化
            return false;
        }
    }

    private static bool IsDatabaseNotFound(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("Unknown database", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("database", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)) ||
               message.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase);
    }
}
