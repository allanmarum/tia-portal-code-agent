#if SIEMENS
using System;
using TiaAgent.AddIn.Bridge;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn;

/// <summary>
/// Static service locator for the TIA Portal Add-In.
/// Initialized once when the Add-In loads; provides access to Bridge client
/// without requiring constructor injection (TIA Portal instantiates Add-In classes directly).
/// </summary>
public static class AddInServices
{
    private static IAgentBridgeClient? _bridgeClient;
    private static BridgeClientConfig? _config;
    private static readonly object _lock = new();

    static AddInServices()
    {
        AddInLogger.Startup();
        AddInLogger.Info("AddInServices static constructor called");
    }

    /// <summary>
    /// Gets the Bridge client configuration, loading it lazily on first access.
    /// </summary>
    public static BridgeClientConfig Config
    {
        get
        {
            if (_config == null)
            {
                lock (_lock)
                {
                    _config ??= BridgeClientConfig.Load();
                }
            }
            return _config;
        }
    }

    /// <summary>
    /// Gets the Bridge client, initializing it lazily on first access.
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

    /// <summary>
    /// Allows tests or initialization code to override the Bridge client.
    /// </summary>
    public static void SetBridgeClient(IAgentBridgeClient client)
    {
        lock (_lock)
        {
            _bridgeClient = client;
        }
    }
}
#endif
