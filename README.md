# Robentex.Fanuc.OpcUa

.NET 9 FANUC OPC UA client library built on the official OPC Foundation .NET Standard stack.

## Package and compatibility

`Robentex.Fanuc.OpcUa` version 1.0.0 targets `net9.0`. It is intended for applications that
communicate with FANUC controllers implementing the HMI Device Communication OPC UA option.
Controller firmware, enabled options, endpoint URLs, security policies, and NodeIds are
controller-specific and must be validated before deployment.

Install the package in a .NET 9 application:

```powershell
dotnet add package Robentex.Fanuc.OpcUa --version 1.0.0
```

The package includes the library assembly, XML API documentation, this README, and the MIT license.
It does not include the console application, unit tests, FANUC documentation, or OPC UA PKI material.

## Safety and security

The library is secure by default: a security-free OPC UA endpoint requires explicit opt-in, and an
unknown server certificate is never automatically trusted. Dynamic Holding Register assignment is
disabled until the caller confirms that its configured address range does not conflict with persistent
`$SNPX_ASG` assignments or other clients.

All robot writes are disabled by default. Enabling a write category requires both its opt-in setting and
the corresponding explicit allow-list. A successful OPC UA write is followed by a read-back check, but
FANUC does not document multi-register writes as controller-atomic. Validate every deployment with the
controller owner and applicable machine-safety procedure.

Before building, install these official packages in `src/Robentex.Fanuc.OpcUa`:

```powershell
dotnet add package OPCFoundation.NetStandard.Opc.Ua.Client --version 1.5.378.176
dotnet add package OPCFoundation.NetStandard.Opc.Ua.Configuration --version 1.5.378.176
```

They are already declared as `PackageReference` items so only one restore is needed after
your package source is available; the commands above are supplied as the requested manual
installation alternative.

