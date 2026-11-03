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

The implementation is in progress. Given a content pack, the host compiles
its rules, generates a deterministic world during `Prepare`, and publishes an
initial world snapshot over the read-stream WebSocket. The host also provides
authenticated HTTP lifecycle control, lockstep WebSockets, and SQLite
checkpoints for the generated world, tick orchestrator, and idempotency window.
Domain systems are not yet connected to the host phases. The viewer has greedy,
incremental terrain meshing, procedural material shading, static agent previews,
an agent inspector, and keyboard-controlled orbit/free cameras (`O` and `F`
modes). See the component
[roadmap](../docs/prism/ROADMAP.md) for phase status.

## Launch the graphical viewer

On Linux, use a terminal opened inside a graphical desktop session with an
OpenGL 3.3-capable driver. Confirm the session exposes X11 or Wayland:

```sh
printf 'DISPLAY=%s\nWAYLAND_DISPLAY=%s\n' "$DISPLAY" "$WAYLAND_DISPLAY"
```

At least one value should be non-empty. A terminal reached over SSH, a
headless service, or a remote VS Code session without X11/Wayland forwarding
cannot open the graphical window; run the command in the machine's desktop
terminal instead. `Prism.Host` remains headless and does not require either
display variable.

When both `DISPLAY` and `WAYLAND_DISPLAY` are present, the viewer uses
`XDG_SESSION_TYPE` to prefer the desktop's native backend. This avoids
mistaking an available XWayland bridge for the active desktop backend. If the
session type is unset or unclear, GLFW selects the platform automatically.
From this directory, launch the standalone deterministic preview:

```sh
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --seed 12345 \
  --size 96
```

If startup reports that the native GLFW library could not be loaded, restore
the NuGet runtime assets from this directory and retry:

```sh
dotnet clean Prism.Viewer/Prism.Viewer.csproj
dotnet restore Prism.Viewer/Prism.Viewer.csproj
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- --seed 12345 --size 96
```

On Linux x86-64, the bundled GLFW library should be at
`Prism.Viewer/bin/Debug/net10.0/runtimes/linux-x64/native/libglfw.so.3`.
If it exists but still cannot be loaded, check missing shared dependencies
with `ldd <path-to-libglfw.so.3>`; on Ubuntu, `libglfw3` is a system-runtime
fallback (`sudo apt install libglfw3`).

The command builds and opens the Silk.NET/OpenGL window. It does **not** connect
to `Prism.Host`: the terrain and three sample agents are static previews, not
live simulation data. Use `--help` to list options; `--no-demo-agents` hides the
sample agents. For example:

```sh
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --seed 42 \
  --size 128 \
  --no-demo-agents
```

Viewer controls:

| Key | Action |
| :-- | :-- |
| `O` / `F` | Orbit / free camera |
| `W` `A` `S` `D` | Pan the orbit target or move the free camera |
| `Q` / `E` | Zoom in/out in orbit mode; move vertically in free mode |
| Arrow keys | Rotate the view |
| `V` | Toggle the biome/zone color overlay |
| `Tab` | Inspect the next sample agent (ID, position, inventory) |
| `C` | Follow/unfollow the selected sample agent |
| `Esc` | Close the viewer |

To run the already-built viewer without rebuilding, use
`dotnet run --no-build --project Prism.Viewer/Prism.Viewer.csproj -- --seed 12345 --size 96`.

For local host development, set `LIVEX_SESSION_TOKEN` and run:

```sh
dotnet run --project Prism.Host -- \
  --control-port 5181 \
  --work-dir ./data \
  --instance-id local-prism \
  --log-dir ./logs \
  --correlation-id local-run \
  --headless
```

The headless host accepts control mutations only when `LIVEX_SESSION_TOKEN` is
set. The SQLite database is created as `world.sqlite` in the supplied work
directory. The host generates and publishes initial terrain, but does not yet
advance its domain systems or publish region/entity deltas.

To produce the platform-native host binary at the path declared by
`component.json`, publish on the target OS:

```sh
dotnet publish Prism.Host/Prism.Host.csproj \
  --configuration Release \
  --output ./bin
```

The Launcher supplies both `--control-port` and `--read-port`; the read-stream
WebSocket endpoint is exposed at `/data/read`.
