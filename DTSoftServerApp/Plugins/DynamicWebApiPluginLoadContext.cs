using DTSoft.AppService.Localization;
using System.Reflection;
using System.Runtime.Loader;

namespace DTSoftServerApp.Plugins;

internal sealed class DynamicWebApiPluginLoadContext : AssemblyLoadContext
{
    private const string PluginAbstractionsAssemblyName = "DTSoft.Plugin.Abstractions";
    private const string AppServiceAssemblyName = "DTSoft.AppService";
    private const string ModelsAssemblyName = "DTSoft.Models";
    private readonly AssemblyDependencyResolver _resolver;
    private readonly IAppLocalizer _localizer;

    public DynamicWebApiPluginLoadContext(string mainAssemblyPath, IAppLocalizer localizer)
        : base($"DTSoft.Plugin:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _localizer = localizer;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsAllowedDtSoftAssembly(assemblyName.Name))
        {
            return TryGetDefaultAssembly(assemblyName.Name)
                   ?? throw new FileLoadException(
                       _localizer.Format("plugin.dependencyMustBeHostLoaded", assemblyName.Name ?? string.Empty),
                       assemblyName.Name);
        }

        if (IsBlockedDtSoftAssembly(assemblyName.Name))
        {
            throw new FileLoadException(
                _localizer.Format(
                    "plugin.directReferenceForbidden",
                    PluginAbstractionsAssemblyName,
                    AppServiceAssemblyName,
                    ModelsAssemblyName,
                    assemblyName.Name ?? string.Empty),
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
