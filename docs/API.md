# API Reference

Detailed behavior extracted from the original v1.0.2 README. This is a usage guide rather than an exhaustive generated API reference.

See [Security](SECURITY.md) before enabling writes and [Testing](TESTING.md) for console commands.

## System variable reads

`ReadSystemVariableAsync<T>(variableName)` reads FANUC INTEGER (`int`), REAL (`float`), BOOLEAN (`bool`), and STRING (`string`) values through connection-scoped dynamic Holding Register assignments. Mappings are cached per connection. For STRING, `ReadSystemVariableAsync<string>` uses 40 registers with `MULTIPLY=1`; each register contains two characters (low byte first). A NUL terminator is required; the current implementation uses Latin-1 as a one-to-one byte mapping because the FANUC manual does not specify a code page. STRING writes are not supported. A read-only test against `$RBTIF.SERIAL_NUM` was reported in ROBOGUIDE for version 1.0.2.

## Digital I/O reads

`ReadDigitalInputAsync(int number)` and `ReadDigitalOutputAsync(int number)` create a connection-scoped `SETASG <address> 1 DI[n]` or `DO[n]` assignment. Per Additional Option Manual §55.3.10 this uses the default `MULTIPLY=1` representation: one Holding Register per signal, with `0=OFF` and `1=ON`. Any other register value is rejected as invalid instead of being silently interpreted. Signal numbers are validated in the documented 1–10000 range. These APIs do not write I/O, and the assignment cache/allocation lock prevents duplicate `SETASG` commands for the same signal within one connection. DI[] has no write API.

`WriteDigitalOutputAsync(int number, bool value)` writes only an explicitly allow-listed DO[] through the same `MULTIPLY=1` mapping (`ON=1`, `OFF=0`), checks OPC UA `AccessLevel` and `UserAccessLevel`, then requires a DO[] read-back match. Writes are disabled by default and require both `EnableDigitalOutputWrites = true` and the output number in `WritableDigitalOutputs`. The write/read-back sequence is serialized per client but is not assumed to be controller-atomic.

## Extended I/O reads

`ReadRobotInputAsync` and `ReadRobotOutputAsync` read `RI[]` and `RO[]` as Boolean values through single-register `MULTIPLY=1` dynamic assignments. Their documented signal range is 1–10000. `ReadGroupInputAsync` and `ReadGroupOutputAsync` read `GI[]` and `GO[]` as `ushort` in the documented 1–1000 range. The OPC UA `HoldingRegisters` node is `Int16[]`, so the library preserves the raw 16-bit pattern and exposes it as `UInt16`, matching the manual’s `InputRegisters` type. Per §55.3.10, GI/GO occupy one Holding Register even when `MULTIPLY=0`; this implementation uses a one-register dynamic assignment. No RI/RO/GI/GO write APIs are added in this stage.

## Robot-status discovery

`DiscoverRobotStatusNodesAsync` browses the OPC UA address space beginning at `Objects` and resolves the optional §55.5 `RobotInformation` and `MotionDeviceSystem` nodes without assuming FANUC NodeIds. It reports `OperationState`, `ServoState`, `ModeState`, `ProgramSpeed`, `Alarm`, and the `SafetyStates` values `OperationalMode`, `EmergencyStop`, and `ProtectiveStop`. Missing nodes are returned explicitly, which is expected on controller software that predates the documented RobotInformation/MotionDeviceSystem support levels.

`GetRobotStatusAsync` turns a successfully read `OperationState` into the §55.5 E-STOP, Abort, PROGRUN, PAUSED, HELD, FAULT, TPENBL, BATALM, and BUSY flags. All status fields are nullable: `null` means the optional node was missing, its OPC UA value status was not Good, or its value could not be interpreted safely. `Alarm` is exposed as `string[]?`; a Good empty/null alarm is an empty array, while an unavailable or unsupported alarm value is `null`. ModeState's raw value is retained because §55.5 does not document a numeric mode-value mapping.

## TP program execution status

`ReadProgramStatusAsync(taskNumber)` creates a connection-scoped, read-only `PRG[n]` assignment of 18 Holding Registers as specified by Additional Option Manual §55.3.7. It exposes the active program name, line number, `Aborted`/`Paused`/`Running` execution state, and parent program name. The mapping explicitly uses `MULTIPLY=1`; per §55.3.1 the first character is the low byte and the second is the high byte of each Holding Register. A task number must be positive; the controller determines which task is represented by `PRG[1]` in a multi-task system.

