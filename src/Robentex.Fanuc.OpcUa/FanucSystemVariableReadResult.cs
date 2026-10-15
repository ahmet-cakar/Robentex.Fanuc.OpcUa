using Opc.Ua;

namespace Robentex.Fanuc.OpcUa;

/// <summary>The value, raw Holding Registers, and command outcome for one system-variable read.</summary>
public sealed record FanucSystemVariableReadResult<T>(
    T Value,
    IReadOnlyList<short> Registers,
    int StartAddress,
    string SetAsgCommand,
    StatusCode SetAsgStatus,
    bool SetAsgIssued);