The offline-capable unit-test project at `tests/Robentex.Fanuc.OpcUa.Tests` additionally declares
`Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.3, and `xunit.runner.visualstudio` 2.8.2. These packages
are not installed automatically by this repository.

The library provides endpoint discovery, secure-by-default session creation, connection
state observation, and synchronous/asynchronous disposal. It also supports connection-scoped
`CLRASG`/`SETASG` dynamic assignments and read-only `$MCR.$GENOVERRIDE` validation through
Holding Registers. It can also read INTEGER (`int`), REAL (`float`), and BOOLEAN (`bool`)
system variables with `ReadSystemVariableAsync<T>`, R[] and Cartesian PR[] values, plus DI[]/DO[] signals.

## Digital I/O reads

`ReadDigitalInputAsync(int number)` and `ReadDigitalOutputAsync(int number)` create a connection-scoped
`SETASG <address> 1 DI[n]` or `DO[n]` assignment. Per Additional Option Manual §55.3.10 this uses the
default `MULTIPLY=1` representation: one Holding Register per signal, with `0=OFF` and `1=ON`. Any other
register value is rejected as invalid instead of being silently interpreted. Signal numbers are validated
in the documented 1–10000 range. These APIs do not write I/O, and the assignment cache/allocation lock
prevents duplicate `SETASG` commands for the same signal within one connection. DI[] has no write API.

`WriteDigitalOutputAsync(int number, bool value)` writes only an explicitly allow-listed DO[] through the
same `MULTIPLY=1` mapping (`ON=1`, `OFF=0`), checks OPC UA `AccessLevel` and `UserAccessLevel`, then
requires a DO[] read-back match. Writes are disabled by default and require both
`EnableDigitalOutputWrites = true` and the output number in `WritableDigitalOutputs`. The write/read-back
sequence is serialized per client but is not assumed to be controller-atomic.

## Extended I/O reads

`ReadRobotInputAsync` and `ReadRobotOutputAsync` read `RI[]` and `RO[]` as Boolean values through
single-register `MULTIPLY=1` dynamic assignments. Their documented signal range is 1–10000.
`ReadGroupInputAsync` and `ReadGroupOutputAsync` read `GI[]` and `GO[]` as `ushort` in the documented
1–1000 range. The OPC UA `HoldingRegisters` node is `Int16[]`, so the library preserves the raw 16-bit
pattern and exposes it as `UInt16`, matching the manual’s `InputRegisters` type. Per §55.3.10, GI/GO
occupy one Holding Register even when `MULTIPLY=0`; this implementation uses a one-register dynamic
assignment. No RI/RO/GI/GO write APIs are added in this stage.

## Robot-status discovery

`DiscoverRobotStatusNodesAsync` browses the OPC UA address space beginning at `Objects` and resolves
the optional §55.5 `RobotInformation` and `MotionDeviceSystem` nodes without assuming FANUC NodeIds.
It reports `OperationState`, `ServoState`, `ModeState`, `ProgramSpeed`, `Alarm`, and the `SafetyStates`
values `OperationalMode`, `EmergencyStop`, and `ProtectiveStop`. Missing nodes are returned explicitly,
which is expected on controller software that predates the documented RobotInformation/MotionDeviceSystem
support levels.

`GetRobotStatusAsync` turns a successfully read `OperationState` into the §55.5 E-STOP, Abort,
PROGRUN, PAUSED, HELD, FAULT, TPENBL, BATALM, and BUSY flags. All status fields are nullable: `null`
means the optional node was missing, its OPC UA value status was not Good, or its value could not be
interpreted safely. `Alarm` is exposed as `string[]?`; a Good empty/null alarm is an empty array, while an
unavailable or unsupported alarm value is `null`. ModeState's raw value is retained because §55.5 does not
document a numeric mode-value mapping.

## TP program execution status

`ReadProgramStatusAsync(taskNumber)` creates a connection-scoped, read-only `PRG[n]` assignment of
18 Holding Registers as specified by Additional Option Manual §55.3.7. It exposes the active program name,
line number, `Aborted`/`Paused`/`Running` execution state, and parent program name. The mapping explicitly
uses `$MULTIPLY=1`; per §55.3.1 the first character is the low byte and the second is the high byte of each
Holding Register. A task number must be positive; the controller determines which task is represented by
`PRG[1]` in a multi-task system.

## System-variable writes

`WriteSystemVariableAsync<T>(variableName, value)` supports `int`, `float`, and `bool` through the same
connection-scoped dynamic `SETASG` cache used for reads. It writes the two required Int16 words in one
indexed OPC UA write, then requires an exact raw-register read-back match. FANUC does not document
multi-register writes as atomic, so the API makes no atomicity guarantee.

Writes are disabled by default and require both an explicit opt-in and an exact, case-sensitive allow-list.
`$RBTIF.*` special variables are always rejected. The client checks `AccessLevel` and `UserAccessLevel`
on `HoldingRegisters` immediately before writing. A Good OPC UA status alone is insufficient because
FANUC can ignore writes to an unassigned or read-only mapping; read-back verification is mandatory.

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

The example is intentionally not exercised by the console application. On FANUC controllers, even
system variables protected on the teach pendant can be changed through Holding Registers; use a
controller-specific allow-list and an approved safety procedure.

## Numeric R[] registers

`ReadRegisterAsync(int registerNumber)` and `WriteRegisterAsync(int registerNumber, double value)`
use a connection-scoped `SETASG <address> 2 R[n] 0` mapping. Per FANUC manual section 55.3.2,
`MULTIPLY=0` represents each R[] value as a two-word IEEE-754 Float32, low word first. The `double`
write API accepts only finite values exactly representable as Float32; it rejects lossy conversions rather
than silently rounding. `10.25` and `-35.75` are representable exactly.

R[] writes are disabled by default and require `EnableRegisterWrites = true` plus the exact register
number in `WritableRegisters`. They use the same HoldingRegisters access-right check and raw read-back
verification as system-variable writes. They do not promise atomic multi-register writes.

## Position PR[] registers

`ReadPositionRegisterAsync(int)` reads Cartesian `X/Y/Z/W/P/R` using the documented
`SETASG <address> 12 PR[n]@1.12 0` mapping. Each member uses two low-word-first IEEE-754 Float32
Holding Registers. `WritePositionRegisterAsync(int, FanucPosition)` requires an explicit
`EnablePositionRegisterWrites` opt-in and `WritablePositionRegisters` allow-list entry, checks write
access, and validates raw read-back.

A `FanucPosition` with all six Cartesian values is a full Cartesian update. A position with only some
Cartesian values is a partial update: the library first reads the current Cartesian values, merges the
specified members, then writes all six. This is read-modify-write, not an atomic controller operation.

CONFIG writes use the documented `PR[n]@19.8` layout: N/F maps to `FLIP` 0/1, D/U to `UP` 0/1, B/T
to `FRONT` 0/1, and `Configuration.Turn4`-`Turn6` map directly to the corresponding signed Int16 fields.
`LEFT` is preserved when omitted. A complete Cartesian write without CONFIG applies `N D B, 0 0 0` while
preserving LEFT. `VALIDC` is written as 1 to establish Cartesian representation. UF and UT remain
unsupported because the manual states PR UF/UT cannot be changed; legacy `TurnJ1`-`TurnJ3` are also
rejected in favor of documented TURN4-6.

The first connection creates the client certificate under `%LOCALAPPDATA%\\Robentex\\FanucOpcUa\\pki`
by default. Its `own`, `trusted`, `issuers`, and `rejected` stores are configured explicitly.
For a secured FANUC server, add its certificate (or issuing CA) to the appropriate trust store
through your organization’s certificate approval process; this library never auto-accepts an
unknown server certificate.

## Test against ROBOGUIDE

Start the ROBOGUIDE OPC UA server, then run:

```powershell
dotnet run --project tests/Robentex.Fanuc.OpcUa.Console -- --allow-insecure
```

The console test issues one connection-scoped `CLRASG`, maps `$MCR.$GENOVERRIDE`
with `SETASG 10001 2 $MCR.$GENOVERRIDE`, then reads Holding Register addresses
10001-10002 (OPC UA array indices 10000-10001). It never changes persistent
`$SNPX_ASG` data.

It additionally demonstrates typed system-variable reads at non-overlapping dynamic addresses:
`$MCR.$GENOVERRIDE` (`int`, 10001), `$AWEPOR[1].$AMPS_FDBK` (`float`, 10003), and
`$AWEPOR[1].$ARC_ON` (`bool`, 10005). For each it prints the SETASG command/status,
the two raw Int16 words, and the converted value.

`ReadSystemVariableAsync<T>(variableName)` automatically reserves a collision-free range from
`DynamicAssignmentStartAddress` to `DynamicAssignmentEndAddress` (defaults: 10001-16384).
The allocation/cache is connection-scoped and protected for concurrent reads. The first mapping on
a connection sends `CLRASG` once; repeated reads reuse their SETASG mapping. A reconnect invalidates
the local cache and rebuilds the needed mapping. Automatic allocation is disabled until
`DynamicAssignmentRangeVerified = true` is explicitly set after reviewing this controller's persistent
`$SNPX_ASG` assignments and other client reservations. The default 10001-16384 range only avoids the
observed ROBOGUIDE default 1-10000 mapping; it is not a portable safety guarantee. Use
`ReservedHoldingRegisterRanges` for known occupied portions; both automatic and manual dynamic
assignments are rejected when they overlap a reserved range.

The default endpoint is `opc.tcp://127.0.0.1:4880`. A different endpoint is passed as
the first argument. The test server's security-free endpoint is never selected unless
`--allow-insecure` is present.

