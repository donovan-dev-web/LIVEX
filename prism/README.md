# PRISM V2

PRISM V2 is the headless, deterministic world simulation described in
[`../docs/prism/README.md`](../docs/prism/README.md). The solution targets
.NET 10 and keeps graphics dependencies out of `Prism.Core`.

## Build and test

From this directory:

```powershell
dotnet restore Prism.sln
dotnet build Prism.sln --no-restore --configuration Release --warnaserror
dotnet test Prism.sln --no-build --configuration Release
```

`Prism.Architecture.Tests` checks that the core remains BCL-only and rejects
floating-point state types, system-clock access, and `System.Random`.

## Project layout

- `Prism.Core`: deterministic world simulation; BCL only.
- `Prism.Contracts`: transport-independent messages and interfaces.
- `Prism.Content`: content-pack compilation and validation.
- `Prism.Host`: headless process and control/data endpoints.
- `Prism.Viewer`: optional read-only graphical client.
- `Prism.Bots`: simulation test agents.
- `Prism.Benchmarks`: performance spikes and benchmarks.
- `*.Tests`: unit, integration, and architecture tests.

The projects currently establish the dependency boundaries only. Production
simulation work starts after the P1/G1 risk benchmarks; see the component
[roadmap](../docs/prism/ROADMAP.md).
