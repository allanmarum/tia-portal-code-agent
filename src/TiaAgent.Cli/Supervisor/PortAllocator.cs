using System;
using System.Net;
using System.Net.Sockets;

namespace TiaAgent.Cli.Supervisor;

public static class PortAllocator
{
    public const int DefaultBridgePort = 43119;
    public const int DefaultRuntimePort = 43120;
    public const int DefaultRangeStart = 43100;
    public const int DefaultRangeEnd = 43200;

    public static bool IsPortAvailable(int port)
    {
        if (port <= 0 || port > 65535) return false;
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static int GetAvailablePort(int preferredPort, int rangeStart = DefaultRangeStart, int rangeEnd = DefaultRangeEnd)
    {
        if (preferredPort > 0 && IsPortAvailable(preferredPort))
        {
            return preferredPort;
        }

        for (int p = rangeStart; p <= rangeEnd; p++)
        {
            if (IsPortAvailable(p))
            {
                return p;
            }
        }

        throw new InvalidOperationException($"No available port found in loopback range {rangeStart}-{rangeEnd}.");
    }
}
