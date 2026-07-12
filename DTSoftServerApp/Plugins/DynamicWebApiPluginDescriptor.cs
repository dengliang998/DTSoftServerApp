namespace DTSoftServerApp.Plugins;

public sealed record DynamicWebApiPluginDescriptor(
    string AssemblyName,
    string FilePath,
    string? PluginName,
    IReadOnlyList<string> ControllerTypes,
    IReadOnlyList<string> ModuleTypes);
