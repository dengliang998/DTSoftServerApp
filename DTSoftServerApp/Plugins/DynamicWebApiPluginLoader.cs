using DTSoft.Plugin.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;

namespace DTSoftServerApp.Plugins;

public static class DynamicWebApiPluginLoader
{
    private const string PluginDirectoryKey = "DynamicWebApi:PluginDirectory";
    private const string PluginEnabledKey = "DynamicWebApi:Enabled";

    public static DynamicWebApiPluginLoadResult Load(IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool?>(PluginEnabledKey) == false)
        {
            return DynamicWebApiPluginLoadResult.Empty;
        }

        var pluginDirectory = ResolvePluginDirectory(configuration);
        Directory.CreateDirectory(pluginDirectory);

        var sharedAssemblyNames = AssemblyLoadContext.Default.Assemblies
            .Select(assembly => assembly.GetName().Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pluginFiles = Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(file => !sharedAssemblyNames.Contains(Path.GetFileNameWithoutExtension(file)))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pluginFiles.Length == 0)
        {
            return DynamicWebApiPluginLoadResult.Empty;
        }

        var assemblies = new List<Assembly>();
        var plugins = new List<DynamicWebApiPluginDescriptor>();
        var failures = new List<DynamicWebApiPluginLoadFailure>();

        foreach (var filePath in pluginFiles)
        {
            try
            {
                var loadContext = new DynamicWebApiPluginLoadContext(filePath);
                var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(filePath));
                var loadableTypes = GetLoadableTypes(assembly).ToArray();

                var controllerTypes = loadableTypes
                    .Where(IsControllerType)
                    .Select(type => type.FullName ?? type.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var moduleTypes = loadableTypes
                    .Where(type => typeof(IDynamicWebApiPlugin).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true, IsPublic: true })
                    .Select(type => type.FullName ?? type.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var entityModelConfigurationTypes = loadableTypes
                    .Where(type => typeof(IPluginEntityModelConfiguration).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true, IsPublic: true })
                    .ToArray();

                if (controllerTypes.Length == 0 && moduleTypes.Length == 0 && entityModelConfigurationTypes.Length == 0)
                {
                    continue;
                }

                foreach (var configurationType in entityModelConfigurationTypes)
                {
                    services.AddSingleton(typeof(IPluginEntityModelConfiguration), configurationType);
                }

                foreach (var pluginType in loadableTypes
                             .Where(type => typeof(IDynamicWebApiPlugin).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true, IsPublic: true }))
                {
                    try
                    {
                        if (Activator.CreateInstance(pluginType) is IDynamicWebApiPlugin plugin)
                        {
                            plugin.ConfigureServices(services, configuration);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new DynamicWebApiPluginLoadFailure(
                            filePath,
                            $"插件服务注册失败：{pluginType.FullName}",
                            ex.GetType().FullName));
                    }
                }

                assemblies.Add(assembly);

                var pluginName = assembly.GetCustomAttribute<DynamicWebApiPluginAttribute>()?.Name
                    ?? loadableTypes.Select(type => type.GetCustomAttribute<DynamicWebApiPluginAttribute>()).FirstOrDefault(attr => attr is not null)?.Name
                    ?? assembly.GetName().Name;

                plugins.Add(new DynamicWebApiPluginDescriptor(
                    assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(filePath),
                    filePath,
                    pluginName,
                    controllerTypes,
                    moduleTypes));
            }
            catch (Exception ex)
            {
                failures.Add(new DynamicWebApiPluginLoadFailure(
                    filePath,
                    ex.Message,
                    ex.GetType().FullName));
            }
        }

        return new DynamicWebApiPluginLoadResult(assemblies, plugins, failures);
    }

    public static void RegisterApplicationParts(IMvcBuilder mvcBuilder, DynamicWebApiPluginLoadResult loadResult)
    {
        mvcBuilder.ConfigureApplicationPartManager(manager =>
        {
            foreach (var assembly in loadResult.Assemblies)
            {
                if (manager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == assembly))
                {
                    continue;
                }

                manager.ApplicationParts.Add(new AssemblyPart(assembly));
            }
        });
    }

    private static string ResolvePluginDirectory(IConfiguration configuration)
    {
        var configuredPath = configuration[PluginDirectoryKey];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "UserDll";
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private static bool IsControllerType(Type type)
    {
        return type is { IsAbstract: false, IsClass: true, IsPublic: true } &&
               typeof(ControllerBase).IsAssignableFrom(type);
    }
}
