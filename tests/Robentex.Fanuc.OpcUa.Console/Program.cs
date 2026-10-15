using System.Reflection;
using Opc.Ua;
using Opc.Ua.Client;
using Robentex.Fanuc.OpcUa;

const string Override = "$MCR.$GENOVERRIDE";
const string Amps = "$AWEPOR[1].$AMPS_FDBK";
const string ArcOn = "$AWEPOR[1].$ARC_ON";
const string ArcEnable = "$AWEPOR[1].$ARC_ENABLE";
const int TestRegisterNumber = 100;
const int DefaultTestPositionRegisterNumber = 100;

var endpoint = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)) ?? "opc.tcp://127.0.0.1:4880";
var allowInsecure = args.Any(arg => string.Equals(arg, "--allow-insecure", StringComparison.OrdinalIgnoreCase));
var verifyWritePolicy = args.Any(arg => string.Equals(arg, "--verify-write-policy", StringComparison.OrdinalIgnoreCase));
var testSystemWrite = args.Any(arg => string.Equals(arg, "--test-system-write", StringComparison.OrdinalIgnoreCase));
var testRegisterRead = args.Any(arg => string.Equals(arg, "--test-register-read", StringComparison.OrdinalIgnoreCase));
var testRegisterWrite = args.Any(arg => string.Equals(arg, "--test-register-write", StringComparison.OrdinalIgnoreCase));
var testPositionRead = args.Any(arg => string.Equals(arg, "--test-position-read", StringComparison.OrdinalIgnoreCase));
var testPositionWrite = args.Any(arg => string.Equals(arg, "--test-position-write", StringComparison.OrdinalIgnoreCase));
var testPositionConfigRead = args.Any(arg => string.Equals(arg, "--test-position-config-read", StringComparison.OrdinalIgnoreCase));
var testPositionConfigWrite = args.Any(arg => string.Equals(arg, "--test-position-config-write", StringComparison.OrdinalIgnoreCase));
var testDigitalIoRead = args.Any(arg => string.Equals(arg, "--test-digital-io-read", StringComparison.OrdinalIgnoreCase));
var testDigitalOutputWrite = args.Any(arg => string.Equals(arg, "--test-digital-output-write", StringComparison.OrdinalIgnoreCase));
var testExtendedIoRead = args.Any(arg => string.Equals(arg, "--test-extended-io-read", StringComparison.OrdinalIgnoreCase));
var testRobotStatusDiscovery = args.Any(arg => string.Equals(arg, "--test-robot-status-discovery", StringComparison.OrdinalIgnoreCase));
var testRobotStatus = args.Any(arg => string.Equals(arg, "--test-robot-status", StringComparison.OrdinalIgnoreCase));
var testProgramStatus = args.Any(arg => string.Equals(arg, "--test-program-status", StringComparison.OrdinalIgnoreCase));
var testArcBooleanDiagnostics = args.Any(arg => string.Equals(arg, "--test-arc-bool-diagnostics", StringComparison.OrdinalIgnoreCase));
var testStatusRemap = args.Any(arg => string.Equals(arg, "--test-status-remap", StringComparison.OrdinalIgnoreCase));
var testStatusDiscoveryRemap = args.Any(arg => string.Equals(arg, "--test-status-discovery-remap", StringComparison.OrdinalIgnoreCase));
var testStatusServiceRemap = args.Any(arg => string.Equals(arg, "--test-status-service-remap", StringComparison.OrdinalIgnoreCase));
var testStatusNodeAttributeRemap = args.Any(arg => string.Equals(arg, "--test-status-node-attribute-remap", StringComparison.OrdinalIgnoreCase));
var testStatusPreReadRemap = args.Any(arg => string.Equals(arg, "--test-status-pre-read-remap", StringComparison.OrdinalIgnoreCase));
var testStatusSetAsgRefresh = args.Any(arg => string.Equals(arg, "--test-status-setasg-refresh", StringComparison.OrdinalIgnoreCase));
var testMixedMonitoring = args.Any(arg => string.Equals(arg, "--test-mixed-monitoring", StringComparison.OrdinalIgnoreCase));
var skipRobotStatus = args.Any(arg => string.Equals(arg, "--skip-robot-status", StringComparison.OrdinalIgnoreCase));
var skipProgramStatus = args.Any(arg => string.Equals(arg, "--skip-program-status", StringComparison.OrdinalIgnoreCase));
var simulationConfirmed = args.Any(arg => string.Equals(arg, "--simulation", StringComparison.OrdinalIgnoreCase));
var registerNumber = ParseRegisterNumber(args) ?? TestRegisterNumber;
var positionRegisterNumber = ParsePositionRegisterNumber(args) ?? DefaultTestPositionRegisterNumber;
var taskNumber = ParseTaskNumber(args) ?? 1;
var mixedMonitoringCycles = ParseMixedMonitoringCycles(args) ?? 20;
var digitalInputNumber = ParseDigitalSignalNumber(args, "--di-number") ?? 1;
var digitalOutputNumber = ParseDigitalSignalNumber(args, "--do-number") ?? 1;
var robotInputNumber = ParseDigitalSignalNumber(args, "--ri-number") ?? 1;
var robotOutputNumber = ParseDigitalSignalNumber(args, "--ro-number") ?? 1;
var groupInputNumber = ParseGroupSignalNumber(args, "--gi-number") ?? 1;
var groupOutputNumber = ParseGroupSignalNumber(args, "--go-number") ?? 1;
var statusNodeIds = ParseStatusNodeIds(args);
var namedStatusNodeIds = ParseNamedStatusNodeIds(args);
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
{
    Console.Error.WriteLine("Usage: dotnet run -- [opc.tcp://host:port] [--allow-insecure]");
    return 2;
}

if (verifyWritePolicy)
{
    await using var policyClient = new FanucOpcUaClient(Options(endpointUri, allowInsecure));
    try
    {
        await policyClient.WriteSystemVariableAsync(Override, 25);
        Console.Error.WriteLine("Write policy verification failed: a default-disabled write was accepted.");
        return 1;
    }
    catch (InvalidOperationException exception) when (exception.Message.StartsWith("System-variable writes are disabled", StringComparison.Ordinal))
    {
        Console.WriteLine("Write policy verification passed: default-disabled writes are rejected before any OPC UA operation.");
        return 0;
    }
}

if (testPositionConfigRead)
{
    return await TestPositionConfigurationReadAsync(endpointUri, allowInsecure, positionRegisterNumber);
}

if (testRegisterRead)
{
    return await TestRegisterReadAsync(endpointUri, allowInsecure, registerNumber);
}

if (testPositionRead)
{
    return await TestPositionReadAsync(endpointUri, allowInsecure, positionRegisterNumber);
}

if (testDigitalIoRead)
{
    return await TestDigitalIoReadAsync(endpointUri, allowInsecure, digitalInputNumber, digitalOutputNumber);
}

if (testExtendedIoRead)
{
    return await TestExtendedIoReadAsync(endpointUri, allowInsecure, robotInputNumber, robotOutputNumber, groupInputNumber, groupOutputNumber);
}

if (testRobotStatusDiscovery)
{
    return await TestRobotStatusDiscoveryAsync(endpointUri, allowInsecure);
}

if (testRobotStatus)
{
    return await TestRobotStatusAsync(endpointUri, allowInsecure);
}

if (testProgramStatus)
{
    return await TestProgramStatusAsync(endpointUri, allowInsecure, taskNumber);
}

if (testArcBooleanDiagnostics)
{
    return await TestArcBooleanDiagnosticsAsync(endpointUri, allowInsecure, skipRobotStatus, skipProgramStatus);
}

if (testMixedMonitoring)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Mixed monitoring diagnostic is blocked. It requires --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }

    return await TestMixedMonitoringAsync(endpointUri, mixedMonitoringCycles, registerNumber, positionRegisterNumber);
}

if (testStatusRemap || testStatusDiscoveryRemap)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Status remap diagnostics are blocked. They require --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }

    return testStatusRemap
        ? await TestStatusRemapAsync(endpointUri)
        : await TestStatusDiscoveryRemapAsync(endpointUri);
}

if (testStatusServiceRemap)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Status service remap diagnostic is blocked. It requires --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }
    if (statusNodeIds.Count == 0)
    {
        Console.Error.WriteLine("Status service remap diagnostic requires one or more --status-node-id=<NodeId> arguments obtained from --test-robot-status-discovery.");
        return 2;
    }

    return await TestStatusServiceRemapAsync(endpointUri, statusNodeIds);
}

if (testStatusNodeAttributeRemap)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Status node-attribute remap diagnostic is blocked. It requires --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }
    if (namedStatusNodeIds.Count != 5)
    {
        Console.Error.WriteLine("Status node-attribute remap diagnostic requires all five NodeIds: --operation-state-node-id, --servo-state-node-id, --mode-state-node-id, --program-speed-node-id, and --alarm-node-id.");
        return 2;
    }

    return await TestStatusNodeAttributeRemapAsync(endpointUri, namedStatusNodeIds);
}

