using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace Robentex.Fanuc.OpcUa;

/// <summary>Owns one OPC UA session. Register access is intentionally out of scope.</summary>
public sealed class FanucOpcUaClient : IFanucOpcUaClient
{
    private readonly FanucOpcUaConnectionOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _mappingGate = new(1, 1);
    private readonly SemaphoreSlim _digitalOutputWriteGate = new(1, 1);
    private readonly object _targetWriteLocksGate = new();
    private readonly List<FanucDynamicAssignment> _dynamicAssignments = [];
    private readonly Dictionary<string, FanucDynamicAssignment> _automaticAssignments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TargetWriteLockEntry> _targetWriteLocks = new(StringComparer.Ordinal);
    private ISession? _session;
    private bool _dynamicAssignmentsInitialized;
    private int _disposed;
    private volatile FanucOpcUaConnectionState _state;

    public FanucOpcUaClient(FanucOpcUaConnectionOptions? options = null)
    {
        _options = options ?? new FanucOpcUaConnectionOptions();
        _options.Validate();
    }

    public FanucOpcUaConnectionState State => _state;
    public bool IsConnected => _state == FanucOpcUaConnectionState.Connected && _session?.Connected == true;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected) return;
            if (_state is FanucOpcUaConnectionState.Connecting or FanucOpcUaConnectionState.Disconnecting)
                throw new InvalidOperationException("A connection state transition is already in progress.");

            // A faulted or transport-disconnected session is not reusable. Its dynamic assignments are
            // connection-scoped, so dispose it and clear the cache before establishing a replacement.
            if (_session is not null)
            {
                _state = FanucOpcUaConnectionState.Disconnecting;
                await CloseSessionBestEffortAsync().ConfigureAwait(false);
                _state = FanucOpcUaConnectionState.Disconnected;
            }

            _state = FanucOpcUaConnectionState.Connecting;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var configuration = CreateConfiguration();
                await configuration.ValidateAsync(ApplicationType.Client, cancellationToken).ConfigureAwait(false);
                await EnsureApplicationCertificateAsync(configuration, cancellationToken).ConfigureAwait(false);
                var endpoint = await CoreClientUtils.SelectEndpointAsync(
                    configuration,
                    _options.EndpointUrl.ToString(),
                    !_options.AllowInsecureConnection,
                    (int)_options.OperationTimeout.TotalMilliseconds,
                    null!,
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new FanucOpcUaConnectionException("The server did not return a compatible OPC UA endpoint.");

                if (!_options.AllowInsecureConnection && endpoint.SecurityMode == MessageSecurityMode.None)
                    throw new FanucOpcUaConnectionException("The server did not provide a secure endpoint. Set AllowInsecureConnection to true only for an explicitly trusted test environment.");

                var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(configuration));
                cancellationToken.ThrowIfCancellationRequested();
                _session = await new DefaultSessionFactory(null!).CreateAsync(
                    configuration,
                    configuredEndpoint,
                    false,
                    false,
                    _options.ApplicationName,
                    (uint)_options.SessionTimeout.TotalMilliseconds,
                    new UserIdentity(new AnonymousIdentityToken()),
                    null,
                    cancellationToken).ConfigureAwait(false);

                _session.KeepAlive += OnKeepAlive;
                _state = FanucOpcUaConnectionState.Connected;
            }
            catch (OperationCanceledException)
            {
                await CloseSessionBestEffortAsync().ConfigureAwait(false);
                _state = FanucOpcUaConnectionState.Disconnected;
                throw;
            }
            catch (Exception ex)
            {
                await CloseSessionBestEffortAsync().ConfigureAwait(false);
                _state = FanucOpcUaConnectionState.Faulted;
                throw new FanucOpcUaConnectionException($"Could not connect to '{_options.EndpointUrl}'.", ex);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is null) { _state = FanucOpcUaConnectionState.Disconnected; return; }
            _state = FanucOpcUaConnectionState.Disconnecting;
            try
            {
                await CloseSessionAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // CloseSessionAsync always disposes and clears session-scoped state in its finally block.
                // Do not leave callers permanently blocked in Disconnecting when CloseAsync faults or cancels.
                _state = FanucOpcUaConnectionState.Disconnected;
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Browses the optional FANUC §55.5 RobotInformation and MotionDeviceSystem status nodes.
    /// NodeIds are discovered from the server; no FANUC-specific NodeId is assumed.
    /// </summary>
    public async Task<IReadOnlyList<FanucRobotStatusNode>> DiscoverRobotStatusNodesAsync(CancellationToken cancellationToken = default)
    {
        var session = GetConnectedSession();
        var rootReferences = await BrowseChildrenAsync(session, ObjectIds.ObjectsFolder, cancellationToken).ConfigureAwait(false);
        var robotInformation = FindReference(rootReferences, "RobotInformation");
        var motionDeviceSystem = FindReference(rootReferences, "MotionDeviceSystem");

        IReadOnlyList<DiscoveredReference> robotInformationNodes = robotInformation is null
            ? []
            : await BrowseDescendantsAsync(session, robotInformation.NodeId, robotInformation.Path, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DiscoveredReference> motionDeviceSystemNodes = motionDeviceSystem is null
            ? []
            : await BrowseDescendantsAsync(session, motionDeviceSystem.NodeId, motionDeviceSystem.Path, cancellationToken).ConfigureAwait(false);
        var safetyStateNodes = motionDeviceSystemNodes
            .Where(node => node.Path.Contains("/SafetyStates/", StringComparison.Ordinal))
            .ToArray();

        var requested = new (string Name, IReadOnlyList<DiscoveredReference> Candidates)[]
        {
            ("OperationState", robotInformationNodes),
            ("ServoState", robotInformationNodes),
            ("ModeState", robotInformationNodes),
            ("ProgramSpeed", robotInformationNodes),
            ("Alarm", robotInformationNodes),
            ("OperationalMode", safetyStateNodes),
            ("EmergencyStop", safetyStateNodes),
            ("ProtectiveStop", safetyStateNodes)
        };

        var found = requested
            .Select(item => (item.Name, Reference: item.Candidates.FirstOrDefault(candidate => string.Equals(candidate.BrowseName, item.Name, StringComparison.Ordinal))))
            .Where(item => item.Reference is not null)
            .ToArray();
        var readValues = new ReadValueIdCollection();
        foreach (var item in found)
        {
            readValues.Add(new ReadValueId { NodeId = item.Reference!.NodeId, AttributeId = Attributes.Value });
            readValues.Add(new ReadValueId { NodeId = item.Reference.NodeId, AttributeId = Attributes.DataType });
        }

        DataValueCollection? values = null;
        if (readValues.Count > 0)
        {
            var response = await session.ReadAsync(null, 0, TimestampsToReturn.Neither, readValues, cancellationToken).ConfigureAwait(false);
            values = response.Results;
            if (values.Count != readValues.Count)
                throw new FanucOpcUaConnectionException("Robot-status discovery returned an unexpected number of OPC UA read results.");
        }

        var results = new List<FanucRobotStatusNode>(requested.Length);
        foreach (var item in requested)
        {
            var reference = found.FirstOrDefault(foundItem => string.Equals(foundItem.Name, item.Name, StringComparison.Ordinal)).Reference;
            if (reference is null)
            {
                results.Add(new FanucRobotStatusNode(item.Name, false));
                continue;
            }

            var index = Array.FindIndex(found, foundItem => string.Equals(foundItem.Name, item.Name, StringComparison.Ordinal)) * 2;
            var valueResult = values![index];
            var dataTypeResult = values[index + 1];
            results.Add(new FanucRobotStatusNode(
                item.Name,
                true,
                reference.Path,
                reference.NodeId.ToString(),
                dataTypeResult.Value is NodeId dataType ? dataType.ToString() : null,
                valueResult.Value?.GetType().FullName,
                valueResult.Value,
                valueResult.StatusCode,
                dataTypeResult.StatusCode));
        }

        return results;
    }

    /// <summary>
    /// Reads a snapshot of the optional FANUC §55.5 status values using nodes resolved by browse.
    /// No FANUC NodeId is assumed. A nullable field is unavailable or could not be safely interpreted.
    /// </summary>
    public async Task<FanucRobotStatus> GetRobotStatusAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await DiscoverRobotStatusNodesAsync(cancellationToken).ConfigureAwait(false);
        var operationState = GetStatusNode(nodes, "OperationState");
        uint? operationStateRaw = TryGetUInt32(operationState, out var rawOperationState) ? rawOperationState : null;
        var modeState = GetStatusNode(nodes, "ModeState");

        return new FanucRobotStatus(
            operationStateRaw,
            GetOperationStateBit(operationStateRaw, 1),
            GetOperationStateBit(operationStateRaw, 2),
            GetOperationStateBit(operationStateRaw, 4),
            GetOperationStateBit(operationStateRaw, 8),
            GetOperationStateBit(operationStateRaw, 16),
            GetOperationStateBit(operationStateRaw, 32),
            GetOperationStateBit(operationStateRaw, 64),
            GetOperationStateBit(operationStateRaw, 128),
            GetOperationStateBit(operationStateRaw, 256),
            GetServoState(GetStatusNode(nodes, "ServoState")),
            GetRobotMode(modeState),
            HasGoodValue(modeState) ? modeState!.Value : null,
            GetProgramSpeed(GetStatusNode(nodes, "ProgramSpeed")),
            GetAlarm(GetStatusNode(nodes, "Alarm")),
            nodes);
    }

    /// <summary>Clears only this session's dynamic assignments by issuing one CLRASG command.</summary>
    public async Task ClearDynamicAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        await _mappingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteCommandAsync("CLRASG", cancellationToken).ConfigureAwait(false);
            _dynamicAssignments.Clear();
            _automaticAssignments.Clear();
            _dynamicAssignmentsInitialized = true;
        }
        finally { _mappingGate.Release(); }
    }

    /// <summary>Issues SETASG and records its collision-free address range for this OPC UA session.</summary>
    public async Task ConfigureDynamicAssignmentAsync(FanucDynamicAssignment assignment, CancellationToken cancellationToken = default)
    {
        await ConfigureDynamicAssignmentCoreAsync(assignment, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maps and reads a FANUC system variable as Int32, Single, or Boolean.</summary>
    public async Task<FanucSystemVariableReadResult<T>> ReadSystemVariableAsync<T>(
        string variableName,
        int startAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        var assignment = CreateSystemVariableAssignment<T>(variableName, startAddress);
        var command = assignment.ToSetAsgCommand();
        var configuration = await ConfigureDynamicAssignmentCoreAsync(assignment, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        var value = ConvertSystemVariable<T>(registers, variableName);
        return new FanucSystemVariableReadResult<T>(value, registers, assignment.StartAddress, command, configuration.Status, configuration.CommandIssued);
    }

    public async Task<FanucSystemVariableReadResult<T>> ReadSystemVariableAsync<T>(string variableName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        var (assignment, configuration) = await GetAutomaticSystemVariableAssignmentAsync<T>(variableName, cancellationToken).ConfigureAwait(false);

        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        var value = ConvertSystemVariable<T>(registers, variableName);
        return new FanucSystemVariableReadResult<T>(value, registers, assignment.StartAddress, assignment.ToSetAsgCommand(), configuration.Status, configuration.CommandIssued);
    }

    /// <summary>
    /// Writes an allow-listed INTEGER, REAL, or BOOLEAN system variable and verifies the raw register read-back.
    /// FANUC does not document multi-register writes as atomic.
    /// </summary>
    public async Task<FanucSystemVariableWriteResult<T>> WriteSystemVariableAsync<T>(
        string variableName,
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        ValidateSystemVariableWritePermission(variableName);
        var registers = ConvertSystemVariableToRegisters(value, variableName);
        using var targetLock = await AcquireTargetWriteLockAsync($"system-variable\0{variableName}", cancellationToken).ConfigureAwait(false);
        var (assignment, configuration) = await GetAutomaticSystemVariableAssignmentAsync<T>(variableName, cancellationToken).ConfigureAwait(false);

        await EnsureHoldingRegistersWritableAsync(cancellationToken).ConfigureAwait(false);
        var writeStatus = await WriteHoldingRegistersAsync(assignment.StartAddress, registers, cancellationToken).ConfigureAwait(false);
        var readBack = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        if (!registers.SequenceEqual(readBack))
            throw new FanucOpcUaConnectionException($"System variable '{variableName}' write verification failed. Expected registers [{string.Join(", ", registers)}], received [{string.Join(", ", readBack)}].");

        var verifiedValue = ConvertSystemVariable<T>(readBack, variableName);
        return new FanucSystemVariableWriteResult<T>(
            verifiedValue,
            registers,
            readBack,
            assignment.StartAddress,
            assignment.ToSetAsgCommand(),
            configuration.Status,
            configuration.CommandIssued,
            writeStatus);
    }

    /// <summary>Reads R[n] through a connection-scoped REAL (MULTIPLY=0) dynamic assignment.</summary>
    public async Task<double> ReadRegisterAsync(int registerNumber, CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await GetAutomaticRegisterAssignmentAsync(registerNumber, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        return ConvertRegisterToDouble(registers, registerNumber);
    }

    /// <summary>
    /// Reads one FANUC TP task's program execution status through a connection-scoped PRG[n] mapping.
    /// The mapping is read-only per Additional Option Manual §55.3.7.
    /// </summary>
    public async Task<FanucProgramStatus> ReadProgramStatusAsync(int taskNumber = 1, CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await GetAutomaticProgramStatusAssignmentAsync(taskNumber, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        return ConvertProgramStatus(registers, taskNumber);
    }

    /// <summary>
    /// Writes R[n] through a REAL (MULTIPLY=0) dynamic assignment and verifies the raw-register read-back.
    /// FANUC does not document multi-register writes as atomic.
    /// </summary>
    public async Task WriteRegisterAsync(int registerNumber, double value, CancellationToken cancellationToken = default)
    {
        ValidateRegisterWritePermission(registerNumber);
        var registers = ConvertDoubleToRegisterWords(value, registerNumber);
        using var targetLock = await AcquireTargetWriteLockAsync($"register\0{registerNumber}", cancellationToken).ConfigureAwait(false);
        var (assignment, _) = await GetAutomaticRegisterAssignmentAsync(registerNumber, cancellationToken).ConfigureAwait(false);

        await EnsureHoldingRegistersWritableAsync(cancellationToken).ConfigureAwait(false);
        await WriteHoldingRegistersAsync(assignment.StartAddress, registers, cancellationToken).ConfigureAwait(false);
        var readBack = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        if (!registers.SequenceEqual(readBack))
            throw new FanucOpcUaConnectionException($"R[{registerNumber}] write verification failed. Expected registers [{string.Join(", ", registers)}], received [{string.Join(", ", readBack)}].");

        var verifiedValue = ConvertRegisterToDouble(readBack, registerNumber);
        if (verifiedValue != value)
            throw new FanucOpcUaConnectionException($"R[{registerNumber}] write verification failed. Expected {value:R}, received {verifiedValue:R}.");
    }

    /// <summary>
    /// Reads DI[n] through a connection-scoped single-register dynamic assignment.
    /// FANUC HMI Device Communication section 55.3.10 defines MULTIPLY=1 as 0=OFF and 1=ON.
    /// </summary>
    public Task<bool> ReadDigitalInputAsync(int number, CancellationToken cancellationToken = default) =>
        ReadDigitalSignalAsync("DI", number, cancellationToken);

    /// <summary>
    /// Reads DO[n] through a connection-scoped single-register dynamic assignment.
    /// This method does not write or otherwise change the output.
    /// </summary>
    public Task<bool> ReadDigitalOutputAsync(int number, CancellationToken cancellationToken = default) =>
        ReadDigitalSignalAsync("DO", number, cancellationToken);

    /// <summary>Reads RI[n] as a single 0/1 Holding Register value.</summary>
    public Task<bool> ReadRobotInputAsync(int number, CancellationToken cancellationToken = default) =>
        ReadDigitalSignalAsync("RI", number, cancellationToken);

    /// <summary>Reads RO[n] as a single 0/1 Holding Register value.</summary>
    public Task<bool> ReadRobotOutputAsync(int number, CancellationToken cancellationToken = default) =>
        ReadDigitalSignalAsync("RO", number, cancellationToken);

    /// <summary>
    /// Reads GI[n] as an unsigned 16-bit value. The raw HoldingRegisters Int16 bit pattern is preserved.
    /// </summary>
    public Task<ushort> ReadGroupInputAsync(int number, CancellationToken cancellationToken = default) =>
        ReadGroupSignalAsync("GI", number, cancellationToken);

    /// <summary>
    /// Reads GO[n] as an unsigned 16-bit value. This method does not write or otherwise change the output.
    /// </summary>
    public Task<ushort> ReadGroupOutputAsync(int number, CancellationToken cancellationToken = default) =>
        ReadGroupSignalAsync("GO", number, cancellationToken);

    /// <summary>
    /// Writes a permitted DO[n] through a connection-scoped MULTIPLY=1 mapping and verifies the read-back.
    /// The write/read-back sequence is serialized per client, but is not a controller-atomic operation.
    /// </summary>
    public async Task WriteDigitalOutputAsync(int number, bool value, CancellationToken cancellationToken = default)
    {
        ValidateDigitalOutputWritePermission(number);
        await _digitalOutputWriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (assignment, _) = await GetAutomaticDigitalSignalAssignmentAsync("DO", number, cancellationToken).ConfigureAwait(false);
            await EnsureHoldingRegistersWritableAsync(cancellationToken).ConfigureAwait(false);
            await WriteHoldingRegistersAsync(assignment.StartAddress, [(short)(value ? 1 : 0)], cancellationToken).ConfigureAwait(false);
            var readBack = await ReadDigitalSignalAsync("DO", number, cancellationToken).ConfigureAwait(false);
            if (readBack != value)
                throw new FanucOpcUaConnectionException($"DO[{number}] write verification failed. Expected {(value ? "ON (1)" : "OFF (0)")}, received {(readBack ? "ON (1)" : "OFF (0)")}.");
        }
        finally
        {
            _digitalOutputWriteGate.Release();
        }
    }

    /// <summary>Reads Cartesian X/Y/Z/W/P/R through PR[n]@1.12 with MULTIPLY=0.</summary>
    public async Task<FanucPosition> ReadPositionRegisterAsync(int number, CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await GetAutomaticPositionAssignmentAsync(number, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        return ConvertCartesianPosition(registers, number);
    }

    /// <summary>Reads raw FLIP, LEFT, UP, FRONT, TURN4-6, and VALIDC fields through PR[n]@19.8.</summary>
    public async Task<FanucPositionRegisterConfiguration> ReadPositionRegisterConfigurationAsync(int number, CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await GetAutomaticPositionConfigurationAssignmentAsync(number, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        if (registers.Length != 8)
            throw new FanucOpcUaConnectionException($"PR[{number}] configuration mapping returned {registers.Length} registers; exactly 8 are required.");
        return new FanucPositionRegisterConfiguration(
            registers[0], registers[1], registers[2], registers[3],
            registers[4], registers[5], registers[6], registers[7]);
    }

    /// <summary>
    /// Writes Cartesian PR[] members and optional documented CONFIG fields with verified read-back.
    /// Supplying all Cartesian fields is a full Cartesian update; otherwise supplied fields are merged with a prior read.
    /// </summary>
    public async Task WritePositionRegisterAsync(int number, FanucPosition position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ValidatePositionRegisterWritePermission(number);
        ValidateUnsupportedPositionFields(position);
        if (!position.HasAnyCartesian && position.Configuration is null)
            throw new ArgumentException("At least one Cartesian PR[] member or CONFIG must be specified.", nameof(position));

        using var targetLock = await AcquireTargetWriteLockAsync($"position\0{number}", cancellationToken).ConfigureAwait(false);
        var (assignment, _) = await GetAutomaticPositionAssignmentAsync(number, cancellationToken).ConfigureAwait(false);
        if (position.HasAnyCartesian)
        {
            var target = position.HasCompleteCartesian
                ? position
                : MergeCartesianPosition(await ReadPositionRegisterAsync(number, cancellationToken).ConfigureAwait(false), position);
            var registers = ConvertCartesianPositionToRegisters(target, number);

            await EnsureHoldingRegistersWritableAsync(cancellationToken).ConfigureAwait(false);
            await WriteHoldingRegistersAsync(assignment.StartAddress, registers, cancellationToken).ConfigureAwait(false);
            var readBack = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
            if (!registers.SequenceEqual(readBack))
                throw new FanucOpcUaConnectionException($"PR[{number}] Cartesian write verification failed. Expected registers [{string.Join(", ", registers)}], received [{string.Join(", ", readBack)}].");
        }

        // Full Cartesian writes use the documented default N D B, 0 0 0 unless CONFIG is explicitly supplied.
        if (position.Configuration is not null || position.HasCompleteCartesian)
            await WritePositionConfigurationAsync(number, position.Configuration, position.HasCompleteCartesian, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DynamicAssignmentConfigurationResult> ConfigureDynamicAssignmentCoreAsync(FanucDynamicAssignment assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await _mappingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ConfigureDynamicAssignmentLockedAsync(assignment, cancellationToken).ConfigureAwait(false);
        }
        finally { _mappingGate.Release(); }
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticSystemVariableAssignmentAsync<T>(string variableName, CancellationToken cancellationToken)
    {
        var template = CreateSystemVariableAssignment<T>(variableName, 1);
        return await GetAutomaticAssignmentAsync(CreateAutomaticAssignmentKey<T>(variableName), template, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticRegisterAssignmentAsync(int registerNumber, CancellationToken cancellationToken)
    {
        ValidateRegisterNumber(registerNumber);
        var template = new FanucDynamicAssignment(1, 2, $"R[{registerNumber}]", 0m);
        return await GetAutomaticAssignmentAsync($"register\0{registerNumber}\0real32", template, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticProgramStatusAssignmentAsync(int taskNumber, CancellationToken cancellationToken)
    {
        ValidateTaskNumber(taskNumber);
        // §55.3.7 uses 18 registers per task. §55.3.1: MULTIPLY=1 stores the first character in each word's low byte.
        var template = new FanucDynamicAssignment(1, 18, $"PRG[{taskNumber}]", 1m);
        return await GetAutomaticAssignmentAsync($"program-status\0{taskNumber}\0multiply-one", template, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ReadDigitalSignalAsync(string signalKind, int number, CancellationToken cancellationToken)
    {
        var (assignment, _) = await GetAutomaticDigitalSignalAssignmentAsync(signalKind, number, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        var value = registers[0];
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new FanucOpcUaConnectionException($"{signalKind}[{number}] returned {value}; a MULTIPLY=1 digital I/O mapping must return 0 (OFF) or 1 (ON).")
        };
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticDigitalSignalAssignmentAsync(string signalKind, int number, CancellationToken cancellationToken)
    {
        ValidateDigitalSignalNumber(number, signalKind);
        var template = new FanucDynamicAssignment(1, 1, $"{signalKind}[{number}]");
        return await GetAutomaticAssignmentAsync($"digital\0{signalKind}\0{number}\0single", template, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ushort> ReadGroupSignalAsync(string signalKind, int number, CancellationToken cancellationToken)
    {
        var (assignment, _) = await GetAutomaticGroupSignalAssignmentAsync(signalKind, number, cancellationToken).ConfigureAwait(false);
        var registers = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        return unchecked((ushort)registers[0]);
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticGroupSignalAssignmentAsync(string signalKind, int number, CancellationToken cancellationToken)
    {
        ValidateGroupSignalNumber(number, signalKind);
        // FANUC §55.3.10: GI/GO always use one Holding Register, including when MULTIPLY is 0.
        var template = new FanucDynamicAssignment(1, 1, $"{signalKind}[{number}]");
        return await GetAutomaticAssignmentAsync($"group\0{signalKind}\0{number}\0uint16", template, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticPositionAssignmentAsync(int number, CancellationToken cancellationToken)
    {
        ValidatePositionRegisterNumber(number);
        var template = new FanucDynamicAssignment(1, 12, $"PR[{number}]@1.12", 0m);
        return await GetAutomaticAssignmentAsync($"position\0{number}\0cartesian-real32", template, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticPositionConfigurationAssignmentAsync(int number, CancellationToken cancellationToken)
    {
        ValidatePositionRegisterNumber(number);
        var template = new FanucDynamicAssignment(1, 8, $"PR[{number}]@19.8");
        return await GetAutomaticAssignmentAsync($"position\0{number}\0configuration-raw", template, cancellationToken).ConfigureAwait(false);
    }

    private async Task WritePositionConfigurationAsync(int number, FanucPositionConfiguration? requested, bool useDefaultWhenMissing, CancellationToken cancellationToken)
    {
        var current = await ReadPositionRegisterConfigurationAsync(number, cancellationToken).ConfigureAwait(false);
        var configuration = requested ?? (useDefaultWhenMissing
            ? new FanucPositionConfiguration("N", "D", "B", Turn4: 0, Turn5: 0, Turn6: 0)
            : throw new InvalidOperationException("Position CONFIG is required for this operation."));
        var registers = new short[]
        {
            ParseConfigurationBit(configuration.Flip, "N", "F", current.Flip, "FLIP"),
            configuration.Left ?? current.Left,
            ParseConfigurationBit(configuration.UpDown, "D", "U", current.Up, "UP"),
            ParseConfigurationBit(configuration.TopBottom, "B", "T", current.Front, "FRONT"),
            configuration.Turn4 ?? current.Turn4,
            configuration.Turn5 ?? current.Turn5,
            configuration.Turn6 ?? current.Turn6,
            1 // Writing VALIDC changes the representation to Cartesian per FANUC §55.3.3.
        };

        ValidateTurn(registers[4], "TURN4");
        ValidateTurn(registers[5], "TURN5");
        ValidateTurn(registers[6], "TURN6");
        var (assignment, _) = await GetAutomaticPositionConfigurationAssignmentAsync(number, cancellationToken).ConfigureAwait(false);
        await EnsureHoldingRegistersWritableAsync(cancellationToken).ConfigureAwait(false);
        await WriteHoldingRegistersAsync(assignment.StartAddress, registers, cancellationToken).ConfigureAwait(false);
        var readBack = await ReadHoldingRegistersAsync(assignment.StartAddress, assignment.Size, cancellationToken).ConfigureAwait(false);
        if (!registers.SequenceEqual(readBack))
            throw new FanucOpcUaConnectionException($"PR[{number}] CONFIG write verification failed. Expected registers [{string.Join(", ", registers)}], received [{string.Join(", ", readBack)}].");
    }

    private async Task<(FanucDynamicAssignment Assignment, DynamicAssignmentConfigurationResult Configuration)> GetAutomaticAssignmentAsync(string key, FanucDynamicAssignment template, CancellationToken cancellationToken)
    {
        await _mappingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_automaticAssignments.TryGetValue(key, out var assignment))
                return (assignment, DynamicAssignmentConfigurationResult.Cached);

            assignment = template with { StartAddress = AllocateDynamicAddressLocked(template.Size) };
            var configuration = await ConfigureDynamicAssignmentLockedAsync(assignment, cancellationToken).ConfigureAwait(false);
            _automaticAssignments.Add(key, assignment);
            return (assignment, configuration);
        }
        finally { _mappingGate.Release(); }
    }

    /// <summary>Maps and reads the current FANUC general override as a signed 32-bit INTEGER.</summary>
    public async Task<int> ReadGeneralOverrideAsync(CancellationToken cancellationToken = default)
    {
        var result = await ReadSystemVariableAsync<int>("$MCR.$GENOVERRIDE", cancellationToken).ConfigureAwait(false);
        return result.Value;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _gate.Dispose();
            _mappingGate.Dispose();
            _digitalOutputWriteGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Dispose();
            _mappingGate.Dispose();
            _digitalOutputWriteGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private ApplicationConfiguration CreateConfiguration() => new()
    {
        ApplicationName = _options.ApplicationName,
        ApplicationUri = $"urn:{Utils.GetHostName()}:{_options.ApplicationName}",
        ApplicationType = ApplicationType.Client,
        // Unknown server certificates are never silently trusted.
        SecurityConfiguration = new SecurityConfiguration
        {
            ApplicationCertificate = new CertificateIdentifier
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = Path.Combine(_options.PkiRootPath, "own"),
                SubjectName = $"CN={_options.ApplicationName}"
            },
            TrustedPeerCertificates = new CertificateTrustList
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = Path.Combine(_options.PkiRootPath, "trusted")
            },
            TrustedIssuerCertificates = new CertificateTrustList
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = Path.Combine(_options.PkiRootPath, "issuers")
            },
            RejectedCertificateStore = new CertificateStoreIdentifier
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = Path.Combine(_options.PkiRootPath, "rejected")
            },
            AutoAcceptUntrustedCertificates = false
        },
        TransportConfigurations = new TransportConfigurationCollection(),
        TransportQuotas = new TransportQuotas { OperationTimeout = (int)_options.OperationTimeout.TotalMilliseconds },
        ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = (int)_options.SessionTimeout.TotalMilliseconds }
    };

    private async Task EnsureApplicationCertificateAsync(ApplicationConfiguration configuration, CancellationToken cancellationToken)
    {
        var application = new ApplicationInstance(configuration, null)
        {
            ApplicationName = _options.ApplicationName,
            ApplicationType = ApplicationType.Client
        };

        if (!await application.CheckApplicationInstanceCertificatesAsync(false, null, cancellationToken).ConfigureAwait(false))
            throw new FanucOpcUaConnectionException("The OPC UA application certificate could not be created or validated.");
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // A delayed callback from a disposed/replaced session must not fault the new session.
        if (ReferenceEquals(session, Volatile.Read(ref _session)) &&
            _state == FanucOpcUaConnectionState.Connected &&
            ServiceResult.IsBad(e.Status))
            _state = FanucOpcUaConnectionState.Faulted;
    }

    private async Task CloseSessionBestEffortAsync()
    {
        try
        {
            await CloseSessionAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // CloseSessionAsync has already disposed the session and cleared mappings in its finally block.
            // Preserve the connection/reconnect failure that triggered cleanup rather than masking it.
        }
    }

    private async Task CloseSessionAsync(CancellationToken cancellationToken)
    {
        await _mappingGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var session = Interlocked.Exchange(ref _session, null);
            if (session is null) return;
            session.KeepAlive -= OnKeepAlive;
            try { await session.CloseAsync(cancellationToken).ConfigureAwait(false); }
            finally { session.Dispose(); }
        }
        finally
        {
            _dynamicAssignments.Clear();
            _automaticAssignments.Clear();
            _dynamicAssignmentsInitialized = false;
            _mappingGate.Release();
        }
    }

    private async Task<StatusCode> WriteCommandAsync(string command, CancellationToken cancellationToken)
    {
        var session = GetConnectedSession();
        var response = await session.WriteAsync(
            null,
            new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = NodeId.Parse(_options.CommandNodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(command)
                }
            },
            cancellationToken).ConfigureAwait(false);

        return EnsureGoodResult(response.Results, "The FANUC Command write was rejected.");
    }

    private static FanucRobotStatusNode? GetStatusNode(IReadOnlyList<FanucRobotStatusNode> nodes, string name) =>
        nodes.FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.Ordinal));

    private static bool HasGoodValue(FanucRobotStatusNode? node) =>
        node is { Found: true, ValueStatusCode: { } status } && StatusCode.IsGood(status);

    private static bool? GetOperationStateBit(uint? operationState, uint mask) =>
        operationState is uint value ? (value & mask) != 0 : null;

    private static bool? GetServoState(FanucRobotStatusNode? node)
    {
        if (!TryGetUInt32(node, out var value) || value > 1) return null;
        return value == 1;
    }

    private static FanucRobotMode? GetRobotMode(FanucRobotStatusNode? node)
    {
        if (!HasGoodValue(node)) return null;
        if (node!.Value is not string mode) return FanucRobotMode.Unknown;

        var normalizedMode = mode.Trim();
        if (IsModeText(normalizedMode, "AUTO")) return FanucRobotMode.Auto;
        if (IsModeText(normalizedMode, "T1")) return FanucRobotMode.T1;
        if (IsModeText(normalizedMode, "T2")) return FanucRobotMode.T2;
        return FanucRobotMode.Unknown;
    }

    private static bool IsModeText(string value, string code) =>
        string.Equals(value, code, StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith($"{code}(", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith($"{code} (", StringComparison.OrdinalIgnoreCase);

    private static int? GetProgramSpeed(FanucRobotStatusNode? node) =>
        TryGetUInt32(node, out var value) && value <= 100 ? (int)value : null;

    private static string[]? GetAlarm(FanucRobotStatusNode? node)
    {
        if (!HasGoodValue(node)) return null;

        return node!.Value switch
        {
            null => [],
            string text when string.IsNullOrWhiteSpace(text) => [],
            string text => [text],
            string[] alarms => alarms.Where(alarm => !string.IsNullOrWhiteSpace(alarm)).ToArray(),
            IEnumerable<string> alarms => alarms.Where(alarm => !string.IsNullOrWhiteSpace(alarm)).ToArray(),
            _ => null
        };
    }

    private static bool TryGetUInt32(FanucRobotStatusNode? node, out uint result)
    {
        result = default;
        return HasGoodValue(node) && TryGetUInt32(node!.Value, out result);
    }

    private static bool TryGetUInt32(object? value, out uint result)
    {
        switch (value)
        {
            case byte number: result = number; return true;
            case ushort number: result = number; return true;
            case uint number: result = number; return true;
            case sbyte number when number >= 0: result = (uint)number; return true;
            case short number when number >= 0: result = (uint)number; return true;
            case int number when number >= 0: result = (uint)number; return true;
            case long number when number is >= 0 and <= uint.MaxValue: result = (uint)number; return true;
            case ulong number when number <= uint.MaxValue: result = (uint)number; return true;
            default: result = default; return false;
        }
    }

    private static async Task<IReadOnlyList<DiscoveredReference>> BrowseDescendantsAsync(ISession session, NodeId rootNodeId, string rootPath, CancellationToken cancellationToken)
    {
        var discovered = new List<DiscoveredReference>();
        var visited = new HashSet<NodeId> { rootNodeId };
        var pending = new Queue<DiscoveredReference>();
        pending.Enqueue(new DiscoveredReference(rootNodeId, rootPath, rootPath));

        while (pending.TryDequeue(out var parent))
        {
            foreach (var child in await BrowseChildrenAsync(session, parent.NodeId, cancellationToken).ConfigureAwait(false))
            {
                if (!visited.Add(child.NodeId)) continue;
                var path = $"{parent.Path}/{child.BrowseName}";
                var discoveredChild = child with { Path = path };
                discovered.Add(discoveredChild);
                pending.Enqueue(discoveredChild);
            }
        }

        return discovered;
    }

    private static async Task<IReadOnlyList<DiscoveredReference>> BrowseChildrenAsync(ISession session, NodeId nodeId, CancellationToken cancellationToken)
    {
        var browser = new Browser(session, new BrowserOptions())
        {
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true,
            ContinueUntilDone = true
        };
        var references = await browser.BrowseAsync(nodeId, cancellationToken).ConfigureAwait(false);
        return references
            .Select(reference => (Node: ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris), Reference: reference))
            .Where(item => item.Node is not null)
            .Select(item => new DiscoveredReference(item.Node!, item.Reference.BrowseName.Name, item.Reference.DisplayName.Text ?? item.Reference.BrowseName.Name))
            .ToArray();
    }

    private static DiscoveredReference? FindReference(IReadOnlyList<DiscoveredReference> references, string browseName) =>
        references.FirstOrDefault(reference => string.Equals(reference.BrowseName, browseName, StringComparison.Ordinal));

    private sealed record DiscoveredReference(NodeId NodeId, string BrowseName, string Path);

    private async Task EnsureHoldingRegistersWritableAsync(CancellationToken cancellationToken)
    {
        var session = GetConnectedSession();
        var nodeId = NodeId.Parse(_options.HoldingRegistersNodeId);
        var response = await session.ReadAsync(
            null,
            0,
            TimestampsToReturn.Neither,
            new ReadValueIdCollection
            {
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.AccessLevel },
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.UserAccessLevel }
            },
            cancellationToken).ConfigureAwait(false);

        if (response.Results.Count != 2 || response.Results.Any(result => !StatusCode.IsGood(result.StatusCode)))
            throw new FanucOpcUaConnectionException($"Could not read HoldingRegisters AccessLevel/UserAccessLevel with Good OPC UA status before writing. Statuses: [{string.Join(", ", response.Results.Select(result => result.StatusCode))}].");

        var accessLevel = ConvertAccessLevel(response.Results[0].Value, "AccessLevel");
        var userAccessLevel = ConvertAccessLevel(response.Results[1].Value, "UserAccessLevel");
        if ((accessLevel & AccessLevels.CurrentWrite) == 0 || (userAccessLevel & AccessLevels.CurrentWrite) == 0)
            throw new FanucOpcUaConnectionException($"HoldingRegisters is not writable for this OPC UA session. AccessLevel={accessLevel}, UserAccessLevel={userAccessLevel}.");
    }

    private async Task<StatusCode> WriteHoldingRegistersAsync(int startAddress, IReadOnlyList<short> registers, CancellationToken cancellationToken)
    {
        if (startAddress < 1 || registers.Count < 1 || startAddress + registers.Count - 1 > 16384)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "Holding Register addresses must be in the range 1 through 16384.");

        var session = GetConnectedSession();
        var firstIndex = startAddress - 1;
        var indexRange = CreateHoldingRegisterIndexRange(firstIndex, registers.Count);
        var response = await session.WriteAsync(
            null,
            new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = NodeId.Parse(_options.HoldingRegistersNodeId),
                    AttributeId = Attributes.Value,
                    IndexRange = indexRange,
                    Value = new DataValue(registers.ToArray())
                }
            },
            cancellationToken).ConfigureAwait(false);

        return EnsureGoodResult(response.Results, $"Writing Holding Registers {startAddress}-{startAddress + registers.Count - 1} was rejected.");
    }

    private async Task<short[]> ReadHoldingRegistersAsync(int startAddress, int count, CancellationToken cancellationToken)
    {
        if (startAddress < 1 || count < 1 || startAddress + count - 1 > 16384)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "Holding Register addresses must be in the range 1 through 16384.");

        var session = GetConnectedSession();
        var firstIndex = startAddress - 1; // FANUC address N is OPC UA array index N - 1.
        var indexRange = CreateHoldingRegisterIndexRange(firstIndex, count);
        var response = await session.ReadAsync(
            null,
            0,
            TimestampsToReturn.Neither,
            new ReadValueIdCollection
            {
                new ReadValueId
                {
                    NodeId = NodeId.Parse(_options.HoldingRegistersNodeId),
                    AttributeId = Attributes.Value,
                    IndexRange = indexRange
                }
            },
            cancellationToken).ConfigureAwait(false);

        if (response.Results.Count != 1 || !StatusCode.IsGood(response.Results[0].StatusCode))
            throw new FanucOpcUaConnectionException($"Reading Holding Registers {startAddress}-{startAddress + count - 1} requires Good OPC UA status; received {response.Results.FirstOrDefault()?.StatusCode}.");

        var value = response.Results[0].Value;
        var registers = value switch
        {
            short[] array => array,
            short single when count == 1 => [single],
            _ => throw new FanucOpcUaConnectionException("The HoldingRegisters node did not return an Int16 array.")
        };

        if (registers.Length != count)
            throw new FanucOpcUaConnectionException($"HoldingRegisters returned {registers.Length} values; {count} were requested.");
        return registers;
    }

    internal static string CreateHoldingRegisterIndexRange(int firstIndex, int count) =>
        count == 1
            ? firstIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"{firstIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{(firstIndex + count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Builds a signed Int32 from FANUC INTEGER words: low 16 bits followed by high 16 bits.</summary>
    public static int CombineInt16ToInt32(short lowWord, short highWord) =>
        unchecked((int)((uint)(ushort)lowWord | ((uint)(ushort)highWord << 16)));

    private static byte ConvertAccessLevel(object? value, string attributeName) => value switch
    {
        byte byteValue => byteValue,
        AccessLevelType accessLevel => (byte)accessLevel,
        _ => throw new FanucOpcUaConnectionException($"HoldingRegisters {attributeName} did not return an OPC UA access-level byte.")
    };

    private static FanucDynamicAssignment CreateSystemVariableAssignment<T>(string variableName, int startAddress)
    {
        if (typeof(T) == typeof(int) || typeof(T) == typeof(bool))
            return new FanucDynamicAssignment(startAddress, 2, variableName);
        if (typeof(T) == typeof(float))
            return new FanucDynamicAssignment(startAddress, 2, variableName, 0m);

        throw new NotSupportedException($"System-variable type '{typeof(T).FullName}' is not supported. Supported types are Int32, Single, and Boolean.");
    }

    private static T ConvertSystemVariable<T>(IReadOnlyList<short> registers, string variableName)
    {
        if (registers.Count != 2)
            throw new FanucOpcUaConnectionException($"System variable '{variableName}' returned {registers.Count} registers; exactly two are required.");

        var intValue = CombineInt16ToInt32(registers[0], registers[1]);
        if (typeof(T) == typeof(int)) return (T)(object)intValue;
        if (typeof(T) == typeof(float))
        {
            var value = BitConverter.Int32BitsToSingle(intValue);
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new FanucOpcUaConnectionException($"System variable '{variableName}' returned a non-finite IEEE 754 Float32 value.");
            return (T)(object)value;
        }
        if (typeof(T) == typeof(bool))
        {
            if (intValue is not 0 and not 1)
                throw new FanucOpcUaConnectionException($"System variable '{variableName}' returned BOOLEAN value {intValue}; only 0 and 1 are valid.");
            return (T)(object)(intValue == 1);
        }

        throw new NotSupportedException($"System-variable type '{typeof(T).FullName}' is not supported.");
    }

    private static FanucProgramStatus ConvertProgramStatus(IReadOnlyList<short> registers, int taskNumber)
    {
        if (registers.Count != 18)
            throw new FanucOpcUaConnectionException($"PRG[{taskNumber}] returned {registers.Count} registers; exactly 18 are required by FANUC §55.3.7.");

        var executionStatus = registers[9] switch
        {
            0 => FanucProgramExecutionState.Aborted,
            1 => FanucProgramExecutionState.Paused,
            2 => FanucProgramExecutionState.Running,
            var value => throw new FanucOpcUaConnectionException($"PRG[{taskNumber}] returned unsupported execution status {value}; FANUC §55.3.7 defines only 0 (Aborted), 1 (Paused), and 2 (Running).")
        };

        return new FanucProgramStatus(
            taskNumber,
            DecodeMultiplyOneString(registers, 0, 8, $"PRG[{taskNumber}] program name"),
            registers[8],
            executionStatus,
            DecodeMultiplyOneString(registers, 10, 8, $"PRG[{taskNumber}] parent program name"));
    }

    private static string DecodeMultiplyOneString(IReadOnlyList<short> registers, int offset, int registerCount, string fieldName)
    {
        if (offset < 0 || registerCount < 1 || offset + registerCount > registers.Count)
            throw new ArgumentOutOfRangeException(nameof(offset), $"{fieldName} register range is invalid.");

        var bytes = new byte[registerCount * 2];
        for (var index = 0; index < registerCount; index++)
        {
            var word = unchecked((ushort)registers[offset + index]);
            // FANUC §55.3.1, $MULTIPLY=1: low byte is the first character and high byte the second.
            bytes[index * 2] = (byte)word;
            bytes[index * 2 + 1] = (byte)(word >> 8);
        }

        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0) length = bytes.Length;
        return System.Text.Encoding.Latin1.GetString(bytes, 0, length);
    }

    private static short[] ConvertSystemVariableToRegisters<T>(T value, string variableName)
    {
        int rawValue;
        if (typeof(T) == typeof(int)) rawValue = (int)(object)value!;
        else if (typeof(T) == typeof(float))
        {
            var floatValue = (float)(object)value!;
            if (float.IsNaN(floatValue) || float.IsInfinity(floatValue))
                throw new ArgumentOutOfRangeException(nameof(value), $"System variable '{variableName}' cannot be written with a non-finite Float32 value.");
            rawValue = BitConverter.SingleToInt32Bits(floatValue);
        }
        else if (typeof(T) == typeof(bool)) rawValue = (bool)(object)value! ? 1 : 0;
        else throw new NotSupportedException($"System-variable type '{typeof(T).FullName}' is not supported. Supported types are Int32, Single, and Boolean.");

        return [unchecked((short)rawValue), unchecked((short)(rawValue >> 16))];
    }

    internal static double ConvertRegisterToDouble(IReadOnlyList<short> registers, int registerNumber)
    {
        if (registers.Count != 2)
            throw new FanucOpcUaConnectionException($"R[{registerNumber}] returned {registers.Count} registers; exactly two are required.");

        var value = BitConverter.Int32BitsToSingle(CombineInt16ToInt32(registers[0], registers[1]));
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new FanucOpcUaConnectionException($"R[{registerNumber}] returned a non-finite IEEE 754 Float32 value.");
        return value;
    }

    internal static short[] ConvertDoubleToRegisterWords(double value, int registerNumber)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), $"R[{registerNumber}] cannot be written with a non-finite value.");

        var floatValue = (float)value;
        if (float.IsInfinity(floatValue) || (double)floatValue != value)
            throw new ArgumentOutOfRangeException(nameof(value), $"R[{registerNumber}] value {value:R} is not exactly representable as FANUC's Float32 Holding Register format. Choose a Float32-exact value.");

        var rawValue = BitConverter.SingleToInt32Bits(floatValue);
        return [unchecked((short)rawValue), unchecked((short)(rawValue >> 16))];
    }

    private static FanucPosition ConvertCartesianPosition(IReadOnlyList<short> registers, int number)
    {
        if (registers.Count != 12)
            throw new FanucOpcUaConnectionException($"PR[{number}] Cartesian mapping returned {registers.Count} registers; exactly 12 are required.");

        return new FanucPosition(
            ConvertFloatWordsToDouble(registers, 0, number, "X"),
            ConvertFloatWordsToDouble(registers, 2, number, "Y"),
            ConvertFloatWordsToDouble(registers, 4, number, "Z"),
            ConvertFloatWordsToDouble(registers, 6, number, "W"),
            ConvertFloatWordsToDouble(registers, 8, number, "P"),
            ConvertFloatWordsToDouble(registers, 10, number, "R"));
    }

    private static short[] ConvertCartesianPositionToRegisters(FanucPosition position, int number)
    {
        if (!position.HasCompleteCartesian)
            throw new ArgumentException("A complete Cartesian position is required after partial-update merging.", nameof(position));

        var values = new[] { position.X!.Value, position.Y!.Value, position.Z!.Value, position.W!.Value, position.P!.Value, position.R!.Value };
        var registers = new short[12];
        for (var index = 0; index < values.Length; index++)
        {
            var words = ConvertDoubleToFloatWords(values[index], $"PR[{number}].{CartesianMemberNames[index]}");
            registers[index * 2] = words[0];
            registers[index * 2 + 1] = words[1];
        }
        return registers;
    }

    private static readonly string[] CartesianMemberNames = ["X", "Y", "Z", "W", "P", "R"];

    private static double ConvertFloatWordsToDouble(IReadOnlyList<short> registers, int offset, int number, string member)
    {
        var value = BitConverter.Int32BitsToSingle(CombineInt16ToInt32(registers[offset], registers[offset + 1]));
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new FanucOpcUaConnectionException($"PR[{number}].{member} returned a non-finite IEEE 754 Float32 value.");
        return value;
    }

    private static short[] ConvertDoubleToFloatWords(double value, string fieldName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), $"{fieldName} cannot be written with a non-finite value.");
        var floatValue = (float)value;
        if (float.IsInfinity(floatValue) || (double)floatValue != value)
            throw new ArgumentOutOfRangeException(nameof(value), $"{fieldName} value {value:R} is not exactly representable as FANUC's Float32 Holding Register format.");
        var rawValue = BitConverter.SingleToInt32Bits(floatValue);
        return [unchecked((short)rawValue), unchecked((short)(rawValue >> 16))];
    }

    private static FanucPosition MergeCartesianPosition(FanucPosition current, FanucPosition update) => new(
        update.X ?? current.X, update.Y ?? current.Y, update.Z ?? current.Z,
        update.W ?? current.W, update.P ?? current.P, update.R ?? current.R);

    private static void ValidateUnsupportedPositionFields(FanucPosition position)
    {
        if (position.TurnJ1.HasValue || position.TurnJ2.HasValue || position.TurnJ3.HasValue)
            throw new NotSupportedException("TurnJ1, TurnJ2, and TurnJ3 are unsupported. Use Configuration.Turn4, Turn5, and Turn6, which map directly to FANUC §55.3.3 fields.");
        if (position.UserFrame.HasValue || position.UserTool.HasValue)
            throw new NotSupportedException("PR UF and UT writes are unsupported because FANUC §55.3.3 states these values cannot be changed.");
    }

    private static short ParseConfigurationBit(string? value, string zeroName, string oneName, short currentValue, string fieldName)
    {
        if (value is null) return currentValue;
        if (string.Equals(value, zeroName, StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(value, oneName, StringComparison.OrdinalIgnoreCase)) return 1;
        throw new ArgumentException($"{fieldName} must be '{zeroName}' or '{oneName}'.", nameof(value));
    }

    private static void ValidateTurn(short value, string fieldName)
    {
        if (value is < -128 or > 127)
            throw new ArgumentOutOfRangeException(fieldName, $"{fieldName} must be in the FANUC range -128 through 127.");
    }

    private async Task<DynamicAssignmentConfigurationResult> ConfigureDynamicAssignmentLockedAsync(FanucDynamicAssignment assignment, CancellationToken cancellationToken)
    {
        await EnsureDynamicAssignmentsInitializedLockedAsync(cancellationToken).ConfigureAwait(false);
        if (_options.ReservedHoldingRegisterRanges.Any(range => range.Overlaps(assignment)))
            throw new InvalidOperationException($"Dynamic assignment {assignment.StartAddress}-{assignment.EndAddress} overlaps a configured reserved Holding Register range.");
        if (_dynamicAssignments.Contains(assignment)) return DynamicAssignmentConfigurationResult.Cached;
        if (_dynamicAssignments.Any(existing => RangesOverlap(existing, assignment)))
            throw new InvalidOperationException($"Holding Register addresses {assignment.StartAddress}-{assignment.EndAddress} overlap an existing dynamic assignment in this session.");

        var status = await WriteCommandAsync(assignment.ToSetAsgCommand(), cancellationToken).ConfigureAwait(false);
        _dynamicAssignments.Add(assignment);
        return new DynamicAssignmentConfigurationResult(status, true);
    }

    private async Task EnsureDynamicAssignmentsInitializedLockedAsync(CancellationToken cancellationToken)
    {
        if (_dynamicAssignmentsInitialized) return;
        await WriteCommandAsync("CLRASG", cancellationToken).ConfigureAwait(false);
        _dynamicAssignments.Clear();
        _automaticAssignments.Clear();
        _dynamicAssignmentsInitialized = true;
    }

    private int AllocateDynamicAddressLocked(int size) =>
        FindAvailableDynamicAddress(
            _options.DynamicAssignmentStartAddress,
            _options.DynamicAssignmentEndAddress,
            size,
            _dynamicAssignments,
            _options.ReservedHoldingRegisterRanges,
            _options.DynamicAssignmentRangeVerified);

    internal static int FindAvailableDynamicAddress(
        int startAddress,
        int endAddress,
        int size,
        IReadOnlyCollection<FanucDynamicAssignment> assignments,
        IReadOnlyList<FanucHoldingRegisterRange> reservedRanges,
        bool rangeVerified)
    {
        if (!rangeVerified)
            throw new InvalidOperationException("Automatic dynamic assignment allocation requires DynamicAssignmentRangeVerified = true after reviewing this controller's persistent $SNPX_ASG assignments and other client reservations.");
        if (startAddress is < 1 or > 16384 || endAddress < startAddress || endAddress > 16384 || size < 1)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "Dynamic assignment allocation parameters are outside the FANUC Holding Register range.");
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(reservedRanges);

        for (var candidate = startAddress; candidate <= endAddress - size + 1; candidate++)
        {
            var proposed = new FanucDynamicAssignment(candidate, size, "__reservation__");
            if (reservedRanges.All(range => !range.Overlaps(proposed)) &&
                assignments.All(existing => !RangesOverlap(existing, proposed))) return candidate;
        }

        throw new FanucOpcUaConnectionException($"No contiguous range of {size} Holding Registers is available in configured dynamic range {startAddress}-{endAddress}.");
    }

    private static string CreateAutomaticAssignmentKey<T>(string variableName) =>
        $"{typeof(T).AssemblyQualifiedName}\0{variableName}";

    internal async Task<IDisposable> AcquireTargetWriteLockAsync(string target, CancellationToken cancellationToken)
    {
        TargetWriteLockEntry entry;
        lock (_targetWriteLocksGate)
        {
            if (!_targetWriteLocks.TryGetValue(target, out var existing) || existing is null)
            {
                entry = new TargetWriteLockEntry();
                _targetWriteLocks.Add(target, entry);
            }
            else entry = existing;
            entry.ReferenceCount++;
        }

        try
        {
            await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new TargetWriteLockReleaser(this, target, entry);
        }
        catch
        {
            ReleaseTargetWriteLock(target, entry, acquired: false);
            throw;
        }
    }

    private void ReleaseTargetWriteLock(string target, TargetWriteLockEntry entry, bool acquired)
    {
        if (acquired) entry.Gate.Release();

        var dispose = false;
        lock (_targetWriteLocksGate)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0 && _targetWriteLocks.TryGetValue(target, out var current) && ReferenceEquals(current, entry))
            {
                _targetWriteLocks.Remove(target);
                dispose = true;
            }
        }

        if (dispose) entry.Gate.Dispose();
    }

    internal int ActiveTargetWriteLockCount
    {
        get
        {
            lock (_targetWriteLocksGate) return _targetWriteLocks.Count;
        }
    }

    private void ValidateSystemVariableWritePermission(string variableName)
    {
        if (!_options.EnableSystemVariableWrites)
            throw new InvalidOperationException("System-variable writes are disabled. Set EnableSystemVariableWrites to true and configure WritableSystemVariables explicitly.");
        if (variableName.StartsWith("$RBTIF.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"System variable '{variableName}' is a read-only $RBTIF special variable and cannot be written.");
        if (!_options.WritableSystemVariables.Contains(variableName))
            throw new InvalidOperationException($"System variable '{variableName}' is not in WritableSystemVariables.");
    }

    private void ValidateRegisterWritePermission(int registerNumber)
    {
        ValidateRegisterNumber(registerNumber);
        if (!_options.EnableRegisterWrites)
            throw new InvalidOperationException("R[] writes are disabled. Set EnableRegisterWrites to true and configure WritableRegisters explicitly.");
        if (!_options.WritableRegisters.Contains(registerNumber))
            throw new InvalidOperationException($"R[{registerNumber}] is not in WritableRegisters.");
    }

    private void ValidatePositionRegisterWritePermission(int number)
    {
        ValidatePositionRegisterNumber(number);
        if (!_options.EnablePositionRegisterWrites)
            throw new InvalidOperationException("PR[] writes are disabled. Set EnablePositionRegisterWrites to true and configure WritablePositionRegisters explicitly.");
        if (!_options.WritablePositionRegisters.Contains(number))
            throw new InvalidOperationException($"PR[{number}] is not in WritablePositionRegisters.");
    }

    private void ValidateDigitalOutputWritePermission(int number)
    {
        ValidateDigitalSignalNumber(number, "DO");
        if (!_options.EnableDigitalOutputWrites)
            throw new InvalidOperationException("DO[] writes are disabled. Set EnableDigitalOutputWrites to true and configure WritableDigitalOutputs explicitly.");
        if (!_options.WritableDigitalOutputs.Contains(number))
            throw new InvalidOperationException($"DO[{number}] is not in WritableDigitalOutputs.");
    }

    private static void ValidateRegisterNumber(int registerNumber)
    {
        if (registerNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(registerNumber), "FANUC R[] numbers must be greater than zero.");
    }

    private static void ValidateTaskNumber(int taskNumber)
    {
        if (taskNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(taskNumber), "FANUC PRG[] task numbers must be greater than zero.");
    }

    private static void ValidatePositionRegisterNumber(int number)
    {
        if (number < 1)
            throw new ArgumentOutOfRangeException(nameof(number), "FANUC PR[] numbers must be greater than zero.");
    }

    private static void ValidateDigitalSignalNumber(int number, string signalKind)
    {
        if (number is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(number), $"FANUC {signalKind}[] numbers must be in the range 1 through 10000.");
    }

    private static void ValidateGroupSignalNumber(int number, string signalKind)
    {
        if (number is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(number), $"FANUC {signalKind}[] numbers must be in the range 1 through 1000.");
    }

    private sealed class TargetWriteLockEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class TargetWriteLockReleaser(FanucOpcUaClient owner, string target, TargetWriteLockEntry entry) : IDisposable
    {
        private FanucOpcUaClient? _owner = owner;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseTargetWriteLock(target, entry, acquired: true);
        }
    }

    private ISession GetConnectedSession() =>
        IsConnected && _session is { } session
            ? session
            : throw new InvalidOperationException("An active OPC UA session is required.");

    private static bool RangesOverlap(FanucDynamicAssignment first, FanucDynamicAssignment second) =>
        first.StartAddress <= second.EndAddress && second.StartAddress <= first.EndAddress;

    internal static StatusCode EnsureGoodResult(StatusCodeCollection? results, string message)
    {
        if (results is null || results.Count != 1 || !StatusCode.IsGood(results[0]))
            throw new FanucOpcUaConnectionException($"{message} A Good OPC UA status is required; received {results?.FirstOrDefault()}.");
        return results[0];
    }

    private readonly record struct DynamicAssignmentConfigurationResult(StatusCode Status, bool CommandIssued)
    {
        public static DynamicAssignmentConfigurationResult Cached => new(StatusCodes.Good, false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }
}
