namespace Robentex.Fanuc.OpcUa;

/// <summary>Connection settings for a FANUC OPC UA server.</summary>
public sealed class FanucOpcUaConnectionOptions
{
    /// <summary>OPC UA endpoint to which the client connects.</summary>
    public Uri EndpointUrl { get; init; } = new("opc.tcp://127.0.0.1:4880");
    /// <summary>Explicitly permits selection of a security-free endpoint; disabled by default.</summary>
    public bool AllowInsecureConnection { get; init; }
    /// <summary>Application name presented during OPC UA session creation.</summary>
    public string ApplicationName { get; init; } = "Robentex.Fanuc.OpcUa.Client";
    /// <summary>OPC UA NodeId of the FANUC Command node.</summary>
    public string CommandNodeId { get; init; } = "ns=1;i=305";
    /// <summary>OPC UA NodeId of the FANUC HoldingRegisters Int16 array.</summary>
    public string HoldingRegistersNodeId { get; init; } = "ns=1;i=304";
    /// <summary>
    /// Explicit acknowledgement that the configured automatic range was reviewed against this controller's persistent
    /// $SNPX_ASG assignments and other client reservations. Disabled by default because the sample range is not universal.
    /// </summary>
    public bool DynamicAssignmentRangeVerified { get; init; }
    /// <summary>First inclusive 1-based Holding Register address usable for automatic assignments.</summary>
    public int DynamicAssignmentStartAddress { get; init; } = 10001;
    /// <summary>Last inclusive 1-based Holding Register address usable for automatic assignments.</summary>
    public int DynamicAssignmentEndAddress { get; init; } = 16384;
    /// <summary>Known occupied ranges that neither automatic nor manual dynamic SETASG assignments may use.</summary>
    public IReadOnlyList<FanucHoldingRegisterRange> ReservedHoldingRegisterRanges { get; init; } = [];
    /// <summary>Explicitly enables system-variable writes. Disabled by default for robot safety.</summary>
    public bool EnableSystemVariableWrites { get; init; }
    /// <summary>Exact FANUC system-variable names permitted for writes when writes are enabled.</summary>
    public IReadOnlySet<string> WritableSystemVariables { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Explicitly enables numeric R[] writes. Disabled by default for robot safety.</summary>
    public bool EnableRegisterWrites { get; init; }
    /// <summary>Exact FANUC R[] numbers permitted for writes when register writes are enabled.</summary>
    public IReadOnlySet<int> WritableRegisters { get; init; } = new HashSet<int>();
    /// <summary>Explicitly enables PR[] Cartesian writes. Disabled by default for robot safety.</summary>
    public bool EnablePositionRegisterWrites { get; init; }
    /// <summary>Exact FANUC PR[] numbers permitted for Cartesian writes when enabled.</summary>
    public IReadOnlySet<int> WritablePositionRegisters { get; init; } = new HashSet<int>();
    /// <summary>Explicitly enables digital-output writes. Disabled by default for robot safety.</summary>
    public bool EnableDigitalOutputWrites { get; init; }
    /// <summary>Exact FANUC DO[] numbers permitted for writes when enabled.</summary>
    public IReadOnlySet<int> WritableDigitalOutputs { get; init; } = new HashSet<int>();
    /// <summary>Root folder for the OPC UA application certificate and trust stores.</summary>
    public string PkiRootPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Robentex",
        "FanucOpcUa",
        "pki");
    /// <summary>Requested OPC UA session timeout.</summary>
    public TimeSpan SessionTimeout { get; init; } = TimeSpan.FromSeconds(60);
    /// <summary>Timeout for endpoint selection and individual OPC UA operations.</summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(15);

    internal void Validate()
    {
        if (EndpointUrl.Scheme != "opc.tcp")
            throw new ArgumentException("Only opc.tcp endpoints are supported.", nameof(EndpointUrl));
        if (SessionTimeout <= TimeSpan.Zero || SessionTimeout > TimeSpan.FromMilliseconds(uint.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(SessionTimeout));
        if (OperationTimeout <= TimeSpan.Zero || OperationTimeout > TimeSpan.FromMilliseconds(uint.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(OperationTimeout));
        if (string.IsNullOrWhiteSpace(ApplicationName))
            throw new ArgumentException("An application name is required.", nameof(ApplicationName));
        if (string.IsNullOrWhiteSpace(PkiRootPath))
            throw new ArgumentException("A PKI root path is required.", nameof(PkiRootPath));
        if (string.IsNullOrWhiteSpace(CommandNodeId))
            throw new ArgumentException("A Command NodeId is required.", nameof(CommandNodeId));
        if (string.IsNullOrWhiteSpace(HoldingRegistersNodeId))
            throw new ArgumentException("A HoldingRegisters NodeId is required.", nameof(HoldingRegistersNodeId));
        if (DynamicAssignmentStartAddress is < 1 or > 16384)
            throw new ArgumentOutOfRangeException(nameof(DynamicAssignmentStartAddress));
        if (DynamicAssignmentEndAddress < DynamicAssignmentStartAddress || DynamicAssignmentEndAddress > 16384)
            throw new ArgumentOutOfRangeException(nameof(DynamicAssignmentEndAddress));
        if (ReservedHoldingRegisterRanges is null)
            throw new ArgumentNullException(nameof(ReservedHoldingRegisterRanges));
        foreach (var range in ReservedHoldingRegisterRanges)
            range.Validate(nameof(ReservedHoldingRegisterRanges));
        if (WritableSystemVariables is null)
            throw new ArgumentNullException(nameof(WritableSystemVariables));
        if (WritableSystemVariables.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Writable system-variable names cannot be empty.", nameof(WritableSystemVariables));
        if (WritableRegisters is null)
            throw new ArgumentNullException(nameof(WritableRegisters));
        if (WritableRegisters.Any(number => number < 1))
            throw new ArgumentException("Writable R[] numbers must be greater than zero.", nameof(WritableRegisters));
        if (WritablePositionRegisters is null)
            throw new ArgumentNullException(nameof(WritablePositionRegisters));
        if (WritablePositionRegisters.Any(number => number < 1))
            throw new ArgumentException("Writable PR[] numbers must be greater than zero.", nameof(WritablePositionRegisters));
        if (WritableDigitalOutputs is null)
            throw new ArgumentNullException(nameof(WritableDigitalOutputs));
        if (WritableDigitalOutputs.Any(number => number is < 1 or > 10000))
            throw new ArgumentException("Writable DO[] numbers must be in the range 1 through 10000.", nameof(WritableDigitalOutputs));
    }
}