if (testStatusPreReadRemap)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Status pre-read remap diagnostic is blocked. It requires --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }
    var programSpeedNode = namedStatusNodeIds.FirstOrDefault(target => target.Name == "ProgramSpeed");
    var alarmNode = namedStatusNodeIds.FirstOrDefault(target => target.Name == "Alarm");
    if (programSpeedNode is null || alarmNode is null)
    {
        Console.Error.WriteLine("Status pre-read remap diagnostic requires --program-speed-node-id and --alarm-node-id from the status discovery output.");
        return 2;
    }

    return await TestStatusPreReadRemapAsync(endpointUri, programSpeedNode, alarmNode);
}

if (testStatusSetAsgRefresh)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Status SETASG refresh diagnostic is blocked. It requires --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }
    var programSpeedNode = namedStatusNodeIds.FirstOrDefault(target => target.Name == "ProgramSpeed");
    if (programSpeedNode is null)
    {
        Console.Error.WriteLine("Status SETASG refresh diagnostic requires --program-speed-node-id from the status discovery output.");
        return 2;
    }

    return await TestStatusSetAsgRefreshAsync(endpointUri, programSpeedNode);
}

if (testSystemWrite || testRegisterWrite || testPositionWrite || testPositionConfigWrite || testDigitalOutputWrite)
{
    if (!simulationConfirmed || !allowInsecure || !IsLoopback(endpointUri))
    {
        Console.Error.WriteLine("Write test is blocked. It requires --simulation --allow-insecure and a loopback endpoint (127.0.0.1, localhost, or ::1).");
        return 2;
    }

    if (testSystemWrite) return await TestSystemWriteAsync(endpointUri);
    if (testRegisterWrite) return await TestRegisterWriteAsync(endpointUri, registerNumber);
    if (testPositionConfigWrite) return await TestPositionConfigurationWriteAsync(endpointUri, positionRegisterNumber);
    if (testDigitalOutputWrite) return await TestDigitalOutputWriteAsync(endpointUri, digitalOutputNumber);
    return await TestPositionWriteAsync(endpointUri, positionRegisterNumber);
}

try
{
    await using var client = new FanucOpcUaClient(Options(endpointUri, allowInsecure));
    await client.ConnectAsync();
    Console.WriteLine($"Connected to {endpointUri}; state={client.State}; insecure allowed={allowInsecure}.");

    await SameVariableAsync(client);
    await MixedTypesAsync(client);
    await CapacityAsync(endpointUri, allowInsecure);
    await ReconnectAsync(client);

    await client.DisconnectAsync();
    Console.WriteLine("All reliability checks passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Reliability check failed: {exception.Message}");
    Console.Error.WriteLine(exception.InnerException?.Message);
    return 1;
}

static FanucOpcUaConnectionOptions Options(Uri endpoint, bool allowInsecure, int start = 10001, int end = 16384) => new()
{
    EndpointUrl = endpoint,
    AllowInsecureConnection = allowInsecure,
    DynamicAssignmentRangeVerified = true, // ROBOGUIDE's 1-10000 persistent range was explicitly verified for this test project.
    DynamicAssignmentStartAddress = start,
    DynamicAssignmentEndAddress = end
};

