# Security and Safety

## Safety and security

The library is secure by default: a security-free OPC UA endpoint requires explicit opt-in, and an unknown server certificate is never automatically trusted. Dynamic Holding Register assignment is disabled until the caller confirms that its configured address range does not conflict with persistent `$SNPX_ASG` assignments or other clients.

All robot writes are disabled by default. Enabling a write category requires both its opt-in setting and the corresponding explicit allow-list. A successful OPC UA write is followed by a read-back check, but FANUC does not document multi-register writes as controller-atomic. Validate every deployment with the controller owner and applicable machine-safety procedure.

Before building, install these official packages in `src/Robentex.Fanuc.OpcUa`:

```powershell
dotnet add package OPCFoundation.NetStandard.Opc.Ua.Client --version 1.5.378.176
dotnet add package OPCFoundation.NetStandard.Opc.Ua.Configuration --version 1.5.378.176
```

They are already declared as `PackageReference` items so only one restore is needed after your package source is available; the commands above are supplied as the requested manual installation alternative.

The offline-capable unit-test project at `tests/Robentex.Fanuc.OpcUa.Tests` additionally declares `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.3, and `xunit.runner.visualstudio` 2.8.2. These packages are not installed automatically by this repository.

The library provides endpoint discovery, secure-by-default session creation, connection state observation, and synchronous/asynchronous disposal. It also supports connection-scoped `CLRASG`/`SETASG` dynamic assignments and read-only `$MCR.$GENOVERRIDE` validation through Holding Registers. It can also read INTEGER (`int`), REAL (`float`), BOOLEAN (`bool`), and STRING (`string`) system variables with `ReadSystemVariableAsync<T>`, R[] and Cartesian PR[] values, plus DI[]/DO[] signals.

For a STRING system variable, `ReadSystemVariableAsync<string>` creates a 40-register mapping with `MULTIPLY=1`, as specified by Additional Option Manual §§55.3.1 and 55.3.8. Each register contains two characters (low byte first), and the string must contain a NUL terminator. The manual does not specify a character code page; the library uses Latin-1 for a one-to-one byte mapping and rejects a missing terminator instead of silently treating an unterminated 80-byte buffer as a string. STRING writes are not supported.


## PKI and certificate trust

The first connection creates the client certificate under `%LOCALAPPDATA%\\\Robentex\\\FanucOpcUa\\\pki` by default. Its `own`, `trusted`, `issuers`, and `rejected` stores are configured explicitly. For a secured FANUC server, add its certificate (or issuing CA) to the appropriate trust store through your organization’s certificate approval process; this library never auto-accepts an unknown server certificate.


## Operational restrictions

- Allow-list protected writes and mandatory read-back do not constitute functional-safety controls.
- Review dynamic Holding Register allocations against persistent `$SNPX_ASG` assignments and other clients.
- Use a controller-owner-approved machine safety procedure for any real robot write.
- Security-free endpoints are intended only for explicitly approved test environments.
