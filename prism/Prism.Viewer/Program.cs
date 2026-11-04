using System.Numerics;
using Prism.Core.World;
using Prism.Viewer;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

var options = ViewerOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("PRISM debug viewer");
    Console.WriteLine("  --seed <uint64>  World seed (default: 12345)");
    Console.WriteLine("  --size <int>     Horizontal world size, 16-512 (default: 96)");
    Console.WriteLine("  --day-length <ticks>   Ticks per in-world day (default: 480)");
    Console.WriteLine("  --season-length <days> Days per season (default: 4)");
    Console.WriteLine("  --ticks-per-second <n> Preview clock speed, 0 pauses (default: 20)");
    Console.WriteLine("  --tick <long>    Starting preview tick (default: 0)");
    Console.WriteLine("  --no-demo-agents Hide the generated agent previews");
    Console.WriteLine("  --help           Show this help");
    return;
}

var displayMessage = ViewerPlatformDiagnostics.GetMissingLinuxDisplayMessage(
    OperatingSystem.IsLinux(),
    Environment.GetEnvironmentVariable("DISPLAY"),
    Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
if (displayMessage is not null)
{
    Console.Error.WriteLine(displayMessage);
    Environment.ExitCode = 1;
    return;
}

try
{
    LinuxGlfwPlatform.Configure();
}
catch (FileNotFoundException exception)
{
    var bundledGlfwPath = LinuxGlfwPlatform.GetBundledLibraryPath(
        AppContext.BaseDirectory,
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
    Console.Error.WriteLine(ViewerPlatformDiagnostics.GetMissingGlfwLibraryMessage(
        exception.Message,
        System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
        bundledGlfwPath,
        bundledGlfwPath is not null && File.Exists(bundledGlfwPath)));
    Environment.ExitCode = 1;
    return;
}
catch (DllNotFoundException exception)
{
    var bundledGlfwPath = LinuxGlfwPlatform.GetBundledLibraryPath(
        AppContext.BaseDirectory,
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
    Console.Error.WriteLine(ViewerPlatformDiagnostics.GetMissingGlfwLibraryMessage(
        exception.Message,
        System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
        bundledGlfwPath,
        bundledGlfwPath is not null && File.Exists(bundledGlfwPath)));
    Environment.ExitCode = 1;
    return;
}

var materials = new WorldSurfaceMaterialIds(1, 2, 3, 4, 5);
var generated = WorldSurfaceGenerator.Generate(
    options.Size,
    options.Size,
    regionSize: 16,
    verticalSize: 64,
    options.Seed,
    new SurfaceHeightConfig(),
    new WorldZoneConfig(),
    materials);
WaterBodyGenerator.FillBelowSeaLevel(generated.Grid, waterMaterialId: 6, seaLevelZ: 26);
var palette = new TerrainMaterialPalette(1, 2, 3, 4, 5, 6);
var previewAgents = options.ShowDemoAgents
    ? CreatePreviewAgents(generated.Grid)
    : [];

var windowOptions = WindowOptions.Default;
windowOptions.API = new GraphicsAPI(
    ContextAPI.OpenGL,
    ContextProfile.Core,
    ContextFlags.Default,
    new APIVersion(3, 3));
windowOptions.Size = new Vector2D<int>(1280, 800);
windowOptions.Title = $"PRISM viewer — seed {options.Seed} — O orbit, F free, WASD move, arrows look";
windowOptions.FramesPerSecond = 60;
windowOptions.UpdatesPerSecond = 60;
IWindow window;
try
{
    window = Window.Create(windowOptions);
}
catch (PlatformNotSupportedException exception)
{
    Console.Error.WriteLine(
        ViewerPlatformDiagnostics.GetWindowPlatformFailureMessage(exception.Message));
    Environment.ExitCode = 1;
    return;
}

using (window)
{
    var calendar = new ViewerCalendarConfig(options.DayLengthTicks, options.SeasonLengthDays);
    calendar.Validate();
    var viewer = new ViewerWindow(
        window,
        generated.Grid,
        generated.Zones,
        palette,
        previewAgents,
        options.Seed,
        calendar,
        options.StartTick,
        options.TicksPerSecond);
    window.Load += viewer.OnLoad;
    window.Update += viewer.OnUpdate;
    window.Render += viewer.OnRender;
    window.FramebufferResize += viewer.OnResize;
    window.Closing += viewer.OnClosing;
    window.Run();
}

static AgentEntitySnapshot[] CreatePreviewAgents(WorldGrid world)
{
    var previewCells = new[]
    {
        (X: world.Width / 2 - 2, Y: world.Height / 2),
        (X: world.Width / 2, Y: world.Height / 2),
        (X: world.Width / 2 + 2, Y: world.Height / 2),
    };
    return previewCells.Select((cell, index) =>
    {
        var surface = world.GetSurfaceColumn(cell.X, cell.Y);
        var inventory = index == 1
            ? new[] { new AgentItemStack(1, 2, 20) }
            : [];
        return new AgentEntitySnapshot(
            (ulong)(index + 1),
            AgentPosition.AtCell(cell.X, cell.Y, surface.SurfaceZ + 1),
            new AgentBodyProfile(),
            inventory);
    }).ToArray();
}

internal sealed record ViewerOptions(
    ulong Seed,
    int Size,
    bool ShowHelp,
    bool ShowDemoAgents,
    int DayLengthTicks,
    int SeasonLengthDays,
    long StartTick,
    double TicksPerSecond)
{
    public static ViewerOptions Parse(string[] args)
    {
        ulong seed = 12_345;
        var size = 96;
        var showHelp = false;
        var showDemoAgents = true;
        var dayLengthTicks = 480;
        var seasonLengthDays = 4;
        long startTick = 0;
        var ticksPerSecond = 20d;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--seed" when index + 1 < args.Length &&
                                   ulong.TryParse(args[index + 1], out var parsedSeed):
                    seed = parsedSeed;
                    index++;
                    break;
                case "--size" when index + 1 < args.Length &&
                                   int.TryParse(args[index + 1], out var parsedSize):
                    size = parsedSize;
                    index++;
                    break;
                case "--day-length" when index + 1 < args.Length &&
                                         int.TryParse(args[index + 1], out var parsedDayLength):
                    dayLengthTicks = parsedDayLength;
                    index++;
                    break;
                case "--season-length" when index + 1 < args.Length &&
                                            int.TryParse(args[index + 1], out var parsedSeasonLength):
                    seasonLengthDays = parsedSeasonLength;
                    index++;
                    break;
                case "--tick" when index + 1 < args.Length &&
                                  long.TryParse(args[index + 1], out var parsedTick):
                    startTick = parsedTick;
                    index++;
                    break;
                case "--ticks-per-second" when index + 1 < args.Length &&
                                               double.TryParse(
                                                   args[index + 1],
                                                   System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture,
                                                   out var parsedTicksPerSecond):
                    ticksPerSecond = parsedTicksPerSecond;
                    index++;
                    break;
                case "--no-demo-agents":
                    showDemoAgents = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown or invalid argument: {args[index]}");
            }
        }

        if (size is < 16 or > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "World size must be between 16 and 512.");
        }

        if (dayLengthTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Day length must be positive.");
        }

        if (seasonLengthDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Season length must be positive.");
        }

        if (startTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Starting tick must be non-negative.");
        }

        if (ticksPerSecond < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Ticks per second must be non-negative.");
        }

        return new ViewerOptions(
            seed,
            size,
            showHelp,
            showDemoAgents,
            dayLengthTicks,
            seasonLengthDays,
            startTick,
            ticksPerSecond);
    }
}

