using System;

namespace DTSoft.Plugin.Abstractions;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DynamicWebApiPluginAttribute : Attribute
{
    public DynamicWebApiPluginAttribute(string? name = null)
    {
        Name = name;
    }

    public string? Name { get; }

    public string? Description { get; init; }

    public string? Version { get; init; }
}
