namespace Robentex.Fanuc.OpcUa;

/// <summary>Asynchronous, UI-independent FANUC OPC UA client contract.</summary>
public interface IFanucOpcUaClient : IDisposable, IAsyncDisposable
{
    /// <summary>Gets the current connection lifecycle state.</summary>
    FanucOpcUaConnectionState State { get; }
    /// <summary>Gets whether the underlying OPC UA session is currently connected.</summary>
    bool IsConnected { get; }
    /// <summary>Opens an OPC UA session using the configured endpoint and security policy.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);
    /// <summary>Closes the current OPC UA session and clears session-scoped mappings.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    /// <summary>Discovers optional FANUC robot-status nodes without assuming their NodeIds.</summary>
    Task<IReadOnlyList<FanucRobotStatusNode>> DiscoverRobotStatusNodesAsync(CancellationToken cancellationToken = default);
    /// <summary>Reads the currently discoverable FANUC robot-status snapshot.</summary>
    Task<FanucRobotStatus> GetRobotStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>Clears dynamic assignments belonging only to this OPC UA session.</summary>
    Task ClearDynamicAssignmentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates one explicit connection-scoped dynamic SETASG assignment.</summary>
    Task ConfigureDynamicAssignmentAsync(FanucDynamicAssignment assignment, CancellationToken cancellationToken = default);
    /// <summary>Reads the signed INTEGER value of <c>$MCR.$GENOVERRIDE</c>.</summary>
    Task<int> ReadGeneralOverrideAsync(CancellationToken cancellationToken = default);
    /// <summary>Automatically maps and reads an INTEGER, REAL, or BOOLEAN system variable.</summary>
    Task<FanucSystemVariableReadResult<T>> ReadSystemVariableAsync<T>(string variableName, CancellationToken cancellationToken = default);
    /// <summary>Maps and reads an INTEGER, REAL, or BOOLEAN system variable at an explicit address.</summary>
    Task<FanucSystemVariableReadResult<T>> ReadSystemVariableAsync<T>(string variableName, int startAddress, CancellationToken cancellationToken = default);
    /// <summary>Writes an allow-listed system variable and verifies its raw register read-back.</summary>
    Task<FanucSystemVariableWriteResult<T>> WriteSystemVariableAsync<T>(string variableName, T value, CancellationToken cancellationToken = default);
    /// <summary>Reads one FANUC numeric R[] register.</summary>
    Task<double> ReadRegisterAsync(int registerNumber, CancellationToken cancellationToken = default);
    /// <summary>Reads one FANUC TP task program-execution status.</summary>
    Task<FanucProgramStatus> ReadProgramStatusAsync(int taskNumber = 1, CancellationToken cancellationToken = default);
    /// <summary>Writes an allow-listed FANUC numeric R[] register and verifies read-back.</summary>
    Task WriteRegisterAsync(int registerNumber, double value, CancellationToken cancellationToken = default);
    /// <summary>Reads one digital input DI[].</summary>
    Task<bool> ReadDigitalInputAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Reads one digital output DO[] without changing it.</summary>
    Task<bool> ReadDigitalOutputAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Reads one robot input RI[].</summary>
    Task<bool> ReadRobotInputAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Reads one robot output RO[] without changing it.</summary>
    Task<bool> ReadRobotOutputAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Reads one group input GI[] as an unsigned 16-bit value.</summary>
    Task<ushort> ReadGroupInputAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Reads one group output GO[] as an unsigned 16-bit value without changing it.</summary>
    Task<ushort> ReadGroupOutputAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Writes an allow-listed digital output DO[] and verifies read-back.</summary>
    Task WriteDigitalOutputAsync(int number, bool value, CancellationToken cancellationToken = default);
    /// <summary>Reads Cartesian X/Y/Z/W/P/R values from one position register PR[].</summary>
    Task<FanucPosition> ReadPositionRegisterAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Reads raw documented CONFIG fields from one position register PR[].</summary>
    Task<FanucPositionRegisterConfiguration> ReadPositionRegisterConfigurationAsync(int number, CancellationToken cancellationToken = default);
    /// <summary>Writes an allow-listed position register PR[] and verifies read-back.</summary>
    Task WritePositionRegisterAsync(int number, FanucPosition position, CancellationToken cancellationToken = default);
}
