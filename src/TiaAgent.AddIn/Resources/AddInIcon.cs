#if SIEMENS
namespace TiaAgent.AddIn.Resources;

/// <summary>
/// Icon placeholder for the TIA Portal Add-In.
///
/// The previous implementation used Icon.FromHandle(GetHicon()) which requires
/// SecurityPermissionFlag.UnmanagedCode — unavailable in TIA Portal's partial-trust
/// sandbox and causes "Operation could destabilize the runtime" VerificationException.
///
/// Context menu items render without an icon. This is cosmetic and does not affect functionality.
/// </summary>
internal static class AddInIcon
{
    // Icon loading removed: GetHicon() requires UnmanagedCode permission
    // which is not granted in TIA Portal's sandbox.
}
#endif
