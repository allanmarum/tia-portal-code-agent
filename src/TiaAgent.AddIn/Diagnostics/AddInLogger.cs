namespace TiaAgent.AddIn.Diagnostics;

/// <summary>
/// Minimal logger — all operations are no-ops to avoid file I/O in the partial-trust sandbox.
/// File I/O requires FileIOPermission which is not declared in Config.xml.
/// </summary>
public static class AddInLogger
{
    public static void Info(string message) { }
    public static void Warn(string message) { }
    public static void Debug(string message) { }
    public static void Error(string message, System.Exception? ex = null) { }
    public static void Startup() { }
}
