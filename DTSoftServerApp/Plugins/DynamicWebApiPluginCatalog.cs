using DTSoft.AppService.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace DTSoftServerApp.Plugins;

public sealed class DynamicWebApiPluginCatalog
{
    private const string CatalogFileName = "plugins.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _pluginDirectory;
    private readonly string _catalogPath;
    private readonly IAppLocalizer _localizer;
    private readonly object _syncRoot = new();

    public DynamicWebApiPluginCatalog(IConfiguration configuration, IAppLocalizer localizer)
    {
        _pluginDirectory = DynamicWebApiPluginLoader.ResolvePluginDirectory(configuration);
        _catalogPath = Path.Combine(_pluginDirectory, CatalogFileName);
        _localizer = localizer;
    }

    public IReadOnlyList<DynamicWebApiPluginCatalogItem> List()
    {
        lock (_syncRoot)
        {
            return ReadCatalog().Plugins
                .OrderByDescending(plugin => plugin.UploadedAt)
                .ThenBy(plugin => plugin.AssemblyName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public DynamicWebApiPluginCatalogItem AddUploadedPlugin(IFormFile? file, string uploadedBy)
    {
        if (file is null)
        {
            throw new InvalidOperationException(_localizer["plugin.uploadFileRequired"]);
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException(_localizer["plugin.uploadFileEmpty"]);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(_localizer["plugin.uploadTypeUnsupported"]);
        }

        Directory.CreateDirectory(_pluginDirectory);

        var stagingDirectory = Path.Combine(_pluginDirectory, "_staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = file.OpenReadStream();
                ExtractZipSafely(stream, stagingDirectory);
            }
            else
            {
                var fileName = Path.GetFileName(file.FileName);
                var targetPath = Path.Combine(stagingDirectory, fileName);
                using var targetStream = File.Create(targetPath);
                file.CopyTo(targetStream);
            }

            var mainAssemblyPath = ResolveMainAssemblyPath(stagingDirectory);
            var assemblyName = AssemblyName.GetAssemblyName(mainAssemblyPath);
            var pluginVersion = assemblyName.Version?.ToString() ?? "1.0.0.0";
            var pluginDirectoryName = SanitizePathPart(assemblyName.Name ?? Path.GetFileNameWithoutExtension(mainAssemblyPath));
            var versionDirectoryName = SanitizePathPart(pluginVersion);
            var pluginId = $"{pluginDirectoryName}:{versionDirectoryName}";

            lock (_syncRoot)
            {
                var catalog = ReadCatalog();
                if (catalog.Plugins.Any(plugin => plugin.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(_localizer.Format("plugin.versionAlreadyExists", pluginDirectoryName, pluginVersion));
                }
            }

            var finalDirectory = PrepareFinalDirectory(pluginDirectoryName, versionDirectoryName);
            Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory)!);
            Directory.Move(stagingDirectory, finalDirectory);

            var finalMainAssemblyPath = Path.Combine(finalDirectory, Path.GetRelativePath(stagingDirectory, mainAssemblyPath));
            var item = new DynamicWebApiPluginCatalogItem
            {
                Id = pluginId,
                AssemblyName = assemblyName.Name ?? pluginDirectoryName,
                PluginName = assemblyName.Name ?? pluginDirectoryName,
                Version = pluginVersion,
                Directory = Path.GetRelativePath(_pluginDirectory, finalDirectory),
                MainAssembly = Path.GetRelativePath(_pluginDirectory, finalMainAssemblyPath),
                Enabled = true,
                Status = "PendingRestart",
                UploadedBy = uploadedBy,
                UploadedAt = DateTimeOffset.UtcNow
            };

            lock (_syncRoot)
            {
                var catalog = ReadCatalog();
                catalog.Plugins.Add(item);
                WriteCatalog(catalog);
            }

            return item;
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public DynamicWebApiPluginCatalogItem SetEnabled(string id, bool enabled)
    {
        lock (_syncRoot)
        {
            var catalog = ReadCatalog();
            var plugin = catalog.Plugins.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (plugin is null)
            {
                throw new InvalidOperationException(_localizer["plugin.notFound"]);
            }

            plugin.Enabled = enabled;
            plugin.Status = "PendingRestart";
            WriteCatalog(catalog);
            return plugin;
        }
    }

    public void Remove(string id)
    {
        lock (_syncRoot)
        {
            var catalog = ReadCatalog();
            var plugin = catalog.Plugins.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (plugin is null)
            {
                throw new InvalidOperationException(_localizer["plugin.notFound"]);
            }

            var pluginDirectory = TryResolvePathUnderPluginDirectory(_pluginDirectory, plugin.Directory)
                                  ?? throw new InvalidOperationException(_localizer["plugin.pathInvalid"]);
            if (Directory.Exists(pluginDirectory) && !TryDeleteDirectory(pluginDirectory))
            {
                throw new InvalidOperationException(_localizer["plugin.deleteDirectoryFailed"]);
            }
            CleanupEmptyParentDirectories(pluginDirectory);

            catalog.Plugins.Remove(plugin);
            WriteCatalog(catalog);
        }
    }

    public static IReadOnlyList<string> GetEnabledPluginAssemblies(IConfiguration configuration)
    {
        var pluginDirectory = DynamicWebApiPluginLoader.ResolvePluginDirectory(configuration);
        var catalogPath = Path.Combine(pluginDirectory, CatalogFileName);
        if (!File.Exists(catalogPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = File.ReadAllText(catalogPath);
            var catalog = JsonSerializer.Deserialize<DynamicWebApiPluginCatalogDocument>(json, JsonOptions)
                          ?? new DynamicWebApiPluginCatalogDocument();

            return catalog.Plugins
                .Where(plugin => plugin.Enabled)
                .Select(plugin => TryResolvePathUnderPluginDirectory(pluginDirectory, plugin.MainAssembly))
                .Where(path => path is not null)
                .Select(path => path!)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static void UpdateEffectiveStatuses(
        IConfiguration configuration,
        DynamicWebApiPluginLoadResult loadResult,
        IAppLocalizer localizer)
    {
        var pluginDirectory = DynamicWebApiPluginLoader.ResolvePluginDirectory(configuration);
        var catalogPath = Path.Combine(pluginDirectory, CatalogFileName);
        if (!File.Exists(catalogPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(catalogPath);
            var catalog = JsonSerializer.Deserialize<DynamicWebApiPluginCatalogDocument>(json, JsonOptions)
                          ?? new DynamicWebApiPluginCatalogDocument();

            var loadedPaths = loadResult.Plugins
                .Select(plugin => Path.GetFullPath(plugin.FilePath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var failuresByPath = loadResult.Failures
                .GroupBy(failure => Path.GetFullPath(failure.FilePath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            foreach (var plugin in catalog.Plugins)
            {
                plugin.LastLoadError = null;

                if (!plugin.Enabled)
                {
                    plugin.Status = "Disabled";
                    continue;
                }

                var assemblyPath = TryResolvePathUnderPluginDirectory(pluginDirectory, plugin.MainAssembly);
                if (assemblyPath is null || !File.Exists(assemblyPath))
                {
                    plugin.Status = "LoadFailed";
                    plugin.LastLoadError = localizer["plugin.assemblyFileMissing"];
                    continue;
                }

                var fullAssemblyPath = Path.GetFullPath(assemblyPath);
                if (failuresByPath.TryGetValue(fullAssemblyPath, out var failure))
                {
                    plugin.Status = "LoadFailed";
                    plugin.LastLoadError = failure.Message;
                    continue;
                }

                plugin.Status = loadedPaths.Contains(fullAssemblyPath) ? "Loaded" : "LoadFailed";
                if (plugin.Status == "LoadFailed")
                {
                    plugin.LastLoadError = localizer["plugin.noValidEntryLoaded"];
                }
            }

            Directory.CreateDirectory(pluginDirectory);
            File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog, JsonOptions));
        }
        catch
        {
            // 状态回写失败不影响服务启动和插件加载。
        }
    }

    private DynamicWebApiPluginCatalogDocument ReadCatalog()
    {
        if (!File.Exists(_catalogPath))
        {
            return new DynamicWebApiPluginCatalogDocument();
        }

        try
        {
            var json = File.ReadAllText(_catalogPath);
            return JsonSerializer.Deserialize<DynamicWebApiPluginCatalogDocument>(json, JsonOptions)
                   ?? new DynamicWebApiPluginCatalogDocument();
        }
        catch
        {
            return new DynamicWebApiPluginCatalogDocument();
        }
    }

    private void WriteCatalog(DynamicWebApiPluginCatalogDocument catalog)
    {
        Directory.CreateDirectory(_pluginDirectory);
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        File.WriteAllText(_catalogPath, json);
    }

    private string PrepareFinalDirectory(string pluginDirectoryName, string versionDirectoryName)
    {
        var finalDirectory = Path.Combine(_pluginDirectory, pluginDirectoryName, versionDirectoryName);
        if (!Directory.Exists(finalDirectory))
        {
            return finalDirectory;
        }

        if (!TryDeleteDirectory(finalDirectory))
        {
            throw new InvalidOperationException(_localizer.Format("plugin.directoryAlreadyExists", pluginDirectoryName, versionDirectoryName));
        }

        return finalDirectory;
    }

    public static string CreateShadowCopy(IConfiguration configuration, string assemblyPath)
    {
        var pluginDirectory = DynamicWebApiPluginLoader.ResolvePluginDirectory(configuration);
        var sourceAssemblyPath = Path.GetFullPath(assemblyPath);
        var sourceDirectory = Path.GetDirectoryName(sourceAssemblyPath)
                              ?? throw new DirectoryNotFoundException(assemblyPath);
        var shadowRoot = Path.Combine(pluginDirectory, "_shadow");
        var shadowDirectory = Path.Combine(
            shadowRoot,
            $"{Path.GetFileNameWithoutExtension(sourceAssemblyPath)}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(shadowDirectory);
        CopyDirectory(sourceDirectory, shadowDirectory);

        var relativeAssemblyPath = Path.GetRelativePath(sourceDirectory, sourceAssemblyPath);
        return Path.Combine(shadowDirectory, relativeAssemblyPath);
    }

    public static void CleanupShadowCopies(IConfiguration configuration)
    {
        var pluginDirectory = DynamicWebApiPluginLoader.ResolvePluginDirectory(configuration);
        var shadowRoot = Path.Combine(pluginDirectory, "_shadow");
        TryDeleteDirectory(shadowRoot);
    }

    private string ResolveMainAssemblyPath(string directory)
    {
        var dllFiles = Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Where(file => !IsHostSharedAssembly(Path.GetFileNameWithoutExtension(file)))
            .OrderBy(file => file.Count(ch => ch == Path.DirectorySeparatorChar))
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var dllFile in dllFiles)
        {
            try
            {
                _ = AssemblyName.GetAssemblyName(dllFile);
                return dllFile;
            }
            catch
            {
                // 继续查找可读取程序集元数据的 DLL。
            }
        }

        throw new InvalidOperationException(_localizer["plugin.mainAssemblyNotFound"]);
    }

    private static bool IsHostSharedAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return true;
        }

        return assemblyName.Equals("DTSoft.Plugin.Abstractions", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals("DTSoft.AppService", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals("DTSoft.Models", StringComparison.OrdinalIgnoreCase);
    }

    private void ExtractZipSafely(Stream stream, string destinationDirectory)
    {
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(_localizer["plugin.packagePathInvalid"]);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath);
        }
    }

    private static string? TryResolvePathUnderPluginDirectory(string pluginDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var pluginRoot = Path.GetFullPath(pluginDirectory) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(pluginDirectory, relativePath));
        return fullPath.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
    }

    private static string SanitizePathPart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
    }

    private static bool TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CleanupEmptyParentDirectories(string deletedDirectory)
    {
        var pluginRoot = Path.GetFullPath(_pluginDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Directory.GetParent(Path.GetFullPath(deletedDirectory));

        while (current is not null)
        {
            var currentPath = current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (currentPath.Equals(pluginRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            try
            {
                if (Directory.Exists(currentPath) && !Directory.EnumerateFileSystemEntries(currentPath).Any())
                {
                    Directory.Delete(currentPath);
                    current = current.Parent;
                    continue;
                }
            }
            catch
            {
                break;
            }

            break;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativeDirectory));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceDirectory, file);
            var targetFile = Path.Combine(targetDirectory, relativeFile);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }
}

public sealed class DynamicWebApiPluginCatalogDocument
{
    public List<DynamicWebApiPluginCatalogItem> Plugins { get; set; } = [];
}

public sealed class DynamicWebApiPluginCatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public string? PluginName { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string MainAssembly { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LastLoadError { get; set; }
    public string? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