internal sealed class ViewerWindow(
    IWindow window,
    WorldGrid world,
    WorldZone[] zones,
    TerrainMaterialPalette palette,
    AgentEntitySnapshot[] previewAgents,
    ulong seed,
    ViewerCalendarConfig calendar,
    long startTick,
    double ticksPerSecond)
{
    private const int MeshBudgetPerFrame = 4;
    private readonly IncrementalTerrainMesher _mesher = new();
    private readonly ViewerCamera _camera = ViewerCamera.LookAt(
        new Vector3(world.Width / 2f, world.Height / 2f, 30f),
        MathF.PI / 4,
        -0.55f,
        Math.Max(world.Width, world.Height) * 1.15f);
    private bool _orbitKeyDown;
    private bool _freeKeyDown;
    private bool _overlayKeyDown;
    private bool _agentSelectKeyDown;
    private bool _followKeyDown;
    private bool _zoneOverlay;
    private int _selectedAgentIndex;
    private GL? _gl;
    private IInputContext? _input;
    private IKeyboard? _keyboard;
    private long _tick = startTick;
    private double _tickAccumulator;
    private ViewerDayNightState _time = ViewerDayNightCycle.Evaluate(startTick, calendar);
    private uint _program;
    private int _viewProjectionLocation;
    private int _lightTintLocation;
    private GpuMesh _hudMesh = new(0, 0, 0);
    private int _viewportWidth;
    private int _viewportHeight;
    private readonly Dictionary<int, GpuMesh> _gpuMeshes = [];
    private readonly List<GpuMesh> _gpuAgentMeshes = [];

    public void OnLoad()
    {
        _gl = GL.GetApi(window);
        _input = window.CreateInput();
        _keyboard = _input.Keyboards.FirstOrDefault();
        if (_keyboard is null)
        {
            throw new InvalidOperationException("The viewer window did not expose a keyboard.");
        }

        _program = CreateProgram(_gl);
        _viewProjectionLocation = _gl.GetUniformLocation(_program, "uViewProjection");
        _lightTintLocation = _gl.GetUniformLocation(_program, "uLightTint");
        _viewportWidth = Math.Max(window.Size.X, 0);
        _viewportHeight = Math.Max(window.Size.Y, 0);
        UpdateMeshes();
        _gpuAgentMeshes.AddRange(
            AgentVisualMeshBuilder.Build(previewAgents).Select(mesh => UploadMesh(_gl, mesh.Vertices)));
        _selectedAgentIndex = previewAgents.Length == 0 ? -1 : 0;
        _hudMesh = UploadMesh(_gl, [], BufferUsageARB.DynamicDraw);

        _gl.Enable(EnableCap.DepthTest);
        _gl.ClearColor(0.78f, 0.84f, 0.88f, 1f);
    }

    public void OnUpdate(double deltaTime)
    {
        if (_keyboard is null)
        {
            return;
        }

        var delta = (float)Math.Clamp(deltaTime, 0d, 0.1d);
        if (ticksPerSecond > 0d)
        {
            _tickAccumulator += delta * ticksPerSecond;
            var advance = (long)Math.Floor(_tickAccumulator);
            if (advance > 0)
            {
                _tick += advance;
                _tickAccumulator -= advance;
                _time = ViewerDayNightCycle.Evaluate(_tick, calendar);
            }
        }

        if (_keyboard.IsKeyPressed(Key.Escape))
        {
            window.Close();
        }

        var orbitKeyDown = _keyboard.IsKeyPressed(Key.O);
        if (orbitKeyDown && !_orbitKeyDown)
        {
            _camera.SetMode(ViewerCameraMode.Orbit);
        }

        _orbitKeyDown = orbitKeyDown;
        var freeKeyDown = _keyboard.IsKeyPressed(Key.F);
        if (freeKeyDown && !_freeKeyDown)
        {
            _camera.SetMode(ViewerCameraMode.Free);
        }

        _freeKeyDown = freeKeyDown;
        var overlayKeyDown = _keyboard.IsKeyPressed(Key.V);
        if (overlayKeyDown && !_overlayKeyDown)
        {
            _zoneOverlay = !_zoneOverlay;
            _mesher.InvalidateAll();
            ClearGpuTerrainMeshes();
        }

        _overlayKeyDown = overlayKeyDown;
        var agentSelectKeyDown = _keyboard.IsKeyPressed(Key.Tab);
        if (agentSelectKeyDown && !_agentSelectKeyDown && previewAgents.Length > 0)
        {
            _selectedAgentIndex = (_selectedAgentIndex + 1) % previewAgents.Length;
            if (_camera.FollowedAgentId.HasValue)
            {
                FollowSelectedAgent();
            }
        }

        _agentSelectKeyDown = agentSelectKeyDown;
        var followKeyDown = _keyboard.IsKeyPressed(Key.C);
        if (followKeyDown && !_followKeyDown && _selectedAgentIndex >= 0)
        {
            if (_camera.FollowedAgentId == previewAgents[_selectedAgentIndex].AgentId)
            {
                _camera.StopFollowingAgent();
            }
            else
            {
                FollowSelectedAgent();
            }
        }

        _followKeyDown = followKeyDown;

        var movement = Vector3.Zero;
        if (_keyboard.IsKeyPressed(Key.W)) movement.Y += 1;
        if (_keyboard.IsKeyPressed(Key.S)) movement.Y -= 1;
        if (_keyboard.IsKeyPressed(Key.D)) movement.X += 1;
        if (_keyboard.IsKeyPressed(Key.A)) movement.X -= 1;
        if (_keyboard.IsKeyPressed(Key.E)) movement.Z += 1;
        if (_keyboard.IsKeyPressed(Key.Q)) movement.Z -= 1;
        _camera.Move(movement, delta);
        UpdateMeshes();

        var rotationSpeed = 1.3f * delta;
        if (_keyboard.IsKeyPressed(Key.Left)) _camera.Rotate(-rotationSpeed, 0);
        if (_keyboard.IsKeyPressed(Key.Right)) _camera.Rotate(rotationSpeed, 0);
        if (_keyboard.IsKeyPressed(Key.Up)) _camera.Rotate(0, rotationSpeed);
        if (_keyboard.IsKeyPressed(Key.Down)) _camera.Rotate(0, -rotationSpeed);
    }

    public unsafe void OnRender(double deltaTime)
    {
        if (_gl is null)
        {
            return;
        }

        _gl.ClearColor(_time.SkyR, _time.SkyG, _time.SkyB, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.UseProgram(_program);
        var aspect = window.Size.Y <= 0 ? 1f : (float)window.Size.X / window.Size.Y;
        var viewProjection = _camera.CreateViewProjection(aspect);
        _gl.UniformMatrix4(_viewProjectionLocation, 1, false, (float*)&viewProjection);
        _gl.Uniform3(_lightTintLocation, _time.LightR, _time.LightG, _time.LightB);
        foreach (var mesh in _gpuMeshes.Values)
        {
            var (minimum, maximum) = ViewerFrustumCuller.GetRegionBounds(world, mesh.RegionId);
            if (!ViewerFrustumCuller.IsVisible(minimum, maximum, viewProjection))
            {
                continue;
            }

            _gl.BindVertexArray(mesh.VertexArray);
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)mesh.VertexCount);
        }

        foreach (var mesh in _gpuAgentMeshes)
        {
            _gl.BindVertexArray(mesh.VertexArray);
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)mesh.VertexCount);
        }

        if (_viewportWidth > 0 && _viewportHeight > 0)
        {
            var hudVertices = ViewerHudMeshBuilder.Build(
                new ViewerHudState(
                    seed,
                    world.Width,
                    world.Height,
                    previewAgents.Length,
                    _camera.Mode,
                    _zoneOverlay,
                    _selectedAgentIndex >= 0 ? previewAgents[_selectedAgentIndex] : null,
                    _camera.FollowedAgentId,
                    _tick,
                    _time,
                    false),
                _viewportWidth,
                _viewportHeight);
            _gl.Disable(EnableCap.DepthTest);
            var identity = Matrix4x4.Identity;
            _gl.UniformMatrix4(_viewProjectionLocation, 1, false, (float*)&identity);
            _gl.Uniform3(_lightTintLocation, 1f, 1f, 1f);
            _gl.BindVertexArray(_hudMesh.VertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _hudMesh.Buffer);
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(hudVertices.Length * sizeof(float)),
                hudVertices,
                BufferUsageARB.DynamicDraw);
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)(hudVertices.Length / 6));
            _gl.Enable(EnableCap.DepthTest);
        }
    }

    public void OnResize(Vector2D<int> size)
    {
        _viewportWidth = Math.Max(size.X, 0);
        _viewportHeight = Math.Max(size.Y, 0);
        _gl?.Viewport(0, 0, (uint)_viewportWidth, (uint)_viewportHeight);
    }

    public void OnClosing()
    {
        if (_gl is null)
        {
            return;
        }

        foreach (var mesh in _gpuMeshes.Values)
        {
            _gl.DeleteBuffer(mesh.Buffer);
            _gl.DeleteVertexArray(mesh.VertexArray);
        }

        foreach (var mesh in _gpuAgentMeshes)
        {
            _gl.DeleteBuffer(mesh.Buffer);
            _gl.DeleteVertexArray(mesh.VertexArray);
        }

        if (_hudMesh.VertexArray != 0)
        {
            _gl.DeleteBuffer(_hudMesh.Buffer);
            _gl.DeleteVertexArray(_hudMesh.VertexArray);
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        _input?.Dispose();
        _gl.Dispose();
    }

    private void UpdateMeshes()
    {
        if (_gl is null)
        {
            return;
        }

        foreach (var mesh in _mesher.Update(
                     world,
                     zones,
                     palette with { ZoneOverlay = _zoneOverlay },
                     MeshBudgetPerFrame))
        {
            if (_gpuMeshes.Remove(mesh.RegionId, out var previous))
            {
                _gl.DeleteBuffer(previous.Buffer);
                _gl.DeleteVertexArray(previous.VertexArray);
            }

            _gpuMeshes.Add(mesh.RegionId, UploadMesh(_gl, mesh.Vertices, regionId: mesh.RegionId));
        }
    }

    private void ClearGpuTerrainMeshes()
    {
        if (_gl is null)
        {
            return;
        }

        foreach (var mesh in _gpuMeshes.Values)
        {
            _gl.DeleteBuffer(mesh.Buffer);
            _gl.DeleteVertexArray(mesh.VertexArray);
        }

        _gpuMeshes.Clear();
    }

    private void FollowSelectedAgent()
    {
        var agent = previewAgents[_selectedAgentIndex];
        _camera.FollowAgent(
            agent.AgentId,
            new Vector3(
                agent.Position.X / 1000f,
                agent.Position.Y / 1000f,
                agent.Position.Z / 1000f + 0.9f));
    }

    private static GpuMesh UploadMesh(
        GL gl,
        float[] vertices,
        BufferUsageARB usage = BufferUsageARB.StaticDraw,
        int regionId = -1)
    {
        var vertexArray = gl.GenVertexArray();
        var buffer = gl.GenBuffer();
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(vertices.Length * sizeof(float)),
            vertices,
            usage);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        return new GpuMesh(vertexArray, buffer, vertices.Length / 6, regionId);
    }

    private static uint CreateProgram(GL gl)
    {
        const string vertexSource = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec3 aColor;
            uniform mat4 uViewProjection;
            out vec3 vColor;
            out vec3 vWorldPosition;
            void main()
            {
                vColor = aColor;
                vWorldPosition = aPosition;
                gl_Position = uViewProjection * vec4(aPosition, 1.0);
            }
            """;
        const string fragmentSource = """
            #version 330 core
            in vec3 vColor;
            in vec3 vWorldPosition;
            uniform vec3 uLightTint;
            out vec4 FragColor;
            float materialGrain(vec3 position)
            {
                vec3 cell = floor(position * 3.0);
                float noise = fract(sin(dot(cell, vec3(127.1, 311.7, 74.7))) * 43758.5453);
                return mix(0.94, 1.04, noise);
            }
            void main()
            {
                FragColor = vec4(vColor * materialGrain(vWorldPosition) * uLightTint, 1.0);
            }
            """;

        var vertexShader = CompileShader(gl, ShaderType.VertexShader, vertexSource);
        var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
        if (linked == 0)
        {
            var log = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"OpenGL shader program failed to link: {log}");
        }

        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            var log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"OpenGL {type} shader failed to compile: {log}");
        }

        return shader;
    }

    private sealed record GpuMesh(uint VertexArray, uint Buffer, int VertexCount, int RegionId = -1);
}