## System-variable writes

`WriteSystemVariableAsync<T>(variableName, value)` supports `int`, `float`, and `bool` through the same connection-scoped dynamic `SETASG` cache used for reads. It writes the two required Int16 words in one indexed OPC UA write, then requires an exact raw-register read-back match. FANUC does not document multi-register writes as atomic, so the API makes no atomicity guarantee.

Writes are disabled by default and require both an explicit opt-in and an exact, case-sensitive allow-list. `$RBTIF.*` special variables are always rejected. The client checks `AccessLevel` and `UserAccessLevel` on `HoldingRegisters` immediately before writing. A Good OPC UA status alone is insufficient because FANUC can ignore writes to an unassigned or read-only mapping; read-back verification is mandatory.

```csharp
var options = new FanucOpcUaConnectionOptions
{
    EndpointUrl = new Uri("opc.tcp://robot.example:4880/FANUC/NanoUaServer"),
    // Set only after reviewing this controller's persistent $SNPX_ASG assignments.
    DynamicAssignmentRangeVerified = true,
    EnableSystemVariableWrites = true,
    WritableSystemVariables = new HashSet<string>(StringComparer.Ordinal)
    {
        "$MCR.$GENOVERRIDE"
    }
};

await using var client = new FanucOpcUaClient(options);
await client.ConnectAsync();
// Only use a controller-owner-approved value and a safe robot state.
var result = await client.WriteSystemVariableAsync("$MCR.$GENOVERRIDE", 25);
```

The example is intentionally not exercised by the console application. On FANUC controllers, even system variables protected on the teach pendant can be changed through Holding Registers; use a controller-specific allow-list and an approved safety procedure.

## Numeric R[] registers

`ReadRegisterAsync(int registerNumber)` and `WriteRegisterAsync(int registerNumber, double value)` use a connection-scoped `SETASG <address> 2 R[n] 0` mapping. Per FANUC manual section 55.3.2, `MULTIPLY=0` represents each R[] value as a two-word IEEE-754 Float32, low word first. The `double` write API accepts only finite values exactly representable as Float32; it rejects lossy conversions rather than silently rounding. `10.25` and `-35.75` are representable exactly.

R[] writes are disabled by default and require `EnableRegisterWrites = true` plus the exact register number in `WritableRegisters`. They use the same HoldingRegisters access-right check and raw read-back verification as system-variable writes. They do not promise atomic multi-register writes.

## Position PR[] registers

`ReadPositionRegisterAsync(int)` reads Cartesian `X/Y/Z/W/P/R` using the documented `SETASG <address> 12 PR[n]@1.12 0` mapping. Each member uses two low-word-first IEEE-754 Float32 Holding Registers. `WritePositionRegisterAsync(int, FanucPosition)` requires an explicit `EnablePositionRegisterWrites` opt-in and `WritablePositionRegisters` allow-list entry, checks write access, and validates raw read-back.

A `FanucPosition` with all six Cartesian values is a full Cartesian update. A position with only some Cartesian values is a partial update: the library first reads the current Cartesian values, merges the specified members, then writes all six. This is read-modify-write, not an atomic controller operation.

CONFIG writes use the documented `PR[n]@19.8` layout: N/F maps to `FLIP` 0/1, D/U to `UP` 0/1, B/T to `FRONT` 0/1, and `Configuration.Turn4`-`Turn6` map directly to the corresponding signed Int16 fields. `LEFT` is preserved when omitted. A complete Cartesian write without CONFIG applies `N D B, 0 0 0` while preserving LEFT. `VALIDC` is written as 1 to establish Cartesian representation. UF and UT remain unsupported because the manual states PR UF/UT cannot be changed; legacy `TurnJ1`-`TurnJ3` are also rejected in favor of documented TURN4-6.

The first connection creates the client certificate under `%LOCALAPPDATA%\\\Robentex\\\FanucOpcUa\\\pki` by default. Its `own`, `trusted`, `issuers`, and `rejected` stores are configured explicitly. For a secured FANUC server, add its certificate (or issuing CA) to the appropriate trust store through your organization’s certificate approval process; this library never auto-accepts an unknown server certificate.
