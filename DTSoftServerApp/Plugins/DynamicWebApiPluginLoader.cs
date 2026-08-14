using DTSoft.AppService.Localization;
using DTSoft.Plugin.Abstractions;
using DTSoftServerApp.Extensions;
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
        DynamicWebApiPluginCatalog.CleanupShadowCopies(configuration);

        var sharedAssemblyNames = AssemblyLoadContext.Default.Assemblies
            .Select(assembly => assembly.GetName().Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var legacyPluginFiles = Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(file => !sharedAssemblyNames.Contains(Path.GetFileNameWithoutExtension(file)))
            .ToArray();
        var managedPluginFiles = DynamicWebApiPluginCatalog.GetEnabledPluginAssemblies(configuration)
            .Where(file => !sharedAssemblyNames.Contains(Path.GetFileNameWithoutExtension(file)));
        var managedPluginFileSet = managedPluginFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pluginFiles = legacyPluginFiles
            .Concat(managedPluginFileSet)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pluginFiles.Length == 0)
        {
            var emptyLoadLocalizer = LocalizationConfigurationExtensions.CreateAppLocalizer(configuration);
            DynamicWebApiPluginCatalog.UpdateEffectiveStatuses(configuration, DynamicWebApiPluginLoadResult.Empty, emptyLoadLocalizer);
            return DynamicWebApiPluginLoadResult.Empty;
        }

        var assemblies = new List<Assembly>();
        var plugins = new List<DynamicWebApiPluginDescriptor>();
        var failures = new List<DynamicWebApiPluginLoadFailure>();
        var localizer = LocalizationConfigurationExtensions.CreateAppLocalizer(configuration);

        foreach (var filePath in pluginFiles)
        {
            try
            {
                var isManagedPlugin = managedPluginFileSet.Contains(filePath);
                var loadFilePath = isManagedPlugin
                    ? DynamicWebApiPluginCatalog.CreateShadowCopy(configuration, filePath)
                    : filePath;
                var loadContext = new DynamicWebApiPluginLoadContext(loadFilePath, localizer);
                var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(loadFilePath));
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
                    if (managedPluginFileSet.Contains(filePath))
                    {
                        failures.Add(new DynamicWebApiPluginLoadFailure(
                            filePath,
                            localizer["plugin.noPluginEntryFound"]));
                    }

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
                            localizer.Format("plugin.serviceRegistrationFailed", pluginType.FullName ?? pluginType.Name),
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

        var loadResult = new DynamicWebApiPluginLoadResult(assemblies, plugins, failures);
        DynamicWebApiPluginCatalog.UpdateEffectiveStatuses(configuration, loadResult, localizer);
        return loadResult;
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

    public static string ResolvePluginDirectory(IConfiguration configuration)
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