```powershell
dotnet run --project tests/Robentex.Fanuc.OpcUa.Console -- opc.tcp://192.0.2.10:4880 --allow-insecure
```

## Automated unit tests

The unit tests do not connect to a robot. They cover dynamic Holding Register allocation and collision
protection, 1-based address/0-based IndexRange conversion, Int16/Float32 conversion, target-scoped write
locks and cancellation cleanup, Good/Bad/Uncertain StatusCode handling, and session-close mapping-cache
cleanup.

```powershell
dotnet test tests/Robentex.Fanuc.OpcUa.Tests/Robentex.Fanuc.OpcUa.Tests.csproj --no-restore -m:1
```

To verify the default write-deny policy without connecting to, mapping, or writing a robot:

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --verify-write-policy
```

### Explicit ROBOGUIDE system-write test

The console never writes by default. The write test is additionally restricted to a loopback endpoint and
requires all three flags below, preventing use against a network-connected physical controller. It reads
`$MCR.$GENOVERRIDE`, writes `20`, verifies the result and restores the original value in `finally`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-system-write --simulation --allow-insecure
```

Use this only with a running local ROBOGUIDE simulation. The console prints the SETASG, OPC UA write,
and read-back status codes for every step, including restoration.

### Explicit ROBOGUIDE R[] write test

