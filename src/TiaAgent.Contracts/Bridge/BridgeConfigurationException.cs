namespace TiaAgent.Contracts.Bridge;

public class BridgeConfigurationException : Exception
{
    public BridgeConfigurationException(string message) : base(message) { }
    public BridgeConfigurationException(string message, Exception inner) : base(message, inner) { }
}
