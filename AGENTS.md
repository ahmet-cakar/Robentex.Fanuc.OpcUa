# Robentex.Fanuc.OpcUa — Codex project instructions

## Mission
Develop a reusable, UI-independent .NET 9 FANUC OPC UA client library usable from WPF, WinForms, ASP.NET Core, Console and Windows Services. Develop incrementally, preserving completed milestones. Communicate progress and questions in Turkish.

## Mandatory execution policy
- **DO build and test.** After code changes, run `dotnet build --no-restore` and relevant `dotnet test --no-restore` where feasible. Fix compilation/test failures caused by your changes. You may run a console smoke test if it does not write to the robot and a test server is available.
- **DO NOT download/install dependencies or restore.** Never run `dotnet restore`, `dotnet add package`, `nuget install`, `dotnet tool install`, or any command that implicitly restores packages. Do not run bare `dotnet build` or bare `dotnet test`; always pass `--no-restore`. If packages/assets are missing, STOP dependency-dependent commands and tell the user the exact package IDs/versions and commands for them to run manually.
- Inspect the existing solution and files before editing. Do not regenerate or overwrite working code without reason.
- Implement **only** the current explicitly approved milestone; never pre-implement future features. Ask when a choice affects safety, behavior, or compatibility.
- Never modify robot settings, persistent `$SNPX_ASG`, or write to live robot registers/PR/outputs without explicit approval. Prefer read-only tests and ROBOGUIDE.
- Never claim a build, test or physical-robot check succeeded unless actually executed and verified.

## Design rules
- Endpoint URL, host, port, security policy, authentication, certificates, timeouts and node mapping must be configurable. `opc.tcp://127.0.0.1:4880` is a ROBOGUIDE example, NOT a universal constant. Do not assume all FANUC controllers advertise the same endpoint or NodeIds.
- Use official OPC Foundation UA-.NETStandard packages; do not reimplement OPC UA.
- Public API should be small, typed, async, CancellationToken-aware, and framework-independent. Support safe session cleanup (`IDisposable`/`IAsyncDisposable`), clear errors and reconnect behavior.
- Never silently truncate, round or reinterpret numeric values. Decimal and negative values are essential for numeric `R[]` and `PR[]` operations; implement encoding only after validating the FANUC manual and read/write round trips.
- Treat multi-register operations, allocation, connection concurrency and write permissions explicitly. Do not promise atomicity unless documented.
- Do not weaken production security. Anonymous/None and untrusted certificates require explicit opt-in; never auto-trust unknown server certificates silently.

## Planned capabilities (not authorization to implement all now)
1. Connection/session/status/errors.
2. Node discovery and HoldingRegisters indexed reads/writes.
3. Connection-scoped dynamic mapping via Command (`CLRASG`, `SETASG`).
4. System variable reads, then approved writes, with typed conversions.
5. Numeric `R[]` reads/writes including decimals and negative values.
6. Position `PR[]` reads/writes, single component and complete XYZWPR, with correct representation, group, frames and validation.
7. Digital I/O reads/writes as supported.
8. Reconnect, concurrency, diagnostics and automated tests.

## Reference documents
- `docs/Additional-Option-Manual.pdf` is the authoritative FANUC-specific reference; chapter 55 `HMI DEVICE COMMUNICATION`, especially §§55.3–55.4. Check exact variable/position formats, supported types and address limits before implementing them. If absent, ask for it; never invent encoding rules.
- Official OPC Foundation docs and package APIs are authoritative for the UA client layer.
- Cite relevant manual section/page in implementation notes when a non-obvious rule is used.

## Verified ROBOGUIDE observations (not portable defaults)
- ROBOGUIDE test host `opc.tcp://127.0.0.1:4880`; actual advertised endpoint may contain an additional path.
- `Objects/Modbus/HoldingRegisters`: `ns=1;i=304`, `Int16[16384]`.
- `Objects/Modbus/Command`: `ns=1;i=305`, `String`.
- `CLRASG` followed by `SETASG 10001 2 $MCR.$GENOVERRIDE` was tested; HoldingRegisters indices `[10000]=100`, `[10001]=0` and pendant override was 100%. The INTEGER low word is first per the manual.
- The observed persistent `$SNPX_ASG[1]` uses address 1, size 10000, `R[1]@1.1`. Do not change it.
- Dynamic assignments are connection-scoped, take precedence over persistent assignments on that connection and must be rebuilt on reconnect. Do not send `CLRASG` for each read.

## Skills
- FANUC-specific tasks: `.agents/skills/fanuc-opcua/SKILL.md`
- .NET implementation/build/review tasks: `.agents/skills/dotnet-quality/SKILL.md`

## Report format after each milestone
In Turkish, state: files changed, functionality added, exact build/test commands and results, unverified assumptions, packages requiring user-side installation, and next suggested milestone. If `--no-restore` fails due to missing assets, say so rather than attempting restore.
