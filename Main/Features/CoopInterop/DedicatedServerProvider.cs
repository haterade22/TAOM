using System;
using System.Reflection;

namespace TAOM.Features.CoopInterop;

/// <inheritdoc />
public sealed class DedicatedServerProvider : IDedicatedServerProvider
{
    // Bannerlord loads a module's binaries from the folder matching the running engine build:
    // Win64_Shipping_Client for the game, Win64_Shipping_Server for the dedicated server. So the
    // folder THIS assembly was loaded from is a direct statement of which one we are — no probing
    // of another mod's state, no guessing from campaign contents, and it cannot change mid-process.
    private const string ServerBinariesFolder = "Win64_Shipping_Server";

    private readonly Lazy<bool> _isDedicatedServer = new(Detect);

    /// <inheritdoc />
    public bool IsDedicatedServer => _isDedicatedServer.Value;

    private static bool Detect()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            return !string.IsNullOrEmpty(location)
                && location.IndexOf(ServerBinariesFolder, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            // Fail to "not a server": the gates built on this only ever SUPPRESS behaviour, so an
            // unreadable location must leave every client and single-player session exactly as-is.
            return false;
        }
    }
}
