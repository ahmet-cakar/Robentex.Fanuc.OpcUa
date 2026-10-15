namespace Robentex.Fanuc.OpcUa;

public sealed class FanucOpcUaConnectionException : Exception
{
    public FanucOpcUaConnectionException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
