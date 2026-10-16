Robentex.Fanuc.OpcUa
FANUC Robot Integration SDK for .NET  
Connect, monitor, and interact with compatible FANUC robot controllers over OPC UA.
![NuGet](https://img.shields.io/nuget/v/Robentex.Fanuc.OpcUa?label=NuGet)
![NuGet Downloads](https://img.shields.io/nuget/dt/Robentex.Fanuc.OpcUa)
![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)
![License: MIT](https://img.shields.io/badge/License-MIT-blue)
A focused .NET client library for FANUC controllers with the HMI Device Communication OPC UA option. Built on the official OPC Foundation .NET Standard stack, it provides higher-level APIs for frequently needed robot data while making write permissions explicit.
> **Compatibility:** Targets `net9.0` (also consumable from compatible later .NET frameworks). Available OPC UA nodes and features depend on the controller, firmware, options and server configuration. This library is not a safety-rated controller.
Highlights
Capability	Supported operations
OPC UA connectivity	Endpoint discovery, session management, connection observation, certificate validation
System variables	Typed read: `int`, `float`, `bool`, `string`; controlled write: `int`, `float`, `bool`
Numeric registers `R[]`	Read and allow-listed write
Position registers `PR[]`	Cartesian `X/Y/Z/W/P/R` read and full/partial write; CONFIG handling
Digital I/O	`DI[]` read; `DO[]` read and allow-listed write
Extended I/O	`RI[]`, `RO[]`, `GI[]`, `GO[]` read
Robot status	Discover optional nodes; read operation, mode, servo, alarm and safety status when available
TP program status	Active program, line number, execution state and parent program via `PRG[n]`
Why this package?
Useful APIs instead of raw register handling: The library handles connection-scoped `CLRASG` / `SETASG` mapping, conversion and allocation.
Safe-by-default writes: Robot writes are disabled until explicitly enabled and allow-listed; supported writes use read-back verification.
Portable endpoint configuration: Use the endpoint of your controller or ROBOGUIDE instance, not a hard-coded simulator address.
Transparent limitations: Unavailable optional status nodes are reported as unavailable rather than invented values.
Install
```bash
dotnet add package Robentex.Fanuc.OpcUa --version 1.0.2
```
Quick start: connect and read
The example below uses the package's public API. Replace the endpoint with your controller's actual OPC UA endpoint. `DynamicAssignmentRangeVerified` must only be enabled after confirming that the configured Holding Register range does not collide with persistent mappings or other OPC UA clients.
```csharp
using Robentex.Fanuc.OpcUa;

var options = new FanucOpcUaConnectionOptions
{
    EndpointUrl = new Uri("opc.tcp://robot.example:4880/FANUC/NanoUaServer"),
    DynamicAssignmentRangeVerified = true
};

await using var client = new FanucOpcUaClient(options);
await client.ConnectAsync();

var overridePercent = await client.ReadSystemVariableAsync<int>("$MCR.$GENOVERRIDE");
var registerValue = await client.ReadRegisterAsync(100);
var program = await client.ReadProgramStatusAsync(1);
```
> This is a secured-endpoint example. Trust the controller certificate using the library's PKI procedure; do not disable certificate verification in production. Confirm the namespace/imports against the distributed XML API docs before publishing this snippet as a compile-tested sample.
New in 1.0.2
FANUC STRING system-variable reads via `ReadSystemVariableAsync<string>`.
Maps 40 Holding Registers with `MULTIPLY=1`.
Decodes low-byte-first characters and validates NUL termination.
Read-only behavior was verified using `$RBTIF.SERIAL_NUM` in ROBOGUIDE.
STRING writes are not supported.
Security and operational boundaries
Unknown OPC UA server certificates are not automatically accepted. Security-free endpoints require explicit opt-in.
Dynamic Holding Register assignments require a verified non-conflicting address range.
Writes are disabled by default, require category-specific opt-in and exact allow-lists, and use read-back checks.
Multi-register writes are not guaranteed to be atomic on the controller. An OPC UA success response alone does not guarantee the intended physical result.
This package does not implement functional safety, collision avoidance, motion interlocks or emergency-stop logic.
Actual controller support must be validated on the target robot; ROBOGUIDE verification alone does not establish production compatibility.
Supported platforms
.NET target: .NET 9 (compatible later frameworks as allowed by NuGet).
Robot side: FANUC controllers exposing the relevant HMI Device Communication OPC UA functionality.
Some robot-status nodes are optional and may be absent on particular controller software versions.
Documentation
API guide — operations, data types, register mappings and limitations.
Security and safety — PKI, certificate trust and controlled robot writes.
ROBOGUIDE testing — simulator commands and deep diagnostics (source repository only).
Changelog — release history.
> These links become available after the documentation files are committed and pushed to the repository’s `main` branch.
License
MIT License. FANUC and ROBOGUIDE are trademarks of their respective owners. This package is an independent integration library and does not distribute FANUC documentation, controller software or robot configuration data.