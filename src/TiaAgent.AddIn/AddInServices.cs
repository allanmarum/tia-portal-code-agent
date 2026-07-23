#if SIEMENS
using System;
using TiaAgent.AddIn.Bridge;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn;

/// <summary>
/// Service locator for the TIA Portal Add-In.
/// Configuration is loaded lazily on first access — no file I/O at assembly load time.
/// </summary>
public static class AddInServices
{
    private static IAgentBridgeClient? _bridgeClient;
    private static AddInConfig? _config;
    private static readonly object _lock = new();

    /// <summary>
    /// Bridge client configuration. Loaded lazily from hardcoded defaults.
    /// No file I/O — avoids FileIOPermission in partial-trust sandbox.
    /// </summary>
    public static AddInConfig Config
    {
        get
        {
            if (_config == null)
            {
                lock (_lock)
                {
                    _config ??= new AddInConfig();
                }
            }
            return _config;
        }
    }

    /// <summary>
    /// Bridge HTTP client. Created lazily on first access.
    /// </summary>
    public static IAgentBridgeClient BridgeClient
    {
        get
        {
            if (_bridgeClient == null)
            {
                lock (_lock)
                {
                    _bridgeClient ??= new AgentBridgeClient(Config);
                }
            }
            return _bridgeClient;
        }
    }
}
#endif
