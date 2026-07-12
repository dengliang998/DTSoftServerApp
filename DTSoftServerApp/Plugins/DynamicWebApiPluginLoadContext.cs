using System.Reflection;
using System.Runtime.Loader;

namespace DTSoftServerApp.Plugins;

internal sealed class DynamicWebApiPluginLoadContext : AssemblyLoadContext
{
    private const string PluginAbstractionsAssemblyName = "DTSoft.Plugin.Abstractions";
    private const string AppServiceAssemblyName = "DTSoft.AppService";
    private const string ModelsAssemblyName = "DTSoft.Models";

    private readonly AssemblyDependencyResolver _resolver;

    public DynamicWebApiPluginLoadContext(string mainAssemblyPath)
        : base($"DTSoft.Plugin:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsAllowedDtSoftAssembly(assemblyName.Name))
        {
            return TryGetDefaultAssembly(assemblyName.Name)
                   ?? throw new FileLoadException(
                       $"插件依赖的 {assemblyName.Name} 必须由宿主加载，不能从插件目录单独加载。",
                       assemblyName.Name);
        }

        if (IsBlockedDtSoftAssembly(assemblyName.Name))
        {
            throw new FileLoadException(
                $"插件只能直接引用 {PluginAbstractionsAssemblyName}、{AppServiceAssemblyName}、{ModelsAssemblyName}，不能直接引用 {assemblyName.Name}。",
                assemblyName.Name);
        }

        if (ShouldUseDefaultContext(assemblyName.Name) &&
            TryGetDefaultAssembly(assemblyName.Name) is { } sharedAssembly)
        {
            return sharedAssembly;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(assemblyPath);
    }

    private static bool ShouldUseDefaultContext(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        return IsAllowedDtSoftAssembly(assemblyName) ||
               assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Scalar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockedDtSoftAssembly(string? assemblyName)
    {
        return !string.IsNullOrWhiteSpace(assemblyName) &&
               assemblyName.StartsWith("DTSoft.", StringComparison.OrdinalIgnoreCase) &&
               !IsAllowedDtSoftAssembly(assemblyName);
    }

    private static bool IsAllowedDtSoftAssembly(string? assemblyName)
    {
        return !string.IsNullOrWhiteSpace(assemblyName) &&
               (assemblyName.Equals(PluginAbstractionsAssemblyName, StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals(AppServiceAssemblyName, StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals(ModelsAssemblyName, StringComparison.OrdinalIgnoreCase));
    }

    private static Assembly? TryGetDefaultAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        return AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }
}
