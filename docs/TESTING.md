# ROBOGUIDE Testing and Diagnostics

These commands belong to the **source repository** and are not included in the NuGet package. A local simulation is required for `--simulation` tests.

Source: original v1.0.2 README; commands are preserved as documentation, not independently re-tested.

## Test against ROBOGUIDE

Start the ROBOGUIDE OPC UA server, then run:

```powershell
dotnet run --project tests/Robentex.Fanuc.OpcUa.Console -- --allow-insecure
```

The console test issues one connection-scoped `CLRASG`, maps `$MCR.$GENOVERRIDE` with `SETASG 10001 2 $MCR.$GENOVERRIDE`, then reads Holding Register addresses 10001-10002 (OPC UA array indices 10000-10001). It never changes persistent `$SNPX_ASG` data.

It additionally demonstrates typed system-variable reads at non-overlapping dynamic addresses: `$MCR.$GENOVERRIDE` (`int`, 10001), `$AWEPOR[1].$AMPS_FDBK` (`float`, 10003), and `$AWEPOR[1].$ARC_ON` (`bool`, 10005). For each it prints the SETASG command/status, the two raw Int16 words, and the converted value.

`ReadSystemVariableAsync<T>(variableName)` automatically reserves a collision-free range from `DynamicAssignmentStartAddress` to `DynamicAssignmentEndAddress` (defaults: 10001-16384). The allocation/cache is connection-scoped and protected for concurrent reads. The first mapping on a connection sends `CLRASG` once; repeated reads reuse their SETASG mapping. A reconnect invalidates the local cache and rebuilds the needed mapping. Automatic allocation is disabled until `DynamicAssignmentRangeVerified = true` is explicitly set after reviewing this controller's persistent `$SNPX_ASG` assignments and other client reservations. The default 10001-16384 range only avoids the observed ROBOGUIDE default 1-10000 mapping; it is not a portable safety guarantee. Use `ReservedHoldingRegisterRanges` for known occupied portions; both automatic and manual dynamic assignments are rejected when they overlap a reserved range.

The default endpoint is `opc.tcp://127.0.0.1:4880`. A different endpoint is passed as the first argument. The test server's security-free endpoint is never selected unless `--allow-insecure` is present.

```powershell
dotnet run --project tests/Robentex.Fanuc.OpcUa.Console -- opc.tcp://192.0.2.10:4880 --allow-insecure
```

## Automated unit tests

The unit tests do not connect to a robot. They cover dynamic Holding Register allocation and collision protection, 1-based address/0-based IndexRange conversion, Int16/Float32 conversion, target-scoped write locks and cancellation cleanup, Good/Bad/Uncertain StatusCode handling, and session-close mapping-cache cleanup.

```powershell
dotnet test tests/Robentex.Fanuc.OpcUa.Tests/Robentex.Fanuc.OpcUa.Tests.csproj --no-restore -m:1
```

To verify the default write-deny policy without connecting to, mapping, or writing a robot:

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --verify-write-policy
```

### Explicit ROBOGUIDE system-write test

The console never writes by default. The write test is additionally restricted to a loopback endpoint and requires all three flags below, preventing use against a network-connected physical controller. It reads `$MCR.$GENOVERRIDE`, writes `20`, verifies the result and restores the original value in `finally`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-system-write --simulation --allow-insecure
```

Use this only with a running local ROBOGUIDE simulation. The console prints the SETASG, OPC UA write, and read-back status codes for every step, including restoration.

### Explicit ROBOGUIDE R[] write test

This simulation-only test uses `R[100]`, verifies `10.25` and `-35.75`, then restores the original value in `finally`. It has the same loopback, insecure-test-endpoint, and explicit simulation requirements:

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-register-write --simulation --allow-insecure
```

### Explicit ROBOGUIDE PR[] write test

The simulation-only PR[] test performs one full Cartesian update (`10.25`, `-35.75`, ...), one partial X-only update, verifies each read-back, and restores the original Cartesian values in `finally`. The position register defaults to `PR[100]` and can be changed using `--position-register=<number>`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-position-write --simulation --allow-insecure --position-register=100
```

### Explicit ROBOGUIDE PR[] CONFIG write test

This simulation-only test verifies `NDB -> FUT -> NDB`, TURN4-6 and read-back, then restores the initial Cartesian and CONFIG values. It requires the initial PR to be valid Cartesian (`VALIDC=1`).

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-position-config-write --simulation --allow-insecure --position-register=100
```

### Read-only DI/DO test

This read-only test maps and reads `DI[1]` and `DO[1]` by default; it does not write either signal. Select different signal numbers with `--di-number` and `--do-number`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-digital-io-read --allow-insecure --di-number=1 --do-number=1
```

### Read-only extended I/O test

This test reads `RI[1]`, `RO[1]`, `GI[1]`, and `GO[1]` by default. The group values are displayed in decimal and hexadecimal. All signal numbers are configurable.

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

