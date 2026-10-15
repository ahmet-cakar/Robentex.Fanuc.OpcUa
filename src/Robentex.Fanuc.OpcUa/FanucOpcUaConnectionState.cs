namespace Robentex.Fanuc.OpcUa;

/// <summary>Lifecycle states of a FANUC OPC UA client session.</summary>
public enum FanucOpcUaConnectionState
{
    /// <summary>No session is active.</summary>
    Disconnected,
    /// <summary>A session is being established.</summary>
    Connecting,
    /// <summary>An active session is available.</summary>
    Connected,
    /// <summary>An active session is being closed.</summary>
    Disconnecting,
    /// <summary>The most recent connection attempt or keep-alive fault failed.</summary>
    Faulted
}
