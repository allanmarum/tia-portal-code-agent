#if SIEMENS
using System;
using Siemens.Engineering;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Providers;

/// <summary>
/// Captures a SelectionSnapshot from IEngineeringObject.
/// Uses direct interface members only — no reflection (reflection on COM-interop
/// types triggers VerificationException in TIA Portal's partial-trust sandbox).
/// </summary>
public static class SelectionSnapshotFactory
{
    public static SelectionSnapshot Create(IEngineeringObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var name = GetSafeName(obj);
        var typeName = obj.GetType().Name;

        return new SelectionSnapshot
        {
            Name = name,
            ObjectType = typeName,
            RuntimeType = obj.GetType().FullName ?? "",
            PlcName = "",
            TiaPath = name,
            Language = ""
        };
    }

    /// <summary>
    /// Gets a human-readable name for the selected object.
    /// Uses ToString() — avoids reflection on COM-interop types which triggers
    /// VerificationException in TIA Portal's partial-trust sandbox.
    /// </summary>
    private static string GetSafeName(IEngineeringObject obj)
    {
        try
        {
            return obj.ToString() ?? "Unknown";
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to get name from {obj.GetType().Name}: {ex.Message}");
            return "Unknown";
        }
    }
}
#endif