### Read-only ARC BOOL diagnostic

This diagnostic keeps one connection open. Each cycle reads the robot status snapshot, `PRG[1]` program status, `$AWEPOR[1].$ARC_ON`, then `$AWEPOR[1].$ARC_ENABLE`; the BOOL reads remain 500 ms apart. It prints the BOOL converted value, raw registers, dynamic address, SETASG command/status, and whether the command was served from the connection-scoped mapping cache. Stop it with Ctrl+C.

Pass `--skip-robot-status` to omit `GetRobotStatusAsync()` or `--skip-program-status` to omit `ReadProgramStatusAsync(1)` while preserving the BOOL reads and their detailed logs. The flags can be combined to isolate the effect of the two calls.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-arc-bool-diagnostics --allow-insecure
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-arc-bool-diagnostics --allow-insecure --skip-robot-status
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-arc-bool-diagnostics --allow-insecure --skip-program-status
```

### ROBOGUIDE-only status remap diagnostic

This isolated read-only diagnostic checks whether a robot-status read affects the dynamic mapping for `$AWEPOR[1].$ARC_ENABLE`. It reads ARC_ENABLE, calls `GetRobotStatusAsync()`, reads ARC_ENABLE again, then clears only the current session's dynamic assignments and reads it once more to force a new SETASG. It requires an explicit local simulation confirmation and does not modify persistent `$SNPX_ASG` data.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-status-remap --simulation --allow-insecure
```

To exclude `GetRobotStatusAsync()`'s in-memory conversion from the diagnosis, use the discovery-only variant below. `DiscoverRobotStatusNodesAsync()` currently combines address-space Browse with status-node `Value` and `DataType` reads, so the public API cannot yet split those two server operations further.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-status-discovery-remap --simulation --allow-insecure
```

### ROBOGUIDE-only raw status-service remap diagnostic

This deeper diagnostic uses the same client session to first Browse only the `RobotInformation` and `MotionDeviceSystem` trees, then—after a session-local remap—read only `Value` and `DataType` from explicit status NodeIds. Obtain the NodeIds with `--test-robot-status-discovery`; do not assume they are portable between controllers. Repeat `--status-node-id` for every node to include.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-status-service-remap --simulation --allow-insecure --status-node-id="ns=1;i=123" --status-node-id="ns=1;i=124"
```

The test uses reflection only inside the Console diagnostic to access the current client session; it does not change the library API or write robot data.

### ROBOGUIDE-only per-status-attribute remap diagnostic

This test isolates each `Value` and `DataType` read for `OperationState`, `ServoState`, `ModeState`, `ProgramSpeed`, and `Alarm`. Before each individual read it recreates only the current session's ARC_ENABLE mapping, verifies it, reads the one status attribute, then reads ARC_ENABLE again. It ends with a Markdown summary table. All NodeIds are required and must be copied from the discovery output.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-status-node-attribute-remap --simulation --allow-insecure "--operation-state-node-id=ns=1;i=123" "--servo-state-node-id=ns=1;i=124" "--mode-state-node-id=ns=1;i=125" "--program-speed-node-id=ns=1;i=126" "--alarm-node-id=ns=1;i=127"
```

### ROBOGUIDE-only ProgramSpeed/Alarm pre-read diagnostic

This test reads `ProgramSpeed.Value` and `Alarm.Value` before creating the ARC_ENABLE dynamic mapping, then samples ARC_ENABLE three times at 500 ms intervals. It repeats the two status reads and samples ARC_ENABLE once more, allowing comparison of a status read made before versus after the mapping exists.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-status-pre-read-remap --simulation --allow-insecure "--program-speed-node-id=ns=1;i=126" "--alarm-node-id=ns=1;i=127"
```

### ROBOGUIDE-only same-address SETASG refresh diagnostic

This diagnostic maps and verifies ARC_ENABLE, reads `ProgramSpeed.Value`, verifies the observed mapping failure, then sends the identical `SETASG` command to the same address without sending `CLRASG`. It reports whether the following ARC_ENABLE read is restored. The direct Command write is limited to the local simulation diagnostic and does not modify persistent `$SNPX_ASG` data.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-status-setasg-refresh --simulation --allow-insecure "--program-speed-node-id=ns=1;i=126"
```

### Read-only R[] and PR[] tests

These commands perform only dynamic mapping and reads; they do not change the selected R[] or PR[]. `R[100]` and `PR[100]` are the defaults and can be changed independently.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-register-read --allow-insecure --register-number=100
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-position-read --allow-insecure --position-register=100
```

### Explicit ROBOGUIDE DO[] write test

This test is disabled unless all safety flags are explicit. It records the original `DO[1]`, writes and verifies ON then OFF, and restores the original value in `finally`. It accepts `--do-number=<n>`.

```powershell
dotnet run --no-restore --project tests/Robentex.Fanuc.OpcUa.Console -- --test-digital-output-write --simulation --allow-insecure --do-number=1
```