This simulation-only test uses `R[100]`, verifies `10.25` and `-35.75`, then restores the original
value in `finally`. It has the same loopback, insecure-test-endpoint, and explicit simulation requirements:

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-register-write --simulation --allow-insecure
```

### Explicit ROBOGUIDE PR[] write test

The simulation-only PR[] test performs one full Cartesian update (`10.25`, `-35.75`, ...), one partial
X-only update, verifies each read-back, and restores the original Cartesian values in `finally`. The
position register defaults to `PR[100]` and can be changed using `--position-register=<number>`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-position-write --simulation --allow-insecure --position-register=100
```

### Explicit ROBOGUIDE PR[] CONFIG write test

This simulation-only test verifies `NDB -> FUT -> NDB`, TURN4-6 and read-back, then restores the
initial Cartesian and CONFIG values. It requires the initial PR to be valid Cartesian (`VALIDC=1`).

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-position-config-write --simulation --allow-insecure --position-register=100
```

### Read-only DI/DO test

This read-only test maps and reads `DI[1]` and `DO[1]` by default; it does not write either signal.
Select different signal numbers with `--di-number` and `--do-number`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-digital-io-read --allow-insecure --di-number=1 --do-number=1
```

### Read-only extended I/O test

This test reads `RI[1]`, `RO[1]`, `GI[1]`, and `GO[1]` by default. The group values are displayed in
decimal and hexadecimal. All signal numbers are configurable.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-extended-io-read --allow-insecure --ri-number=1 --ro-number=1 --gi-number=1 --go-number=1
```

### Read-only robot-status discovery

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-robot-status-discovery --allow-insecure
```

### Read-only robot-status snapshot

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-robot-status --allow-insecure
```

### Read-only TP program-status test

`PRG[1]` is used by default. Pass `--task-number=<n>` for another active task.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-program-status --allow-insecure --task-number=1
```

### Read-only R[] and PR[] tests

These commands perform only dynamic mapping and reads; they do not change the selected R[] or PR[].
`R[100]` and `PR[100]` are the defaults and can be changed independently.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-register-read --allow-insecure --register-number=100
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-position-read --allow-insecure --position-register=100
```

### Explicit ROBOGUIDE DO[] write test

This test is disabled unless all safety flags are explicit. It records the original `DO[1]`, writes and
verifies ON then OFF, and restores the original value in `finally`. It accepts `--do-number=<n>`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-digital-output-write --simulation --allow-insecure --do-number=1
```

## License

This library is licensed under the [MIT License](LICENSE). FANUC documentation and controller software
are not distributed with this package and remain subject to their respective license terms.
