namespace Robentex.Fanuc.OpcUa;

/// <summary>Represents a failure to establish or maintain an OPC UA connection.</summary>
public sealed class FanucOpcUaConnectionException : Exception
{
    /// <summary>Initializes a connection exception with an optional underlying failure.</summary>
    public FanucOpcUaConnectionException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
