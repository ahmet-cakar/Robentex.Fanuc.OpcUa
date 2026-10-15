---
name: fanuc-opcua
description: FANUC OPC UA HMI Device Communication implementation: HoldingRegisters, Command, CLRASG/SETASG, R/PR numeric conversion and documented robot safety constraints.
---

# FANUC OPC UA skill

Use for FANUC-specific API design, implementation, verification and troubleshooting.

## Workflow
1. Read repository `AGENTS.md` and determine the **currently authorized milestone**.
2. Inspect existing classes and tests; avoid replacing working code.
3. Consult `docs/Additional-Option-Manual.pdf`, chapter 55, especially §§55.3 and 55.4. Check the manual before assuming encodings or capabilities.
4. Implement the smallest verifiable change. Default to read-only tests; ask before writes to real robots.
5. Run `dotnet build --no-restore` and appropriate `dotnet test --no-restore` (per AGENTS.md). Never restore/install packages.

## Dynamic assignments
- On the tested ROBOGUIDE server, `HoldingRegisters` is an Int16 array; documented Modbus holding address N maps to OPC UA array index N-1. Verify this on other controllers.
- `Command` accepts one complete String command per write (maximum 128 characters per manual). Writing `CLRASG` resets current connection's dynamic assignments; use once before a SETASG initialization sequence, **not before each read**.
- Syntax: `SETASG <ADDRESS> <SIZE> <VAR_NAME> [MULTIPLY]`. Optional MULTIPLY defaults to 1. Assignments are connection-specific and override `$SNPX_ASG` for that connection. Maintain a collision-free range allocator, document bounds, and recreate mappings after reconnect.
- Do not modify persistent `$SNPX_ASG`. Do not assume that reading the Command node back echoes the command; confirm operation through mapped values/status.

## Types and writes
- Per manual §55.3.8, an INTEGER system variable uses two holding registers, low 16-bit word first. Implement signed Int32 reconstruction correctly; test positive and negative values.
- For `R[]` and `PR[]`, verify data type, scaling, packing, sign, decimal precision, frame/group, position representation and limits in the manual. A C# `double` is **not** directly writable to a single Int16 register. Do not invent a general REAL encoding.
- Support decimals such as `10.2`, `-25.75` for supported numeric R[] and PR[] components. Validate round-trip precision and explicitly reject unsupported representations.
- For PR XYZWPR writes, validate robot state, group, coordinate representation and frame semantics. Do not assume multi-register writes are atomic.
- Node IDs `ns=1;i=304` and `ns=1;i=305` are observations from ROBOGUIDE, not constants for every robot. Resolve by browse or configurable mapping.

## Recommended milestone order
1. Connection/session only (already developed; verify before proceeding).
2. Browse/resolve nodes and indexed read-only HoldingRegisters.
3. Command + dynamic mapping + read-only `$MCR.$GENOVERRIDE` test.
4. R[] reading and verified numeric conversions; separately approved writing.
5. PR[] reading and verified conversions; separately approved writing.
6. Digital I/O, reconnect, concurrency and robustness.

## Verification checklist
- Test endpoint configured for the environment, no hardcoded host.
- Use `--no-restore` for build/test; report missing packages instead of installing.
- Confirm no persistent robot settings changed.
- Explain mapping address versus zero-based array index.
- Report observed values and robot pendant comparison separately from assumptions.
