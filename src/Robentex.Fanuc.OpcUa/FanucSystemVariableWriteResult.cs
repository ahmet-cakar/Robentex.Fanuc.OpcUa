using Opc.Ua;

namespace Robentex.Fanuc.OpcUa;

/// <summary>Details of a verified system-variable write through a dynamic Holding Register assignment.</summary>
public sealed record FanucSystemVariableWriteResult<T>(
    T Value,
    IReadOnlyList<short> WrittenRegisters,
    IReadOnlyList<short> ReadBackRegisters,
    int StartAddress,
    string SetAsgCommand,
    StatusCode SetAsgStatus,
    bool SetAsgIssued,
    StatusCode WriteStatus);