static async Task<int> TestSystemWriteAsync(Uri endpoint)
{
    const int testValue = 20;
    var options = new FanucOpcUaConnectionOptions
    {
        EndpointUrl = endpoint,
        AllowInsecureConnection = true,
        DynamicAssignmentRangeVerified = true,
        EnableSystemVariableWrites = true,
        WritableSystemVariables = new HashSet<string>(StringComparer.Ordinal) { Override }
    };

    await using var client = new FanucOpcUaClient(options);
    int? originalValue = null;
    var writeAttempted = false;
    Exception? testFailure = null;

    try
    {
        Console.WriteLine("SIMULATION-ONLY system-write test started for $MCR.$GENOVERRIDE.");
        await client.ConnectAsync();
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        var before = await client.ReadSystemVariableAsync<int>(Override);
        originalValue = before.Value;
        PrintReadResult("Original value", before);

        writeAttempted = true;
        var write = await client.WriteSystemVariableAsync(Override, testValue);
        PrintWriteResult($"Write test value {testValue}", write);

        var after = await client.ReadSystemVariableAsync<int>(Override);
        PrintReadResult("Read-back after test write", after);
        Require(after.Value == testValue, $"Expected {testValue} after test write, received {after.Value}.");
        Console.WriteLine("Test write verified.");
        return 0;
    }
    catch (Exception exception)
    {
        testFailure = exception;
        Console.Error.WriteLine($"System-write test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (writeAttempted && originalValue.HasValue)
        {
            try
            {
                var restore = await client.WriteSystemVariableAsync(Override, originalValue.Value);
                PrintWriteResult($"Restore original value {originalValue.Value}", restore);
                var restored = await client.ReadSystemVariableAsync<int>(Override);
                PrintReadResult("Read-back after restore", restored);
                Require(restored.Value == originalValue.Value, $"Restore verification failed: expected {originalValue.Value}, received {restored.Value}.");
                Console.WriteLine("Original value restored.");
            }
            catch (Exception restoreException)
            {
                Console.Error.WriteLine($"CRITICAL: original value restore failed: {restoreException.Message}");
                if (testFailure is null) throw;
            }
        }

        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestRegisterWriteAsync(Uri endpoint, int registerNumber)
{
    var options = new FanucOpcUaConnectionOptions
    {
        EndpointUrl = endpoint,
        AllowInsecureConnection = true,
        DynamicAssignmentRangeVerified = true,
        EnableRegisterWrites = true,
        WritableRegisters = new HashSet<int> { registerNumber }
    };

    await using var client = new FanucOpcUaClient(options);
    double? originalValue = null;
    var writeAttempted = false;
    Exception? testFailure = null;

    try
    {
        Console.WriteLine($"SIMULATION-ONLY R[] write test started for R[{registerNumber}].");
        await client.ConnectAsync();
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        originalValue = await client.ReadRegisterAsync(registerNumber);
        Console.WriteLine($"Original R[{registerNumber}]={originalValue.Value:R}");

        writeAttempted = true;
        await client.WriteRegisterAsync(registerNumber, 10.25);
        var positive = await client.ReadRegisterAsync(registerNumber);
        Console.WriteLine($"R[{registerNumber}] after 10.25 write={positive:R}");
        Require(positive == 10.25, $"Expected R[{registerNumber}]=10.25, received {positive:R}.");

        await client.WriteRegisterAsync(registerNumber, -35.75);
        var negative = await client.ReadRegisterAsync(registerNumber);
        Console.WriteLine($"R[{registerNumber}] after -35.75 write={negative:R}");
        Require(negative == -35.75, $"Expected R[{registerNumber}]=-35.75, received {negative:R}.");
        Console.WriteLine("R[] positive and negative decimal writes verified.");
        return 0;
    }
    catch (Exception exception)
    {
        testFailure = exception;
        Console.Error.WriteLine($"R[] write test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (writeAttempted && originalValue.HasValue)
        {
            try
            {
                await client.WriteRegisterAsync(registerNumber, originalValue.Value);
                var restored = await client.ReadRegisterAsync(registerNumber);
                Console.WriteLine($"R[{registerNumber}] after restore={restored:R}");
                Require(restored == originalValue.Value, $"R[{registerNumber}] restore verification failed: expected {originalValue.Value:R}, received {restored:R}.");
                Console.WriteLine("Original R[] value restored.");
            }
            catch (Exception restoreException)
            {
                Console.Error.WriteLine($"CRITICAL: original R[] value restore failed: {restoreException.Message}");
                if (testFailure is null) throw;
            }
        }

        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestRegisterReadAsync(Uri endpoint, bool allowInsecure, int registerNumber)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine($"Read-only FANUC R[] test started for R[{registerNumber}].");
        await client.ConnectAsync();
        Console.WriteLine($"R[{registerNumber}]={await client.ReadRegisterAsync(registerNumber):R}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"R[] read test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestPositionWriteAsync(Uri endpoint, int number)
{
    var options = new FanucOpcUaConnectionOptions
    {
        EndpointUrl = endpoint,
        AllowInsecureConnection = true,
        DynamicAssignmentRangeVerified = true,
        EnablePositionRegisterWrites = true,
        WritablePositionRegisters = new HashSet<int> { number }
    };

    await using var client = new FanucOpcUaClient(options);
    FanucPosition? original = null;
    var writeAttempted = false;
    Exception? testFailure = null;
    try
    {
        Console.WriteLine($"SIMULATION-ONLY PR[] write test started for PR[{number}].");
        await client.ConnectAsync();
        original = await client.ReadPositionRegisterAsync(number);
        Console.WriteLine($"Original PR[{number}]: {FormatPosition(original)}");

        writeAttempted = true;
        var testPosition = new FanucPosition(10.25, -35.75, 100d, 0d, 0d, 0d);
        await client.WritePositionRegisterAsync(number, testPosition);
        var afterFullWrite = await client.ReadPositionRegisterAsync(number);
        Console.WriteLine($"After full Cartesian write: {FormatPosition(afterFullWrite)}");
        Require(afterFullWrite == testPosition, "Full Cartesian PR[] write read-back differs.");

        await client.WritePositionRegisterAsync(number, new FanucPosition(X: 20.5));
        var afterPartialWrite = await client.ReadPositionRegisterAsync(number);
        Console.WriteLine($"After partial X update: {FormatPosition(afterPartialWrite)}");
        Require(afterPartialWrite.X == 20.5 && afterPartialWrite.Y == -35.75, "Partial PR[] update did not preserve the preceding Cartesian values.");
        Console.WriteLine("PR[] full and partial Cartesian writes verified.");
        return 0;
    }
    catch (Exception exception)
    {
        testFailure = exception;
        Console.Error.WriteLine($"PR[] write test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (writeAttempted && original is not null)
        {
            try
            {
                await client.WritePositionRegisterAsync(number, original);
                var restored = await client.ReadPositionRegisterAsync(number);
                Console.WriteLine($"After restore: {FormatPosition(restored)}");
                Require(restored == original, "PR[] restore verification failed.");
                Console.WriteLine("Original PR[] Cartesian value restored.");
            }
            catch (Exception restoreException)
            {
                Console.Error.WriteLine($"CRITICAL: original PR[] value restore failed: {restoreException.Message}");
                if (testFailure is null) throw;
            }
        }
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestPositionConfigurationReadAsync(Uri endpoint, bool allowInsecure, int number)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine($"Read-only PR[] CONFIG test started for PR[{number}].");
        await client.ConnectAsync();
        var configuration = await client.ReadPositionRegisterConfigurationAsync(number);
        Console.WriteLine($"PR[{number}]@19.8 configuration fields:");
        Console.WriteLine($"  FLIP={configuration.Flip}");
        Console.WriteLine($"  LEFT={configuration.Left}");
        Console.WriteLine($"  UP={configuration.Up}");
        Console.WriteLine($"  FRONT={configuration.Front}");
        Console.WriteLine($"  TURN4={configuration.Turn4}");
        Console.WriteLine($"  TURN5={configuration.Turn5}");
        Console.WriteLine($"  TURN6={configuration.Turn6}");
        Console.WriteLine($"  VALIDC={configuration.ValidCartesian}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"PR[] CONFIG read test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestPositionReadAsync(Uri endpoint, bool allowInsecure, int number)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine($"Read-only FANUC PR[] test started for PR[{number}].");
        await client.ConnectAsync();
        var position = await client.ReadPositionRegisterAsync(number);
        var configuration = await client.ReadPositionRegisterConfigurationAsync(number);
        Console.WriteLine($"PR[{number}]: {FormatPosition(position)}");
        Console.WriteLine($"  {FormatConfiguration(configuration)}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"PR[] read test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestDigitalIoReadAsync(Uri endpoint, bool allowInsecure, int inputNumber, int outputNumber)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine($"Read-only digital I/O test started for DI[{inputNumber}] and DO[{outputNumber}].");
        await client.ConnectAsync();
        var input = await client.ReadDigitalInputAsync(inputNumber);
        var output = await client.ReadDigitalOutputAsync(outputNumber);
        Console.WriteLine($"DI[{inputNumber}]={(input ? "ON" : "OFF")}");
        Console.WriteLine($"DO[{outputNumber}]={(output ? "ON" : "OFF")}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Digital I/O read test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestDigitalOutputWriteAsync(Uri endpoint, int outputNumber)
{
    var options = new FanucOpcUaConnectionOptions
    {
        EndpointUrl = endpoint,
        AllowInsecureConnection = true,
        DynamicAssignmentRangeVerified = true,
        EnableDigitalOutputWrites = true,
        WritableDigitalOutputs = new HashSet<int> { outputNumber }
    };

    await using var client = new FanucOpcUaClient(options);
    bool? originalValue = null;
    var writeAttempted = false;
    Exception? testFailure = null;
    try
    {
        Console.WriteLine($"SIMULATION-ONLY DO[] write test started for DO[{outputNumber}].");
        await client.ConnectAsync();
        originalValue = await client.ReadDigitalOutputAsync(outputNumber);
        Console.WriteLine($"Original DO[{outputNumber}]={(originalValue.Value ? "ON (1)" : "OFF (0)")}");

        writeAttempted = true;
        await client.WriteDigitalOutputAsync(outputNumber, true);
        var onValue = await client.ReadDigitalOutputAsync(outputNumber);
        Console.WriteLine($"DO[{outputNumber}] after ON write={(onValue ? "ON (1)" : "OFF (0)")}");
        Require(onValue, $"Expected DO[{outputNumber}] to be ON after the ON write.");

        await client.WriteDigitalOutputAsync(outputNumber, false);
        var offValue = await client.ReadDigitalOutputAsync(outputNumber);
        Console.WriteLine($"DO[{outputNumber}] after OFF write={(offValue ? "ON (1)" : "OFF (0)")}");
        Require(!offValue, $"Expected DO[{outputNumber}] to be OFF after the OFF write.");
        Console.WriteLine("DO[] ON/OFF writes verified.");
        return 0;
    }
    catch (Exception exception)
    {
        testFailure = exception;
        Console.Error.WriteLine($"DO[] write test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (writeAttempted && originalValue.HasValue)
        {
            try
            {
                await client.WriteDigitalOutputAsync(outputNumber, originalValue.Value);
                var restored = await client.ReadDigitalOutputAsync(outputNumber);
                Console.WriteLine($"DO[{outputNumber}] after restore={(restored ? "ON (1)" : "OFF (0)")}");
                Require(restored == originalValue.Value, $"DO[{outputNumber}] restore verification failed.");
                Console.WriteLine("Original DO[] value restored.");
            }
            catch (Exception restoreException)
            {
                Console.Error.WriteLine($"CRITICAL: original DO[] value restore failed: {restoreException.Message}");
                if (testFailure is null) throw;
            }
        }

        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestExtendedIoReadAsync(Uri endpoint, bool allowInsecure, int robotInputNumber, int robotOutputNumber, int groupInputNumber, int groupOutputNumber)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine($"Read-only extended I/O test started for RI[{robotInputNumber}], RO[{robotOutputNumber}], GI[{groupInputNumber}], and GO[{groupOutputNumber}].");
        await client.ConnectAsync();
        var robotInput = await client.ReadRobotInputAsync(robotInputNumber);
        var robotOutput = await client.ReadRobotOutputAsync(robotOutputNumber);
        var groupInput = await client.ReadGroupInputAsync(groupInputNumber);
        var groupOutput = await client.ReadGroupOutputAsync(groupOutputNumber);
        Console.WriteLine($"RI[{robotInputNumber}]={(robotInput ? "ON" : "OFF")}");
        Console.WriteLine($"RO[{robotOutputNumber}]={(robotOutput ? "ON" : "OFF")}");
        Console.WriteLine($"GI[{groupInputNumber}]={groupInput} (0x{groupInput:X4})");
        Console.WriteLine($"GO[{groupOutputNumber}]={groupOutput} (0x{groupOutput:X4})");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Extended I/O read test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestRobotStatusDiscoveryAsync(Uri endpoint, bool allowInsecure)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine("Read-only FANUC §55.5 robot-status discovery started.");
        await client.ConnectAsync();
        foreach (var node in await client.DiscoverRobotStatusNodesAsync())
        {
            if (!node.Found)
            {
                Console.WriteLine($"{node.Name}: NOT FOUND (the controller may not support this optional §55.5 node).");
                continue;
            }

            Console.WriteLine($"{node.Name}: path={node.BrowsePath}, NodeId={node.NodeId}");
            Console.WriteLine($"  dataType={node.DataTypeNodeId ?? "<unavailable>"}, runtimeType={node.RuntimeValueType ?? "<null>"}, value={node.Value ?? "<null>"}");
            Console.WriteLine($"  value StatusCode={node.ValueStatusCode}, dataType StatusCode={node.DataTypeStatusCode}");
        }
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Robot-status discovery failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestRobotStatusAsync(Uri endpoint, bool allowInsecure)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine("Read-only FANUC §55.5 robot-status read started.");
        await client.ConnectAsync();
        var status = await client.GetRobotStatusAsync();

        Console.WriteLine($"OperationState raw={FormatOptional(status.OperationStateRaw)}");
        Console.WriteLine($"  E-STOP={FormatOptional(status.EmergencyStop)}, Abort={FormatOptional(status.Abort)}, PROGRUN={FormatOptional(status.ProgramRunning)}");
        Console.WriteLine($"  PAUSED={FormatOptional(status.Paused)}, HELD={FormatOptional(status.Held)}, FAULT={FormatOptional(status.Fault)}");
        Console.WriteLine($"  TPENBL={FormatOptional(status.TeachPendantEnabled)}, BATALM={FormatOptional(status.BatteryAlarm)}, BUSY={FormatOptional(status.Busy)}");
        Console.WriteLine($"ServoState={FormatOptional(status.ServoOn)}, Mode={FormatOptional(status.Mode)}, ModeState raw={status.ModeStateRaw ?? "<unavailable>"}");
        Console.WriteLine($"ProgramSpeed={FormatOptional(status.ProgramSpeedPercent)}%");
        Console.WriteLine(status.Alarm is null
            ? "Alarm=<unavailable>"
            : status.Alarm.Length == 0 ? "Alarm=<none>" : $"Alarm={string.Join(" | ", status.Alarm)}");

        foreach (var node in status.Nodes.Where(node => !node.Found || node.ValueStatusCode is not { } valueStatus || !Opc.Ua.StatusCode.IsGood(valueStatus)))
        {
            var reason = !node.Found ? "NOT FOUND" : $"Value StatusCode={node.ValueStatusCode}";
            Console.WriteLine($"{node.Name}: {reason}");
        }

        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Robot-status read failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestProgramStatusAsync(Uri endpoint, bool allowInsecure, int taskNumber)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    try
    {
        Console.WriteLine($"Read-only FANUC §55.3.7 program-status read started for PRG[{taskNumber}].");
        await client.ConnectAsync();
        var status = await client.ReadProgramStatusAsync(taskNumber);
        Console.WriteLine($"Task={status.TaskNumber}, Program='{status.ProgramName}', Line={status.LineNumber}, State={status.ExecutionStatus}, ParentProgram='{status.ParentProgramName}'");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Program-status read failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestArcBooleanDiagnosticsAsync(
    Uri endpoint,
    bool allowInsecure,
    bool skipRobotStatus,
    bool skipProgramStatus)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure));
    using var cancellationSource = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        Console.WriteLine("Read-only ARC BOOL diagnostic started. Press Ctrl+C to stop.");
        Console.WriteLine($"Each cycle reads enabled diagnostics, ARC_ON, and ARC_ENABLE sequentially over one OPC UA session. SkipRobotStatus={skipRobotStatus}; SkipProgramStatus={skipProgramStatus}.");
        Console.WriteLine("The two BOOL reads remain 500 ms apart.");
        await client.ConnectAsync(cancellationSource.Token);
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        var cycle = 0;
        while (!cancellationSource.IsCancellationRequested)
        {
            var cycleNumber = ++cycle;
            if (!skipRobotStatus)
                await PrintRobotStatusDiagnosticAsync(client, cycleNumber, cancellationSource.Token);
            if (!skipProgramStatus)
                await PrintProgramStatusDiagnosticAsync(client, cycleNumber, cancellationSource.Token);
            await PrintArcBooleanDiagnosticAsync(client, ArcOn, cycleNumber, cancellationSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationSource.Token);
            await PrintArcBooleanDiagnosticAsync(client, ArcEnable, cycleNumber, cancellationSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationSource.Token);
        }

        return 0;
    }
    catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
    {
        Console.WriteLine("ARC BOOL diagnostic stopped by user.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"ARC BOOL diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestMixedMonitoringAsync(
    Uri endpoint,
    int cycles,
    int registerNumber,
    int positionRegisterNumber)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    using var cancellationSource = new CancellationTokenSource();
    var outcomes = new Dictionary<string, (int Successes, int Failures)>(StringComparer.Ordinal)
    {
        ["RobotStatus"] = (0, 0),
        [ArcEnable] = (0, 0),
        [ArcOn] = (0, 0),
        [$"R[{registerNumber}]"] = (0, 0),
        [$"PR[{positionRegisterNumber}] Cartesian"] = (0, 0),
        [$"PR[{positionRegisterNumber}] CONFIG"] = (0, 0)
    };
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        Console.WriteLine($"SIMULATION-ONLY mixed monitoring diagnostic started: {cycles} cycles, 500 ms between cycles.");
        Console.WriteLine($"One OPC UA session; read-only targets: RobotStatus, {ArcEnable}, {ArcOn}, R[{registerNumber}], PR[{positionRegisterNumber}] Cartesian and CONFIG.");
        Console.WriteLine("BOOL results include the available dynamic-mapping diagnostics. R[]/PR[] public read APIs return values only.");
        await client.ConnectAsync(cancellationSource.Token);
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            Console.WriteLine($"\n--- Cycle {cycle}/{cycles} {DateTimeOffset.Now:O} ---");
            await MonitorMixedReadAsync("RobotStatus", async () =>
            {
                var status = await client.GetRobotStatusAsync(cancellationSource.Token);
                var alarms = status.Alarm is null ? "<unavailable>" : status.Alarm.Length == 0 ? "<none>" : string.Join(" | ", status.Alarm);
                return $"OperationState={FormatOptional(status.OperationStateRaw)}; ServoOn={FormatOptional(status.ServoOn)}; Mode={FormatOptional(status.Mode)}; ProgramSpeed={FormatOptional(status.ProgramSpeedPercent)}; Alarm={alarms}";
            }, outcomes);
            await MonitorMixedReadAsync(ArcEnable, async () => FormatSystemBooleanResult(await client.ReadSystemVariableAsync<bool>(ArcEnable, cancellationSource.Token)), outcomes);
            await MonitorMixedReadAsync(ArcOn, async () => FormatSystemBooleanResult(await client.ReadSystemVariableAsync<bool>(ArcOn, cancellationSource.Token)), outcomes);
            await MonitorMixedReadAsync($"R[{registerNumber}]", async () => $"Value={(await client.ReadRegisterAsync(registerNumber, cancellationSource.Token)).ToString("R", System.Globalization.CultureInfo.InvariantCulture)}", outcomes);
            await MonitorMixedReadAsync($"PR[{positionRegisterNumber}] Cartesian", async () => FormatPosition(await client.ReadPositionRegisterAsync(positionRegisterNumber, cancellationSource.Token)), outcomes);
            await MonitorMixedReadAsync($"PR[{positionRegisterNumber}] CONFIG", async () => FormatConfiguration(await client.ReadPositionRegisterConfigurationAsync(positionRegisterNumber, cancellationSource.Token)), outcomes);

            if (cycle < cycles)
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationSource.Token);
        }

        PrintMixedMonitoringSummary(outcomes, cycles, cancelled: false);
        return outcomes.Values.Any(outcome => outcome.Failures > 0) ? 1 : 0;
    }
    catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
    {
        Console.WriteLine("Mixed monitoring diagnostic stopped by user.");
        PrintMixedMonitoringSummary(outcomes, cycles, cancelled: true);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Mixed monitoring diagnostic failed: {exception.Message}");
        PrintMixedMonitoringSummary(outcomes, cycles, cancelled: false);
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task MonitorMixedReadAsync(
    string name,
    Func<Task<string>> read,
    Dictionary<string, (int Successes, int Failures)> outcomes)
{
    try
    {
        var result = await read();
        var outcome = outcomes[name];
        outcomes[name] = (outcome.Successes + 1, outcome.Failures);
        Console.WriteLine($"  {name}: {result}");
    }
    catch (Exception exception)
    {
        var outcome = outcomes[name];
        outcomes[name] = (outcome.Successes, outcome.Failures + 1);
        Console.Error.WriteLine($"  {name}: ERROR {exception.GetType().Name}: {exception.Message}");
    }
}

static string FormatSystemBooleanResult(FanucSystemVariableReadResult<bool> result) =>
    $"Value={result.Value}; Registers=[{string.Join(", ", result.Registers)}]; StartAddress={result.StartAddress}; Cached={!result.SetAsgIssued}; SetAsgStatus={result.SetAsgStatus}; SetAsgCommand='{result.SetAsgCommand}'";

static void PrintMixedMonitoringSummary(
    IReadOnlyDictionary<string, (int Successes, int Failures)> outcomes,
    int requestedCycles,
    bool cancelled)
{
    Console.WriteLine("\nMixed monitoring summary");
    Console.WriteLine($"Requested cycles={requestedCycles}; Cancelled={cancelled}");
    foreach (var (name, outcome) in outcomes)
        Console.WriteLine($"  {name}: Success={outcome.Successes}; Error={outcome.Failures}");
    Console.WriteLine($"Total: Success={outcomes.Values.Sum(outcome => outcome.Successes)}; Error={outcomes.Values.Sum(outcome => outcome.Failures)}");
}

static async Task<int> TestStatusRemapAsync(Uri endpoint)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    try
    {
        Console.WriteLine("SIMULATION-ONLY status-remap diagnostic started for $AWEPOR[1].$ARC_ENABLE.");
        await client.ConnectAsync();
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        Console.WriteLine("[1] ARC_ENABLE before GetRobotStatusAsync");
        var beforeStatus = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(beforeStatus);

        Console.WriteLine("[2] GetRobotStatusAsync");
        var robotStatus = await client.GetRobotStatusAsync();
        Console.WriteLine($"  OperationState={FormatOptional(robotStatus.OperationStateRaw)}; ServoOn={FormatOptional(robotStatus.ServoOn)}; Mode={FormatOptional(robotStatus.Mode)}; ProgramSpeed={FormatOptional(robotStatus.ProgramSpeedPercent)}");

        Console.WriteLine("[3] ARC_ENABLE after GetRobotStatusAsync");
        var afterStatus = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterStatus);

        Console.WriteLine("[4] ClearDynamicAssignmentsAsync (issues CLRASG only for this test session)");
        await client.ClearDynamicAssignmentsAsync();
        Console.WriteLine("  CLRASG completed. The next ARC_ENABLE read must issue a new SETASG.");

        Console.WriteLine("[5] ARC_ENABLE after remap");
        var afterRemap = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterRemap);

        Console.WriteLine($"Summary: beforeStatus={beforeStatus.Value}, afterStatus={afterStatus.Value}, afterRemap={afterRemap.Value}; remapSetAsgIssued={afterRemap.SetAsgIssued}.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Status-remap diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestStatusDiscoveryRemapAsync(Uri endpoint)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    try
    {
        Console.WriteLine("SIMULATION-ONLY status-discovery remap diagnostic started for $AWEPOR[1].$ARC_ENABLE.");
        Console.WriteLine("Note: DiscoverRobotStatusNodesAsync combines address-space Browse with status-node Value/DataType reads; the current public API cannot invoke those service operations separately.");
        await client.ConnectAsync();
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        Console.WriteLine("[1] ARC_ENABLE before DiscoverRobotStatusNodesAsync");
        var beforeDiscovery = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(beforeDiscovery);

        Console.WriteLine("[2] DiscoverRobotStatusNodesAsync (Browse + status-node Value/DataType reads)");
        var nodes = await client.DiscoverRobotStatusNodesAsync();
        foreach (var node in nodes)
        {
            Console.WriteLine(node.Found
                ? $"  {node.Name}: NodeId={node.NodeId}; Value={node.Value ?? "<null>"}; ValueStatus={node.ValueStatusCode}; DataTypeStatus={node.DataTypeStatusCode}"
                : $"  {node.Name}: NOT FOUND");
        }

        Console.WriteLine("[3] ARC_ENABLE after DiscoverRobotStatusNodesAsync");
        var afterDiscovery = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterDiscovery);

        Console.WriteLine("[4] ClearDynamicAssignmentsAsync (issues CLRASG only for this test session)");
        await client.ClearDynamicAssignmentsAsync();
        Console.WriteLine("  CLRASG completed. The next ARC_ENABLE read must issue a new SETASG.");

        Console.WriteLine("[5] ARC_ENABLE after remap");
        var afterRemap = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterRemap);

        Console.WriteLine($"Summary: beforeDiscovery={beforeDiscovery.Value}, afterDiscovery={afterDiscovery.Value}, afterRemap={afterRemap.Value}; remapSetAsgIssued={afterRemap.SetAsgIssued}.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Status-discovery remap diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<int> TestStatusServiceRemapAsync(Uri endpoint, IReadOnlyList<string> statusNodeIdTexts)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    try
    {
        Console.WriteLine("SIMULATION-ONLY status-service remap diagnostic started for $AWEPOR[1].$ARC_ENABLE.");
        Console.WriteLine($"Status NodeIds to read: {string.Join(", ", statusNodeIdTexts)}");
        await client.ConnectAsync();
        var session = GetDiagnosticSession(client);
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        Console.WriteLine("[1] ARC_ENABLE before raw OPC UA Browse");
        var beforeBrowse = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(beforeBrowse);

        Console.WriteLine("[2] Raw OPC UA Browse of RobotInformation and MotionDeviceSystem subtrees");
        var browsedNodeCount = await BrowseStatusSubtreesAsync(session, CancellationToken.None);
        Console.WriteLine($"  Browse completed; discovered descendant count={browsedNodeCount}.");

        Console.WriteLine("[3] ARC_ENABLE after raw OPC UA Browse");
        var afterBrowse = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterBrowse);

        Console.WriteLine("[4] Rebuild current-session dynamic mappings (CLRASG followed by ARC_ENABLE SETASG)");
        await client.ClearDynamicAssignmentsAsync();
        var afterRemap = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterRemap);

        Console.WriteLine("[5] Raw OPC UA Value/DataType Read of supplied status NodeIds");
        await ReadStatusNodeAttributesAsync(session, statusNodeIdTexts, CancellationToken.None);

        Console.WriteLine("[6] ARC_ENABLE after raw status-node Read");
        var afterStatusRead = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterStatusRead);

        Console.WriteLine($"Summary: beforeBrowse={beforeBrowse.Value}, afterBrowse={afterBrowse.Value}, afterRemap={afterRemap.Value}, afterStatusRead={afterStatusRead.Value}.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Status-service remap diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static ISession GetDiagnosticSession(FanucOpcUaClient client) =>
    typeof(FanucOpcUaClient)
        .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetValue(client) as ISession
    ?? throw new InvalidOperationException("The Console diagnostic could not access the active client session. This test is intentionally limited to the current library implementation.");

static async Task<int> BrowseStatusSubtreesAsync(ISession session, CancellationToken cancellationToken)
{
    var rootReferences = await BrowseDiagnosticChildrenAsync(session, ObjectIds.ObjectsFolder, cancellationToken);
    var roots = rootReferences
        .Where(reference => reference.BrowseName.Name is "RobotInformation" or "MotionDeviceSystem")
        .Select(reference => ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris))
        .Where(nodeId => nodeId is not null)
        .Cast<NodeId>()
        .ToArray();

    var count = 0;
    foreach (var root in roots)
        count += await BrowseDiagnosticDescendantsAsync(session, root, cancellationToken);
    return count;
}

static async Task<int> BrowseDiagnosticDescendantsAsync(ISession session, NodeId rootNodeId, CancellationToken cancellationToken)
{
    var visited = new HashSet<NodeId> { rootNodeId };
    var pending = new Queue<NodeId>();
    pending.Enqueue(rootNodeId);

    while (pending.TryDequeue(out var parentNodeId))
    {
        foreach (var reference in await BrowseDiagnosticChildrenAsync(session, parentNodeId, cancellationToken))
        {
            var childNodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);
            if (childNodeId is not null && visited.Add(childNodeId))
                pending.Enqueue(childNodeId);
        }
    }

    return visited.Count - 1;
}

static async Task<IReadOnlyList<ReferenceDescription>> BrowseDiagnosticChildrenAsync(ISession session, NodeId nodeId, CancellationToken cancellationToken)
{
    var browser = new Browser(session, new BrowserOptions())
    {
        BrowseDirection = BrowseDirection.Forward,
        ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
        IncludeSubtypes = true,
        ContinueUntilDone = true
    };
    return (await browser.BrowseAsync(nodeId, cancellationToken)).ToArray();
}

static async Task ReadStatusNodeAttributesAsync(ISession session, IReadOnlyList<string> statusNodeIdTexts, CancellationToken cancellationToken)
{
    var nodeIds = statusNodeIdTexts.Select(NodeId.Parse).ToArray();
    var readValues = new ReadValueIdCollection();
    foreach (var nodeId in nodeIds)
    {
        readValues.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.Value });
        readValues.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.DataType });
    }

    var response = await session.ReadAsync(null, 0, TimestampsToReturn.Neither, readValues, cancellationToken);
    if (response.Results.Count != readValues.Count)
        throw new InvalidOperationException($"Status-node Read returned {response.Results.Count} results; {readValues.Count} were expected.");

    for (var index = 0; index < nodeIds.Length; index++)
    {
        var value = response.Results[index * 2];
        var dataType = response.Results[index * 2 + 1];
        Console.WriteLine($"  {nodeIds[index]}: Value={value.Value ?? "<null>"}; ValueStatus={value.StatusCode}; DataType={dataType.Value ?? "<null>"}; DataTypeStatus={dataType.StatusCode}");
    }
}

static async Task<int> TestStatusNodeAttributeRemapAsync(Uri endpoint, IReadOnlyList<StatusNodeTarget> targets)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    try
    {
        Console.WriteLine("SIMULATION-ONLY per-node status attribute remap diagnostic started for $AWEPOR[1].$ARC_ENABLE.");
        await client.ConnectAsync();
        var session = GetDiagnosticSession(client);
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        var observations = new List<StatusNodeAttributeObservation>();
        foreach (var target in targets)
        {
            foreach (var attribute in new[] { Attributes.Value, Attributes.DataType })
            {
                var attributeName = attribute == Attributes.Value ? "Value" : "DataType";
                Console.WriteLine($"\n{target.Name} ({target.NodeId}) / {attributeName}");
                Console.WriteLine("  [1] Rebuild session-local mapping with CLRASG + ARC_ENABLE SETASG");
                await client.ClearDynamicAssignmentsAsync();
                var beforeAttributeRead = await client.ReadSystemVariableAsync<bool>(ArcEnable);
                PrintStatusRemapRead(beforeAttributeRead);
                Require(beforeAttributeRead.Value, $"ARC_ENABLE must be TRUE before testing {target.Name}.{attributeName}; received registers [{string.Join(", ", beforeAttributeRead.Registers)}].");

                Console.WriteLine($"  [2] Raw OPC UA {attributeName} Read");
                var attributeResult = await ReadStatusNodeAttributeAsync(session, target.NodeId, attribute, CancellationToken.None);
                Console.WriteLine($"    {attributeName}={attributeResult.Value ?? "<null>"}; Status={attributeResult.StatusCode}");

                Console.WriteLine("  [3] ARC_ENABLE after attribute Read");
                var afterAttributeRead = await client.ReadSystemVariableAsync<bool>(ArcEnable);
                PrintStatusRemapRead(afterAttributeRead);
                observations.Add(new StatusNodeAttributeObservation(
                    target.Name,
                    attributeName,
                    attributeResult.StatusCode.ToString(),
                    beforeAttributeRead.Value,
                    afterAttributeRead.Value,
                    string.Join(", ", afterAttributeRead.Registers),
                    !afterAttributeRead.SetAsgIssued));
            }
        }

        PrintStatusNodeAttributeSummary(observations);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Status node-attribute remap diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task<DataValue> ReadStatusNodeAttributeAsync(ISession session, NodeId nodeId, uint attributeId, CancellationToken cancellationToken)
{
    var response = await session.ReadAsync(
        null,
        0,
        TimestampsToReturn.Neither,
        new ReadValueIdCollection { new() { NodeId = nodeId, AttributeId = attributeId } },
        cancellationToken);
    if (response.Results.Count != 1)
        throw new InvalidOperationException($"Status-node attribute Read returned {response.Results.Count} results; one was expected.");
    return response.Results[0];
}

static async Task<int> TestStatusPreReadRemapAsync(Uri endpoint, StatusNodeTarget programSpeedNode, StatusNodeTarget alarmNode)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    try
    {
        Console.WriteLine("SIMULATION-ONLY ProgramSpeed/Alarm pre-read remap diagnostic started for $AWEPOR[1].$ARC_ENABLE.");
        await client.ConnectAsync();
        var session = GetDiagnosticSession(client);
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        Console.WriteLine("[1] Read ProgramSpeed.Value and Alarm.Value before ARC_ENABLE dynamic mapping");
        await PrintStatusValueAsync(session, programSpeedNode, CancellationToken.None);
        await PrintStatusValueAsync(session, alarmNode, CancellationToken.None);

        Console.WriteLine("[2] Create ARC_ENABLE dynamic mapping and read it");
        var readings = new List<FanucSystemVariableReadResult<bool>>();
        var initial = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        readings.Add(initial);
        PrintStatusRemapRead(initial);

        Console.WriteLine("[3] ARC_ENABLE follow-up reads (three cycles, 500 ms apart)");
        for (var cycle = 1; cycle <= 3; cycle++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            var result = await client.ReadSystemVariableAsync<bool>(ArcEnable);
            readings.Add(result);
            Console.WriteLine($"  Cycle {cycle}:");
            PrintStatusRemapRead(result);
        }

        Console.WriteLine("[4] Read ProgramSpeed.Value and Alarm.Value again after ARC_ENABLE mapping");
        await PrintStatusValueAsync(session, programSpeedNode, CancellationToken.None);
        await PrintStatusValueAsync(session, alarmNode, CancellationToken.None);

        Console.WriteLine("[5] ARC_ENABLE after repeated status Value reads");
        var afterStatusReads = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        readings.Add(afterStatusReads);
        PrintStatusRemapRead(afterStatusReads);

        Console.WriteLine("\nSummary");
        Console.WriteLine("| Phase | Value | Registers | Cached | SETASG Status |");
        Console.WriteLine("|---|---|---|---|---|");
        Console.WriteLine($"| Initial after pre-read | {initial.Value} | [{string.Join(", ", initial.Registers)}] | {!initial.SetAsgIssued} | {initial.SetAsgStatus} |");
        for (var index = 1; index <= 3; index++)
        {
            var reading = readings[index];
            Console.WriteLine($"| Follow-up {index} | {reading.Value} | [{string.Join(", ", reading.Registers)}] | {!reading.SetAsgIssued} | {reading.SetAsgStatus} |");
        }
        Console.WriteLine($"| After repeated status reads | {afterStatusReads.Value} | [{string.Join(", ", afterStatusReads.Registers)}] | {!afterStatusReads.SetAsgIssued} | {afterStatusReads.SetAsgStatus} |");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Status pre-read remap diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static async Task PrintStatusValueAsync(ISession session, StatusNodeTarget target, CancellationToken cancellationToken)
{
    var result = await ReadStatusNodeAttributeAsync(session, target.NodeId, Attributes.Value, cancellationToken);
    Console.WriteLine($"  {target.Name}.Value: {result.Value ?? "<null>"}; Status={result.StatusCode}; NodeId={target.NodeId}");
}

static async Task<int> TestStatusSetAsgRefreshAsync(Uri endpoint, StatusNodeTarget programSpeedNode)
{
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure: true));
    try
    {
        Console.WriteLine("SIMULATION-ONLY ProgramSpeed SETASG refresh diagnostic started for $AWEPOR[1].$ARC_ENABLE.");
        await client.ConnectAsync();
        var session = GetDiagnosticSession(client);
        var options = GetDiagnosticOptions(client);
        Console.WriteLine($"Connected: state={client.State}, endpoint={endpoint}");

        Console.WriteLine("[1] Create ARC_ENABLE dynamic mapping and verify TRUE");
        var beforeStatusRead = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(beforeStatusRead);
        Require(beforeStatusRead.Value, $"ARC_ENABLE must be TRUE before ProgramSpeed.Value Read; received registers [{string.Join(", ", beforeStatusRead.Registers)}].");

        Console.WriteLine("[2] Read ProgramSpeed.Value and verify ARC_ENABLE becomes FALSE");
        await PrintStatusValueAsync(session, programSpeedNode, CancellationToken.None);
        var afterStatusRead = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterStatusRead);
        Require(!afterStatusRead.Value, $"Expected ProgramSpeed.Value Read to invalidate ARC_ENABLE for this diagnostic; received registers [{string.Join(", ", afterStatusRead.Registers)}].");

        Console.WriteLine("[3] Resend the same SETASG without CLRASG");
        var command = $"SETASG {beforeStatusRead.StartAddress} {beforeStatusRead.Registers.Count} {ArcEnable}";
        var setAsgStatus = await WriteDiagnosticCommandAsync(session, options.CommandNodeId, command, CancellationToken.None);
        Console.WriteLine($"  Command='{command}'; Status={setAsgStatus}");

        Console.WriteLine("[4] ARC_ENABLE after same-address SETASG refresh");
        var afterRefresh = await client.ReadSystemVariableAsync<bool>(ArcEnable);
        PrintStatusRemapRead(afterRefresh);
        Console.WriteLine($"Summary: before={beforeStatusRead.Value}, after ProgramSpeed.Value={afterStatusRead.Value}, after same-address SETASG={afterRefresh.Value}; SETASG status={setAsgStatus}.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Status SETASG refresh diagnostic failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static FanucOpcUaConnectionOptions GetDiagnosticOptions(FanucOpcUaClient client) =>
    typeof(FanucOpcUaClient)
        .GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetValue(client) as FanucOpcUaConnectionOptions
    ?? throw new InvalidOperationException("The Console diagnostic could not access the active client options. This test is intentionally limited to the current library implementation.");

static async Task<StatusCode> WriteDiagnosticCommandAsync(ISession session, string commandNodeId, string command, CancellationToken cancellationToken)
{
    var response = await session.WriteAsync(
        null,
        new WriteValueCollection
        {
            new()
            {
                NodeId = NodeId.Parse(commandNodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(command)
            }
        },
        cancellationToken);
    if (response.Results.Count != 1 || !StatusCode.IsGood(response.Results[0]))
        throw new InvalidOperationException($"Same-address SETASG command was rejected; Status={response.Results.FirstOrDefault()}.");
    return response.Results[0];
}

static void PrintStatusNodeAttributeSummary(IReadOnlyList<StatusNodeAttributeObservation> observations)
{
    Console.WriteLine("\nSummary");
    Console.WriteLine("| Node | Attribute | Read Status | Before | After | After Registers | Cached |");
    Console.WriteLine("|---|---|---|---|---|---|---|");
    foreach (var observation in observations)
        Console.WriteLine($"| {observation.NodeName} | {observation.AttributeName} | {observation.AttributeStatus} | {observation.BeforeValue} | {observation.AfterValue} | [{observation.AfterRegisters}] | {observation.Cached} |");
}

static void PrintStatusRemapRead(FanucSystemVariableReadResult<bool> result)
{
    Console.WriteLine($"  Value={result.Value}; Registers=[{string.Join(", ", result.Registers)}]; StartAddress={result.StartAddress}");
    Console.WriteLine($"  Cached={!result.SetAsgIssued}; SetAsgStatus={result.SetAsgStatus}; SetAsgCommand='{result.SetAsgCommand}'");
}

static async Task PrintRobotStatusDiagnosticAsync(
    FanucOpcUaClient client,
    int cycle,
    CancellationToken cancellationToken)
{
    var status = await client.GetRobotStatusAsync(cancellationToken);
    var timestamp = DateTimeOffset.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    var alarms = status.Alarm is null ? "<unavailable>" : status.Alarm.Length == 0 ? "<none>" : string.Join(" | ", status.Alarm);
    Console.WriteLine($"[{cycle:D6}] {timestamp} RobotStatus: OperationState={FormatOptional(status.OperationStateRaw)}, ServoOn={FormatOptional(status.ServoOn)}, Mode={FormatOptional(status.Mode)}, ProgramSpeed={FormatOptional(status.ProgramSpeedPercent)}, Alarm={alarms}");
}

static async Task PrintProgramStatusDiagnosticAsync(
    FanucOpcUaClient client,
    int cycle,
    CancellationToken cancellationToken)
{
    var status = await client.ReadProgramStatusAsync(1, cancellationToken);
    var timestamp = DateTimeOffset.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    Console.WriteLine($"[{cycle:D6}] {timestamp} PRG[1]: Program='{status.ProgramName}', Line={status.LineNumber}, State={status.ExecutionStatus}, ParentProgram='{status.ParentProgramName}'");
}

static async Task PrintArcBooleanDiagnosticAsync(
    FanucOpcUaClient client,
    string variableName,
    int cycle,
    CancellationToken cancellationToken)
{
    var result = await client.ReadSystemVariableAsync<bool>(variableName, cancellationToken);
    var timestamp = DateTimeOffset.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    Console.WriteLine($"[{cycle:D6}] {timestamp} {variableName}");
    Console.WriteLine($"  Value={result.Value}; StartAddress={result.StartAddress}; Registers=[{string.Join(", ", result.Registers)}]");
    Console.WriteLine($"  SetAsgCommand='{result.SetAsgCommand}'; SetAsgStatus={result.SetAsgStatus}; Cached={!result.SetAsgIssued}");
}

static string FormatOptional<T>(T? value) where T : struct => value?.ToString() ?? "<unavailable>";

static async Task<int> TestPositionConfigurationWriteAsync(Uri endpoint, int number)
{
    var options = new FanucOpcUaConnectionOptions
    {
        EndpointUrl = endpoint,
        AllowInsecureConnection = true,
        DynamicAssignmentRangeVerified = true,
        EnablePositionRegisterWrites = true,
        WritablePositionRegisters = new HashSet<int> { number }
    };

    await using var client = new FanucOpcUaClient(options);
    FanucPosition? originalPosition = null;
    FanucPositionRegisterConfiguration? originalConfiguration = null;
    var writeAttempted = false;
    Exception? testFailure = null;
    try
    {
        Console.WriteLine($"SIMULATION-ONLY PR[] CONFIG write test started for PR[{number}].");
        await client.ConnectAsync();
        originalPosition = await client.ReadPositionRegisterAsync(number);
        originalConfiguration = await client.ReadPositionRegisterConfigurationAsync(number);
        Require(originalConfiguration.ValidCartesian == 1, "PR[] CONFIG test requires an initially valid Cartesian PR (VALIDC=1) so restoration can be verified.");
        Console.WriteLine($"Original PR[{number}]: {FormatPosition(originalPosition)}; {FormatConfiguration(originalConfiguration)}");

        writeAttempted = true;
        await client.WritePositionRegisterAsync(number, new FanucPosition(Configuration: new FanucPositionConfiguration("N", "D", "B", Turn4: 0, Turn5: 0, Turn6: 0)));
        var ndb = await client.ReadPositionRegisterConfigurationAsync(number);
        Console.WriteLine($"After NDB: {FormatConfiguration(ndb)}");
        Require(ndb.Flip == 0 && ndb.Up == 0 && ndb.Front == 0 && ndb.Turn4 == 0 && ndb.Turn5 == 0 && ndb.Turn6 == 0 && ndb.ValidCartesian == 1, "NDB CONFIG read-back differs.");

        await client.WritePositionRegisterAsync(number, new FanucPosition(Configuration: new FanucPositionConfiguration("F", "U", "T", Turn4: 4, Turn5: -5, Turn6: 6)));
        var fut = await client.ReadPositionRegisterConfigurationAsync(number);
        Console.WriteLine($"After FUT: {FormatConfiguration(fut)}");
        Require(fut.Flip == 1 && fut.Up == 1 && fut.Front == 1 && fut.Turn4 == 4 && fut.Turn5 == -5 && fut.Turn6 == 6 && fut.ValidCartesian == 1, "FUT CONFIG read-back differs.");

        await client.WritePositionRegisterAsync(number, new FanucPosition(Configuration: new FanucPositionConfiguration("N", "D", "B", Turn4: 0, Turn5: 0, Turn6: 0)));
        var ndbAgain = await client.ReadPositionRegisterConfigurationAsync(number);
        Console.WriteLine($"After NDB restore-step: {FormatConfiguration(ndbAgain)}");
        Require(ndbAgain.Flip == 0 && ndbAgain.Up == 0 && ndbAgain.Front == 0 && ndbAgain.Turn4 == 0 && ndbAgain.Turn5 == 0 && ndbAgain.Turn6 == 0, "Second NDB CONFIG read-back differs.");
        return 0;
    }
    catch (Exception exception)
    {
        testFailure = exception;
        Console.Error.WriteLine($"PR[] CONFIG write test failed: {exception.Message}");
        return 1;
    }
    finally
    {
        if (writeAttempted && originalPosition is not null && originalConfiguration is not null)
        {
            try
            {
                await client.WritePositionRegisterAsync(number, originalPosition with { Configuration = ToWritableConfiguration(originalConfiguration) });
                var restoredPosition = await client.ReadPositionRegisterAsync(number);
                var restoredConfiguration = await client.ReadPositionRegisterConfigurationAsync(number);
                Console.WriteLine($"After restore: {FormatPosition(restoredPosition)}; {FormatConfiguration(restoredConfiguration)}");
                Require(restoredPosition == originalPosition && SameConfiguration(restoredConfiguration, originalConfiguration), "PR[] Cartesian/CONFIG restore verification failed.");
                Console.WriteLine("Original PR[] Cartesian and CONFIG values restored.");
            }
            catch (Exception restoreException)
            {
                Console.Error.WriteLine($"CRITICAL: original PR[] CONFIG restore failed: {restoreException.Message}");
                if (testFailure is null) throw;
            }
        }
        if (client.IsConnected) await client.DisconnectAsync();
    }
}

static bool IsLoopback(Uri endpoint) =>
    endpoint.IsLoopback || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);

static int? ParsePositionRegisterNumber(IEnumerable<string> arguments)
{
    var option = arguments.FirstOrDefault(argument => argument.StartsWith("--position-register=", StringComparison.OrdinalIgnoreCase));
    if (option is null) return null;
    var value = option["--position-register=".Length..];
    if (!int.TryParse(value, out var number) || number < 1)
        throw new ArgumentException("--position-register must be a positive integer.");
    return number;
}

static int? ParseRegisterNumber(IEnumerable<string> arguments)
{
    const string prefix = "--register-number=";
    var option = arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (option is null) return null;
    var value = option[prefix.Length..];
    if (!int.TryParse(value, out var number) || number < 1)
        throw new ArgumentException("--register-number must be a positive integer.");
    return number;
}

static int? ParseMixedMonitoringCycles(IEnumerable<string> arguments)
{
    const string prefix = "--mixed-monitoring-cycles=";
    var option = arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (option is null) return null;
    var value = option[prefix.Length..];
    if (!int.TryParse(value, out var cycles) || cycles < 1)
        throw new ArgumentException("--mixed-monitoring-cycles must be a positive integer.");
    return cycles;
}

static int? ParseTaskNumber(IEnumerable<string> arguments)
{
    const string prefix = "--task-number=";
    var option = arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (option is null) return null;
    var value = option[prefix.Length..];
    if (!int.TryParse(value, out var number) || number < 1)
        throw new ArgumentException("--task-number must be a positive integer.");
    return number;
}

static int? ParseDigitalSignalNumber(IEnumerable<string> arguments, string optionName)
{
    var prefix = $"{optionName}=";
    var option = arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (option is null) return null;
    var value = option[prefix.Length..];
    if (!int.TryParse(value, out var number) || number is < 1 or > 10000)
        throw new ArgumentException($"{optionName} must be an integer in the range 1 through 10000.");
    return number;
}

static int? ParseGroupSignalNumber(IEnumerable<string> arguments, string optionName)
{
    var prefix = $"{optionName}=";
    var option = arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (option is null) return null;
    var value = option[prefix.Length..];
    if (!int.TryParse(value, out var number) || number is < 1 or > 1000)
        throw new ArgumentException($"{optionName} must be an integer in the range 1 through 1000.");
    return number;
}

static IReadOnlyList<string> ParseStatusNodeIds(IEnumerable<string> arguments)
{
    const string prefix = "--status-node-id=";
    var values = arguments
        .Where(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        .Select(argument => argument[prefix.Length..])
        .ToArray();

    if (values.Any(string.IsNullOrWhiteSpace))
        throw new ArgumentException("--status-node-id must include a non-empty OPC UA NodeId.");
    return values;
}

static IReadOnlyList<StatusNodeTarget> ParseNamedStatusNodeIds(IEnumerable<string> arguments)
{
    var options = new (string Name, string Prefix)[]
    {
        ("OperationState", "--operation-state-node-id="),
        ("ServoState", "--servo-state-node-id="),
        ("ModeState", "--mode-state-node-id="),
        ("ProgramSpeed", "--program-speed-node-id="),
        ("Alarm", "--alarm-node-id=")
    };

    var targets = new List<StatusNodeTarget>();
    foreach (var option in options)
    {
        var argument = arguments.FirstOrDefault(value => value.StartsWith(option.Prefix, StringComparison.OrdinalIgnoreCase));
        if (argument is null) continue;
        var nodeIdText = argument[option.Prefix.Length..];
        if (string.IsNullOrWhiteSpace(nodeIdText))
            throw new ArgumentException($"{option.Prefix[..^1]} must include a non-empty OPC UA NodeId.");
        targets.Add(new StatusNodeTarget(option.Name, NodeId.Parse(nodeIdText)));
    }

    return targets;
}

static string FormatPosition(FanucPosition position) =>
    $"X={position.X:R}, Y={position.Y:R}, Z={position.Z:R}, W={position.W:R}, P={position.P:R}, R={position.R:R}";

static string FormatConfiguration(FanucPositionRegisterConfiguration configuration) =>
    $"FLIP={configuration.Flip}, LEFT={configuration.Left}, UP={configuration.Up}, FRONT={configuration.Front}, TURN4={configuration.Turn4}, TURN5={configuration.Turn5}, TURN6={configuration.Turn6}, VALIDC={configuration.ValidCartesian}";

static FanucPositionConfiguration ToWritableConfiguration(FanucPositionRegisterConfiguration configuration) => new(
    configuration.Flip switch { 0 => "N", 1 => "F", _ => throw new InvalidOperationException($"Cannot restore unsupported FLIP value {configuration.Flip}.") },
    configuration.Up switch { 0 => "D", 1 => "U", _ => throw new InvalidOperationException($"Cannot restore unsupported UP value {configuration.Up}.") },
    configuration.Front switch { 0 => "B", 1 => "T", _ => throw new InvalidOperationException($"Cannot restore unsupported FRONT value {configuration.Front}.") },
    configuration.Left,
    configuration.Turn4,
    configuration.Turn5,
    configuration.Turn6);

static bool SameConfiguration(FanucPositionRegisterConfiguration first, FanucPositionRegisterConfiguration second) =>
    first.Flip == second.Flip && first.Left == second.Left && first.Up == second.Up && first.Front == second.Front &&
    first.Turn4 == second.Turn4 && first.Turn5 == second.Turn5 && first.Turn6 == second.Turn6 && first.ValidCartesian == second.ValidCartesian;

static void PrintReadResult<T>(string step, FanucSystemVariableReadResult<T> result)
{
    Console.WriteLine($"{step}: value={result.Value}, address={result.StartAddress}, SETASG issued={result.SetAsgIssued}, SETASG status={result.SetAsgStatus}");
    Console.WriteLine($"  SETASG='{result.SetAsgCommand}', raw=[{string.Join(", ", result.Registers)}]");
}

static void PrintWriteResult<T>(string step, FanucSystemVariableWriteResult<T> result)
{
    Console.WriteLine($"{step}: value={result.Value}, address={result.StartAddress}, SETASG issued={result.SetAsgIssued}, SETASG status={result.SetAsgStatus}, write status={result.WriteStatus}");
    Console.WriteLine($"  SETASG='{result.SetAsgCommand}', written=[{string.Join(", ", result.WrittenRegisters)}], read-back=[{string.Join(", ", result.ReadBackRegisters)}]");
}

static async Task SameVariableAsync(FanucOpcUaClient client)
{
    Console.WriteLine("\n[1/4] 20 concurrent reads of $MCR.$GENOVERRIDE");
    var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.ReadSystemVariableAsync<int>(Override)));
    foreach (var result in results) CheckInt(result, Override);
    var addresses = results.Select(result => result.StartAddress).Distinct().Order().ToArray();
    var commandCount = results.Count(result => result.SetAsgIssued);
    Require(addresses.Length == 1, "Same variable was allocated multiple addresses.");
    Require(commandCount == 1, $"Expected one SETASG, observed {commandCount}.");
    Console.WriteLine($"  address={addresses[0]}, SETASG count={commandCount}, raw=[{string.Join(", ", results[0].Registers)}], value={results[0].Value}%");
}

static async Task MixedTypesAsync(FanucOpcUaClient client)
{
    Console.WriteLine("\n[2/4] 20 concurrent mixed reads (int/float/bool)");
    await client.ClearDynamicAssignmentsAsync();
    var observations = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i => (i % 3) switch
    {
        0 => Observe(Override, await ReadIntAsync(client)),
        1 => Observe(Amps, await ReadFloatAsync(client)),
        _ => Observe(ArcOn, await ReadBoolAsync(client))
    }));
    var groups = observations.GroupBy(item => item.Name, StringComparer.Ordinal)
        .Select(group => new { Name = group.Key, Addresses = group.Select(item => item.Address).Distinct().ToArray(), Commands = group.Count(item => item.SetAsgIssued), Sample = group.First() })
        .OrderBy(group => group.Name, StringComparer.Ordinal).ToArray();
    Require(groups.Length == 3, "All three variables were not read.");
    Require(groups.All(group => group.Addresses.Length == 1), "A variable was allocated multiple addresses.");
    Require(groups.Select(group => group.Addresses[0]).Distinct().Count() == 3, "Dynamic assignment addresses overlap.");
    Require(groups.Sum(group => group.Commands) == 3, "Expected one SETASG for each variable.");
    foreach (var group in groups)
        Console.WriteLine($"  {group.Name}: address={group.Addresses[0]}, SETASG count={group.Commands}, raw=[{group.Sample.Raw}], value={group.Sample.Value}");
}

static async Task CapacityAsync(Uri endpoint, bool allowInsecure)
{
    Console.WriteLine("\n[3/4] Exhaustion in range 10001-10002");
    await using var client = new FanucOpcUaClient(Options(endpoint, allowInsecure, 10001, 10002));
    await client.ConnectAsync();
    try
    {
        var first = await client.ReadSystemVariableAsync<int>(Override);
        CheckInt(first, Override);
        try
        {
            await client.ReadSystemVariableAsync<float>(Amps);
            throw new InvalidOperationException("Expected allocation exhaustion did not occur.");
        }
        catch (FanucOpcUaConnectionException ex) when (ex.Message.Contains("No contiguous range", StringComparison.Ordinal))
        {
            Console.WriteLine($"  first address={first.StartAddress}, SETASG count={Convert.ToInt32(first.SetAsgIssued)}; controlled error='{ex.Message}'");
        }
    }
    finally
    {
        await client.DisconnectAsync();
    }
}

static async Task ReconnectAsync(FanucOpcUaClient client)
{
    Console.WriteLine("\n[4/4] Disconnect/reconnect cache reset");
    await client.DisconnectAsync();
    Require(!client.IsConnected, "Client remained connected after DisconnectAsync.");
    await client.ConnectAsync();
    var result = await client.ReadSystemVariableAsync<int>(Override);
    CheckInt(result, Override);
    Require(result.SetAsgIssued, "First mapping after reconnect did not issue SETASG.");
    Console.WriteLine($"  address={result.StartAddress}, SETASG count={Convert.ToInt32(result.SetAsgIssued)}, raw=[{string.Join(", ", result.Registers)}], value={result.Value}%");
}

static async Task<FanucSystemVariableReadResult<int>> ReadIntAsync(FanucOpcUaClient client)
{
    var result = await client.ReadSystemVariableAsync<int>(Override); CheckInt(result, Override); return result;
}

static async Task<FanucSystemVariableReadResult<float>> ReadFloatAsync(FanucOpcUaClient client)
{
    var result = await client.ReadSystemVariableAsync<float>(Amps); CheckFloat(result, Amps); return result;
}

static async Task<FanucSystemVariableReadResult<bool>> ReadBoolAsync(FanucOpcUaClient client)
{
    var result = await client.ReadSystemVariableAsync<bool>(ArcOn); CheckBool(result, ArcOn); return result;
}

static Observation Observe<T>(string name, FanucSystemVariableReadResult<T> result) => new(name, result.StartAddress, result.SetAsgIssued, string.Join(", ", result.Registers), Convert.ToString(result.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>");

static void CheckInt(FanucSystemVariableReadResult<int> result, string name)
{
    Require(result.Registers.Count == 2, $"{name} did not return two registers.");
    Require(result.Value == FanucOpcUaClient.CombineInt16ToInt32(result.Registers[0], result.Registers[1]), $"{name} integer conversion differs from raw registers.");
}

static void CheckFloat(FanucSystemVariableReadResult<float> result, string name)
{
    Require(result.Registers.Count == 2, $"{name} did not return two registers.");
    Require(BitConverter.SingleToInt32Bits(result.Value) == FanucOpcUaClient.CombineInt16ToInt32(result.Registers[0], result.Registers[1]), $"{name} Float32 conversion differs from raw registers.");
}

static void CheckBool(FanucSystemVariableReadResult<bool> result, string name)
{
    Require(result.Registers.Count == 2, $"{name} did not return two registers.");
    var raw = FanucOpcUaClient.CombineInt16ToInt32(result.Registers[0], result.Registers[1]);
    Require(raw is 0 or 1, $"{name} has invalid BOOLEAN register value {raw}.");
    Require(result.Value == (raw == 1), $"{name} BOOLEAN conversion differs from raw registers.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record Observation(string Name, int Address, bool SetAsgIssued, string Raw, string Value);
internal sealed record StatusNodeTarget(string Name, NodeId NodeId);
internal sealed record StatusNodeAttributeObservation(string NodeName, string AttributeName, string AttributeStatus, bool BeforeValue, bool AfterValue, string AfterRegisters, bool Cached);
