namespace Robentex.Fanuc.OpcUa;

/// <summary>FANUC TP program execution states defined by Additional Option Manual §55.3.7.</summary>
public enum FanucProgramExecutionState
{
    /// <summary>The task has been aborted.</summary>
    Aborted = 0,
    /// <summary>The task is paused.</summary>
    Paused = 1,
    /// <summary>The task is running.</summary>
    Running = 2
}

/// <summary>Read-only TP program execution status obtained from a FANUC PRG[n] mapping.</summary>
public sealed record FanucProgramStatus(
    int TaskNumber,
    string ProgramName,
    int LineNumber,
    FanucProgramExecutionState ExecutionStatus,
    string ParentProgramName);
