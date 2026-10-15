namespace Robentex.Fanuc.OpcUa;

/// <summary>Known textual FANUC robot operation modes. Numeric ModeState values remain available as raw data.</summary>
public enum FanucRobotMode
{
    Unknown,
    Auto,
    T1,
    T2
}

/// <summary>
/// Read-only snapshot of the optional FANUC HMI Device Communication robot-status nodes.
/// Nullable values represent a missing node, a non-Good OPC UA value status, or a value whose representation was not documented.
/// </summary>
public sealed record FanucRobotStatus(
    uint? OperationStateRaw,
    bool? EmergencyStop,
    bool? Abort,
    bool? ProgramRunning,
    bool? Paused,
    bool? Held,
    bool? Fault,
    bool? TeachPendantEnabled,
    bool? BatteryAlarm,
    bool? Busy,
    bool? ServoOn,
    FanucRobotMode? Mode,
    object? ModeStateRaw,
    int? ProgramSpeedPercent,
    string[]? Alarm,
    IReadOnlyList<FanucRobotStatusNode> Nodes);
