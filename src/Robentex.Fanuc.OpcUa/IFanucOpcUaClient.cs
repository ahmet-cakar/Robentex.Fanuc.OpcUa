namespace Robentex.Fanuc.OpcUa;

public interface IFanucOpcUaClient : IDisposable, IAsyncDisposable
{
    FanucOpcUaConnectionState State { get; }
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FanucRobotStatusNode>> DiscoverRobotStatusNodesAsync(CancellationToken cancellationToken = default);
    Task<FanucRobotStatus> GetRobotStatusAsync(CancellationToken cancellationToken = default);
    Task ClearDynamicAssignmentsAsync(CancellationToken cancellationToken = default);
    Task ConfigureDynamicAssignmentAsync(FanucDynamicAssignment assignment, CancellationToken cancellationToken = default);
    Task<int> ReadGeneralOverrideAsync(CancellationToken cancellationToken = default);
    Task<FanucSystemVariableReadResult<T>> ReadSystemVariableAsync<T>(string variableName, CancellationToken cancellationToken = default);
    Task<FanucSystemVariableReadResult<T>> ReadSystemVariableAsync<T>(string variableName, int startAddress, CancellationToken cancellationToken = default);
    Task<FanucSystemVariableWriteResult<T>> WriteSystemVariableAsync<T>(string variableName, T value, CancellationToken cancellationToken = default);
    Task<double> ReadRegisterAsync(int registerNumber, CancellationToken cancellationToken = default);
    Task<FanucProgramStatus> ReadProgramStatusAsync(int taskNumber = 1, CancellationToken cancellationToken = default);
    Task WriteRegisterAsync(int registerNumber, double value, CancellationToken cancellationToken = default);
    Task<bool> ReadDigitalInputAsync(int number, CancellationToken cancellationToken = default);
    Task<bool> ReadDigitalOutputAsync(int number, CancellationToken cancellationToken = default);
    Task<bool> ReadRobotInputAsync(int number, CancellationToken cancellationToken = default);
    Task<bool> ReadRobotOutputAsync(int number, CancellationToken cancellationToken = default);
    Task<ushort> ReadGroupInputAsync(int number, CancellationToken cancellationToken = default);
    Task<ushort> ReadGroupOutputAsync(int number, CancellationToken cancellationToken = default);
    Task WriteDigitalOutputAsync(int number, bool value, CancellationToken cancellationToken = default);
    Task<FanucPosition> ReadPositionRegisterAsync(int number, CancellationToken cancellationToken = default);
    Task<FanucPositionRegisterConfiguration> ReadPositionRegisterConfigurationAsync(int number, CancellationToken cancellationToken = default);
    Task WritePositionRegisterAsync(int number, FanucPosition position, CancellationToken cancellationToken = default);
}
