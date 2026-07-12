using System.Reflection;

namespace DTSoftServerApp.Plugins;

public sealed record DynamicWebApiPluginLoadResult(
    IReadOnlyList<Assembly> Assemblies,
    IReadOnlyList<DynamicWebApiPluginDescriptor> Plugins,
    IReadOnlyList<DynamicWebApiPluginLoadFailure> Failures)
{
    public static DynamicWebApiPluginLoadResult Empty { get; } =
        new(Array.Empty<Assembly>(), Array.Empty<DynamicWebApiPluginDescriptor>(), Array.Empty<DynamicWebApiPluginLoadFailure>());
}

public sealed record DynamicWebApiPluginLoadFailure(
    string FilePath,
    string Message,
    string? ExceptionType = null);
