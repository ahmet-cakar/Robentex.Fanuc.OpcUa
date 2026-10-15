---
name: dotnet-quality
description: Build, test and review the Robentex.Fanuc.OpcUa .NET 9 solution without performing NuGet installation or restore.
---

# .NET 9 quality and execution skill

## Required workflow
1. Inspect solution, existing projects, target frameworks, package references and relevant source files.
2. Make focused changes only for the authorized milestone. Preserve public API compatibility where possible.
3. Build using `dotnet build --no-restore` (with explicit solution/project path if needed).
4. Run relevant tests using `dotnet test --no-restore` when a test project exists and can run safely. A Console application is not automatically a unit-test project; do not claim otherwise.
5. If build/test fails due to code, fix and rerun. If it fails due to missing package assets, stop and provide exact user-side restore/install instructions.

## Forbidden dependency operations
- NEVER run `dotnet restore`, `dotnet add package`, `nuget install`, package manager install, or commands that implicitly restore dependencies.
- NEVER run `dotnet build` or `dotnet test` without `--no-restore`.
- You MAY edit `.csproj` PackageReference versions when necessary, but tell the user to restore/install manually before the next build.

## Coding requirements
- .NET 9; nullable reference types and clear contracts.
- Async APIs with CancellationToken; avoid sync-over-async and fire-and-forget operations.
- Thread-safe connection state and clean disposal; no hidden global singleton assumptions.
- Typed options for endpoint, credentials, security and timeouts; no hardcoded local network settings.
- Structured, actionable exceptions; do not log passwords or sensitive certificate material.
- Keep robot protocol handling separate from UI and test-console code.
- Unit-test pure conversions and mapping logic independently of an actual robot.

## Final report in Turkish
List changed files, build command/result, test command/result, missing packages or prerequisites, and any unverified behavior. Never present unexecuted tests as passed.
