using System.Reflection;

namespace ShipFlow.Infrastructure;

/// <summary>Marker used for assembly scanning and architecture tests.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
