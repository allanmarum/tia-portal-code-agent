#if SIEMENS
using System;
using Siemens.Engineering;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Providers;

/// <summary>
/// Captures a SelectionSnapshot from IEngineeringObject using defensive reflection.
/// Each property access is wrapped in try/catch to handle missing properties gracefully.
/// </summary>
public static class SelectionSnapshotFactory
{
    public static SelectionSnapshot Create(IEngineeringObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        return new SelectionSnapshot
        {
            Name = GetProperty(obj, "Name") ?? obj.ToString() ?? "Unknown",
            ObjectType = GetProperty(obj, "ObjectType") ?? obj.GetType().Name,
            RuntimeType = GetProperty(obj, "RuntimeType") ?? obj.GetType().FullName ?? "",
            PlcName = GetProperty(obj, "PlcName") ?? "",
            TiaPath = GetTiaPath(obj),
            Language = GetProperty(obj, "Language") ?? ""
        };
    }

    private static string? GetProperty(object obj, string propertyName)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            return prop?.GetValue(obj) as string;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to read property '{propertyName}' from {obj.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string GetTiaPath(IEngineeringObject obj)
    {
        try
        {
            var path = obj.GetType().GetProperty("Path");
            if (path != null)
            {
                var value = path.GetValue(obj);
                if (value != null)
                    return value.ToString() ?? "";
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to read TIA path: {ex.Message}");
        }

        return obj.ToString() ?? "";
    }
}
#endif
