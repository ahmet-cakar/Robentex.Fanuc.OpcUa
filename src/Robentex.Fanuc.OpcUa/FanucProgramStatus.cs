namespace Robentex.Fanuc.OpcUa;

/// <summary>FANUC TP program execution states defined by Additional Option Manual §55.3.7.</summary>
public enum FanucProgramExecutionState
{
    Aborted = 0,
    Paused = 1,
    Running = 2
}

/// <summary>Read-only TP program execution status obtained from a FANUC PRG[n] mapping.</summary>
public sealed record FanucProgramStatus(
    int TaskNumber,
    string ProgramName,
    int LineNumber,
    FanucProgramExecutionState ExecutionStatus,
    string ParentProgramName);
