using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Viewer;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

var options = ViewerOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("PRISM viewer");
    Console.WriteLine("  --seed <uint64>  World seed (default: 12345)");
    Console.WriteLine("  --size <int>     Horizontal world size, 16-512 (default: 96)");
    Console.WriteLine("  --day-length <ticks>   Ticks per in-world day (default: 480)");
    Console.WriteLine("  --season-length <days> Days per season (default: 4)");
    Console.WriteLine("  --ticks-per-second <n> Clock speed, 0 pauses (default: 20)");
    Console.WriteLine("  --tick <long>    Starting preview tick (default: 0)");
    Console.WriteLine("  --no-demo-agents Hide the generated agent previews");
    Console.WriteLine("  --connect <url>  Live client of a host read stream (ws://host:port/data/read)");
    Console.WriteLine("  --embedded       Host a world in-process and consume its own read stream");
    Console.WriteLine("  --content <dir>  Content pack directory for --embedded");
    Console.WriteLine("  --replay <file>  Replay a recorded read stream (one envelope per line)");
    Console.WriteLine("  --record <file>  Record the read stream consumed in --connect mode");
    Console.WriteLine("  --interventions <file>  Enable journaled interventions (place/remove blocks)");
    Console.WriteLine("  --intervention-agent <id>  Agent id that carries the intervention commands");
    Console.WriteLine("  --control <url>  Host control endpoint: interventions and Play/Pause/Stop (http://host:port)");
    Console.WriteLine("  --cut <z>        Initial horizontal section plane (hidden above z)");
    Console.WriteLine("  --benchmark-frames <n>  Render n frames, write timings, exit (B8)");
    Console.WriteLine("  --benchmark-output <file>  Benchmark JSON output (default: B8-viewer.json)");
    Console.WriteLine("  --screenshot <file>  Capture one frame to a PNG file, then exit");
    Console.WriteLine("  --screenshot-frames <n>  Frames rendered before the capture (default: 3)");
    Console.WriteLine("  --assets <dir>  Assets directory (default: assets/ next to the viewer)");
    Console.WriteLine("  --help           Show this help");
    Console.WriteLine("Mouse: left drag orbits, right/middle drag pans, wheel zooms, F12 captures");
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

var mode = options.ReplayPath is not null
    ? ViewerMode.Replay
    : options.Embedded
        ? ViewerMode.Embedded
        : options.ConnectUrl is not null
            ? ViewerMode.Live
            : ViewerMode.Preview;

var (initialGrid, initialZones) = ViewerWorldFactory.CreateGenerated(options.Size, options.Seed);
// Le monde d'aperçu n'a pas de host : arbre, feuillage et minerai reçoivent des
// identifiants locaux pour rester lisibles. Un monde live n'utilise que les matières du pack.
var fallbackPalette = new TerrainMaterialPalette(
    1, 2, 3, 4, 5, 6,
    Tree: ViewerWorldFactory.TreeMaterial,
    Leaves: ViewerWorldFactory.LeavesMaterial,
    Ore: ViewerWorldFactory.OreMaterial);
var assets = ViewerAssets.Load(
    options.AssetsDirectory,
    AppContext.BaseDirectory,
    fallbackPalette);
foreach (var warning in assets.Warnings)
{
    Console.Error.WriteLine($"assets : {warning}");
}
var previewAgents = mode == ViewerMode.Preview && options.ShowDemoAgents
    ? CreatePreviewAgents(initialGrid)
    : [];
var calendar = new ViewerCalendarConfig(options.DayLengthTicks, options.SeasonLengthDays);
calendar.Validate();

// Surface de contrôle attendue par le Launcher (endpoint `control` du manifeste) :
// santé et arrêt propre, sans exposer le moindre pilotage du monde.
using var controlServer = options.ControlPort is { } controlPort
    ? new ViewerControlServer(
        controlPort,
        Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN"),
        options.InstanceId ?? "prism-viewer",
        Uri.TryCreate(options.ControlUrl, UriKind.Absolute, out var hostControl) ? hostControl : null)
    : null;
controlServer?.Start();

var windowOptions = WindowOptions.Default;
windowOptions.API = new GraphicsAPI(
    ContextAPI.OpenGL,
    ContextProfile.Core,
    ContextFlags.Default,
    new APIVersion(3, 3));
windowOptions.Size = new Vector2D<int>(1280, 800);
windowOptions.Title =
    $"PRISM viewer — {mode.ToString().ToLowerInvariant()} — O orbit, F free, WASD move, arrows look";
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
    var viewer = new ViewerWindow(
        window,
        options,
        mode,
        initialGrid,
        initialZones,
        assets,
        previewAgents,
        calendar);
    window.Load += () =>
    {
        viewer.OnLoad();
        controlServer?.MarkReady();
    };
    window.Update += deltaSeconds =>
    {
        viewer.OnUpdate(deltaSeconds);
        // Fermeture demandée par HTTP : Close() doit rester sur le fil de la boucle
        // de rendu, sinon GLFW est démonté depuis un thread de requête (crash).
        if (controlServer?.ShutdownRequested.IsCompleted == true)
        {
            window.Close();
        }
    };
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

internal enum ViewerMode
{
    Preview,
    Live,
    Embedded,
    Replay,
}

internal sealed record ViewerOptions(
    ulong Seed,
    int Size,
    bool ShowHelp,
    bool ShowDemoAgents,
    int DayLengthTicks,
    int SeasonLengthDays,
    long StartTick,
    double TicksPerSecond,
    string? ConnectUrl,
    bool Embedded,
    string? ContentDirectory,
    string? ReplayPath,
    string? RecordPath,
    string? InterventionJournal,
    ulong InterventionAgentId,
    string? ControlUrl,
    float? CutZ,
    int BenchmarkFrames,
    string? BenchmarkOutput,
    string? ScreenshotPath,
    int ScreenshotFrames,
    string? AssetsDirectory,
    int? ControlPort,
    string? InstanceId)
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
        string? connectUrl = null;
        var embedded = false;
        string? contentDirectory = null;
        string? replayPath = null;
        string? recordPath = null;
        string? interventionJournal = null;
        ulong interventionAgentId = 1;
        string? controlUrl = null;
        float? cutZ = null;
        var benchmarkFrames = 0;
        string? benchmarkOutput = null;
        string? screenshotPath = null;
        var screenshotFrames = 3;
        string? assetsDirectory = null;
        int? controlPort = null;
        string? instanceId = null;
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
                case "--connect" when index + 1 < args.Length:
                    connectUrl = args[index + 1];
                    index++;
                    break;
                case "--embedded":
                    embedded = true;
                    break;
                case "--content" when index + 1 < args.Length:
                    contentDirectory = args[index + 1];
                    index++;
                    break;
                case "--replay" when index + 1 < args.Length:
                    replayPath = args[index + 1];
                    index++;
                    break;
                case "--record" when index + 1 < args.Length:
                    recordPath = args[index + 1];
                    index++;
                    break;
                case "--interventions" when index + 1 < args.Length:
                    interventionJournal = args[index + 1];
                    index++;
                    break;
                case "--intervention-agent" when index + 1 < args.Length &&
                                                ulong.TryParse(args[index + 1], out var parsedAgent):
                    interventionAgentId = parsedAgent;
                    index++;
                    break;
                case "--control" when index + 1 < args.Length:
                    controlUrl = args[index + 1];
                    index++;
                    break;
                case "--cut" when index + 1 < args.Length &&
                                 float.TryParse(
                                     args[index + 1],
                                     System.Globalization.NumberStyles.Float,
                                     System.Globalization.CultureInfo.InvariantCulture,
                                     out var parsedCut):
                    cutZ = parsedCut;
                    index++;
                    break;
                case "--benchmark-frames" when index + 1 < args.Length &&
                                              int.TryParse(args[index + 1], out var parsedFrames):
                    benchmarkFrames = parsedFrames;
                    index++;
                    break;
                case "--benchmark-output" when index + 1 < args.Length:
                    benchmarkOutput = args[index + 1];
                    index++;
                    break;
                case "--screenshot" when index + 1 < args.Length:
                    screenshotPath = args[index + 1];
                    index++;
                    break;
                case "--screenshot-frames" when index + 1 < args.Length &&
                                                 int.TryParse(args[index + 1], out var parsedShotFrames):
                    screenshotFrames = parsedShotFrames;
                    index++;
                    break;
                case "--assets" when index + 1 < args.Length:
                    assetsDirectory = args[index + 1];
                    index++;
                    break;
                // Arguments communs du Launcher : le viewer n'a pas besoin du
                // répertoire de travail ni du corrélateur, mais il doit les tolérer.
                case "--control-port" when index + 1 < args.Length &&
                                           int.TryParse(args[index + 1], out var parsedControlPort) &&
                                           parsedControlPort is > 0 and < 65536:
                    controlPort = parsedControlPort;
                    index++;
                    break;
                case "--instance-id" when index + 1 < args.Length:
                    instanceId = args[index + 1];
                    index++;
                    break;
                case "--work-dir" or "--log-dir" or "--correlation-id"
                    when index + 1 < args.Length:
                    index++;
                    break;
                case "--headless":
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

        if (benchmarkFrames < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Benchmark frames must be non-negative.");
        }

        if (screenshotFrames < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(args),
                "Screenshot frames must be positive.");
        }

        var sources = (connectUrl is null ? 0 : 1) + (embedded ? 1 : 0) + (replayPath is null ? 0 : 1);
        if (sources > 1)
        {
            throw new ArgumentException(
                "Choose one source: --connect, --embedded or --replay.");
        }

        return new ViewerOptions(
            seed,
            size,
            showHelp,
            showDemoAgents,
            dayLengthTicks,
            seasonLengthDays,
            startTick,
            ticksPerSecond,
            connectUrl,
            embedded,
            contentDirectory,
            replayPath,
            recordPath,
            interventionJournal,
            interventionAgentId,
            controlUrl,
            cutZ,
            benchmarkFrames,
            benchmarkOutput,
            screenshotPath,
            screenshotFrames,
            assetsDirectory,
            controlPort,
            instanceId);
    }
}

internal sealed class ViewerWindow
{
    private const int MeshBudgetPerFrame = 4;
    private const float MaximumCutZ = 512f;

    private readonly IWindow _window;
    private readonly ViewerOptions _options;
    private readonly ViewerMode _mode;
    private readonly ViewerCalendarConfig _calendar;
    private readonly ViewerAssets _assets;
    private readonly ViewerAgentPalette _agentPalette;
    private readonly ViewerHudFont _hudFont;
    private readonly TerrainMaterialPalette _basePalette;
    private readonly IncrementalTerrainMesher _mesher = new();
    private readonly ViewerLiveSession _session = new();
    private readonly ViewerLiveConnection _connection = new();
    private readonly ViewerMovementHeatmap _heatmap;
    private readonly ViewerCamera _camera;
    private readonly Dictionary<int, GpuMesh> _gpuMeshes = [];
    private readonly List<double> _frameTimes = [];
    private readonly HttpClient _http = new();
    private WorldGrid _world;
    private WorldZone[] _zones;
    private AgentEntitySnapshot[] _previewAgents;
    private GeneratedWorldSnapshot? _appliedWorldSnapshot;
    private bool _worldFromHost;
    private ViewerStreamRecorder? _recorder;
    private ViewerReplayCursor? _replay;
    private ViewerEmbeddedHost? _embedded;
    private bool _hostStarted;
    private bool _interventionsEnabled;
    private long _interventionSequence;
    private string _status = "READY";

    private bool _orbitKeyDown;
    private bool _freeKeyDown;
    private bool _overlayKeyDown;
    private bool _agentSelectKeyDown;
    private bool _followKeyDown;
    private bool _cutKeyDown;
    private bool _sliceDownKeyDown;
    private bool _sliceUpKeyDown;
    private bool _placeKeyDown;
    private bool _removeKeyDown;
    private readonly bool[] _layerKeyDown = new bool[7];
    private int _selectedAgentIndex;
    private ulong? _selectedAgentId;
    private float _interpolation = 1f;
    private long _storeRevision = -1;
    private GL? _gl;
    private IInputContext? _input;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private Vector2 _lastMousePosition;
    private float _pendingZoom;
    private string? _screenshotPath;
    private int _screenshotAfterFrames;
    private int _frameIndex;
    private bool _screenshotKeyDown;
    private long _tick;
    private double _tickAccumulator;
    private ViewerDayNightState _time;
    private uint _program;
    private uint _atlasTexture;
    private int _viewProjectionLocation;
    private int _lightTintLocation;
    private int _atlasLocation;
    private int _atlasMixLocation;
    private int _cutEnabledLocation;
    private int _cutZLocation;
    private int _alphaLocation;
    private GpuMesh _hudMesh = new(0, 0, 0, 0);
    private GpuMesh _agentMesh = new(0, 0, 0, 0);
    private GpuMesh _ghostMesh = new(0, 0, 0, 0);
    private GpuMesh _groundMesh = new(0, 0, 0, 0);
    private GpuMesh _heatMesh = new(0, 0, 0, 0);
    private int _viewportWidth;
    private int _viewportHeight;
    private float _cutZ = MaximumCutZ;
    private bool _cutEnabled;

    public ViewerWindow(
        IWindow window,
        ViewerOptions options,
        ViewerMode mode,
        WorldGrid world,
        WorldZone[] zones,
        ViewerAssets assets,
        AgentEntitySnapshot[] previewAgents,
        ViewerCalendarConfig calendar)
    {
        _window = window;
        _options = options;
        _mode = mode;
        _world = world;
        _zones = zones;
        _assets = assets;
        _basePalette = assets.Palette;
        _agentPalette = assets.Agents;
        _hudFont = assets.Font;
        _previewAgents = previewAgents;
        _calendar = calendar;
        _tick = options.StartTick;
        _time = ViewerDayNightCycle.Evaluate(_tick, calendar);
        _heatmap = new ViewerMovementHeatmap(world.Width, world.Height);
        _cutZ = options.CutZ ?? MaximumCutZ;
        _cutEnabled = options.CutZ is not null;
        _camera = ViewerCamera.LookAt(
            new Vector3(world.Width / 2f, world.Height / 2f, 30f),
            MathF.PI / 4,
            -0.55f,
            Math.Max(world.Width, world.Height) * 1.15f);
        _session.Store.ReplaceAgents(previewAgents, options.StartTick);
        _selectedAgentIndex = previewAgents.Length == 0 ? -1 : 0;
        _selectedAgentId = previewAgents.Length == 0 ? null : previewAgents[0].AgentId;
        if (options.InterventionJournal is not null)
        {
            _interventionsEnabled = true;
            _status = "INTERVENTIONS ENABLED";
        }
    }

    public void OnLoad()
    {
        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();
        _keyboard = _input.Keyboards.FirstOrDefault();
        if (_keyboard is null)
        {
            throw new InvalidOperationException("The viewer window did not expose a keyboard.");
        }

        _mouse = _input.Mice.FirstOrDefault();
        if (_mouse is not null)
        {
            _lastMousePosition = _mouse.Position;
            _mouse.Scroll += OnMouseScroll;
        }

        _screenshotPath = _options.ScreenshotPath;
        _screenshotAfterFrames = _options.ScreenshotFrames;
        _program = CreateProgram(_gl);
        _viewProjectionLocation = _gl.GetUniformLocation(_program, "uViewProjection");
        _lightTintLocation = _gl.GetUniformLocation(_program, "uLightTint");
        _atlasLocation = _gl.GetUniformLocation(_program, "uAtlas");
        _atlasMixLocation = _gl.GetUniformLocation(_program, "uAtlasMix");
        _cutEnabledLocation = _gl.GetUniformLocation(_program, "uCutEnabled");
        _cutZLocation = _gl.GetUniformLocation(_program, "uCutZ");
        _alphaLocation = _gl.GetUniformLocation(_program, "uAlpha");
        _atlasTexture = UploadAtlas(_gl, _assets.Atlas);
        _viewportWidth = Math.Max(_window.Size.X, 0);
        _viewportHeight = Math.Max(_window.Size.Y, 0);
        UpdateTerrainMeshes();
        _hudMesh = CreateDynamicMesh(_gl);
        _agentMesh = CreateDynamicMesh(_gl);
        _ghostMesh = CreateDynamicMesh(_gl);
        _groundMesh = CreateDynamicMesh(_gl);
        _heatMesh = CreateDynamicMesh(_gl);

        _gl.Enable(EnableCap.DepthTest);
        _gl.ClearColor(0.78f, 0.84f, 0.88f, 1f);

        StartSource();
    }

    private void StartSource()
    {
        switch (_mode)
        {
            case ViewerMode.Live:
                _recorder = _options.RecordPath is null
                    ? null
                    : new ViewerStreamRecorder(_options.RecordPath);
                _ = ConnectAsync(_options.ConnectUrl!);
                break;
            case ViewerMode.Replay:
                try
                {
                    var envelopes = ViewerReplayFile.Load(_options.ReplayPath!);
                    _replay = new ViewerReplayCursor(envelopes, _options.TicksPerSecond);
                    _status = $"REPLAY: {envelopes.Count} ENVELOPES";
                }
                catch (Exception exception)
                {
                    _status = $"REPLAY ERROR: {exception.Message}";
                }

                break;
            case ViewerMode.Embedded:
                var contentDirectory = _options.ContentDirectory
                    ?? Path.GetFullPath(Path.Combine(
                        AppContext.BaseDirectory, "..", "..", "..", "..", "content", "minimal"));
                _embedded = new ViewerEmbeddedHost(
                    _connection,
                    _options.Seed,
                    contentDirectory,
                    _options.TicksPerSecond);
                try
                {
                    _embedded.Start();
                    _hostStarted = _embedded.Error is null;
                    _status = _hostStarted ? "EMBEDDED HOST RUNNING" : $"HOST ERROR: {_embedded.Error}";
                    if (_embedded.World is { } world)
                    {
                        SwapWorld(world);
                    }
                }
                catch (Exception exception)
                {
                    _status = $"HOST ERROR: {exception.Message}";
                }

                break;
        }
    }

    private async Task ConnectAsync(string url)
    {
        try
        {
            await _connection.StartAsync(new Uri(url), _recorder);
            _status = "CONNECTED";
        }
        catch (Exception exception)
        {
            _status = $"CONNECT ERROR: {exception.Message}";
        }
    }

    public void OnUpdate(double deltaTime)
    {
        if (_keyboard is null)
        {
            return;
        }

        var delta = (float)Math.Clamp(deltaTime, 0d, 0.1d);
        PumpSource(delta);
        AdvanceClock(delta);

        if (_keyboard.IsKeyPressed(Key.Escape))
        {
            _window.Close();
        }

        HandleCameraKeys(delta);
        HandleMouseCamera();
        HandleLayerKeys();
        HandleCutKeys();
        HandleSelectionKeys();
        HandleInterventionKeys();
        HandleScreenshotKey();
        UpdateFollowedAgent();

        if (_session.Store.Revision != _storeRevision)
        {
            _storeRevision = _session.Store.Revision;
            _interpolation = 0f;
        }
        else if (_interpolation < 1f)
        {
            _interpolation = Math.Min(
                1f,
                _interpolation + delta * (float)Math.Max(_options.TicksPerSecond, 1d));
        }

        UpdateTerrainMeshes();
    }

    private void PumpSource(float delta)
    {
        var applied = 0;
        switch (_mode)
        {
            case ViewerMode.Replay:
                if (_replay is null)
                {
                    return;
                }

                foreach (var envelope in _replay.Next(delta))
                {
                    ApplyEnvelope(envelope);
                    applied++;
                }

                if (_replay.Complete && applied == 0)
                {
                    _status = $"REPLAY COMPLETE AT TICK {_tick}";
                }

                break;
            case ViewerMode.Live:
            case ViewerMode.Embedded:
                while (applied < 256 && _connection.TryDequeue(out var envelope))
                {
                    ApplyEnvelope(envelope);
                    applied++;
                }

                if (_connection.Error is { } error)
                {
                    _status = $"STREAM ERROR: {error}";
                }
                else if (_connection.Dropped > 0)
                {
                    _status = $"{_connection.Dropped} DELTAS SKIPPED";
                }

                break;
        }
    }

    private void ApplyEnvelope(ViewerStreamEnvelope envelope)
    {
        var status = _session.Apply(envelope);
        if (status == ViewerApplyStatus.Resynchronised)
        {
            _status = $"RESYNC {_session.Resynchronisations}";
            return;
        }

        if (status != ViewerApplyStatus.Applied)
        {
            return;
        }

        if (envelope.Kind is "world_snapshot" && _session.World is { } snapshot)
        {
            SwapWorld(snapshot);
        }
        else if (envelope.Kind == "region_delta" && _worldFromHost)
        {
            try
            {
                var delta = envelope.Payload.Deserialize<WorldDeltaSnapshot>(ProtocolJson.Options);
                if (delta is not null)
                {
                    ViewerWorldFactory.ApplyDelta(_world, delta);
                }
            }
            catch (JsonException)
            {
                // Un delta illisible est ignoré : le prochain snapshot complet rétablit la vue.
            }
        }
    }

    private void SwapWorld(GeneratedWorldSnapshot snapshot)
    {
        if (ReferenceEquals(_appliedWorldSnapshot, snapshot))
        {
            return;
        }

        (WorldGrid Grid, WorldZone[] Zones) rebuilt;
        try
        {
            rebuilt = ViewerWorldFactory.CreateFromSnapshot(snapshot);
        }
        catch (ArgumentException)
        {
            _status = "WORLD SNAPSHOT INVALID";
            return;
        }

        _appliedWorldSnapshot = snapshot;
        _world = rebuilt.Grid;
        _zones = rebuilt.Zones;
        _worldFromHost = true;
        _mesher.InvalidateAll();
        ClearGpuTerrainMeshes();
        _heatmap.Clear();
        _status = $"WORLD READY {snapshot.Width}X{snapshot.Height}";
    }

    private void AdvanceClock(float delta)
    {
        if (_mode is ViewerMode.Live or ViewerMode.Embedded)
        {
            if (_session.Tick >= 0 && _tick != _session.Tick)
            {
                _tick = _session.Tick;
                _time = ViewerDayNightCycle.Evaluate(_tick, _calendar);
            }

            return;
        }

        if (_options.TicksPerSecond > 0d)
        {
            _tickAccumulator += delta * _options.TicksPerSecond;
            var advance = (long)Math.Floor(_tickAccumulator);
            if (advance > 0)
            {
                _tick += advance;
                _tickAccumulator -= advance;
                _time = ViewerDayNightCycle.Evaluate(_tick, _calendar);
            }
        }
    }

    private void HandleCameraKeys(float delta)
    {
        if (_keyboard is null)
        {
            return;
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
            _session.Store.ToggleLayer(ViewerLayer.Zones);
            InvalidateTerrain();
        }

        _overlayKeyDown = overlayKeyDown;

        var movement = Vector3.Zero;
        if (_keyboard.IsKeyPressed(Key.W)) movement.Y += 1;
        if (_keyboard.IsKeyPressed(Key.S)) movement.Y -= 1;
        if (_keyboard.IsKeyPressed(Key.D)) movement.X += 1;
        if (_keyboard.IsKeyPressed(Key.A)) movement.X -= 1;
        if (_keyboard.IsKeyPressed(Key.E)) movement.Z += 1;
        if (_keyboard.IsKeyPressed(Key.Q)) movement.Z -= 1;
        _camera.Move(movement, delta);

        var rotationSpeed = 1.3f * delta;
        if (_keyboard.IsKeyPressed(Key.Left)) _camera.Rotate(-rotationSpeed, 0);
        if (_keyboard.IsKeyPressed(Key.Right)) _camera.Rotate(rotationSpeed, 0);
        if (_keyboard.IsKeyPressed(Key.Up)) _camera.Rotate(0, rotationSpeed);
        if (_keyboard.IsKeyPressed(Key.Down)) _camera.Rotate(0, -rotationSpeed);
    }

    private void HandleLayerKeys()
    {
        if (_keyboard is null)
        {
            return;
        }

        var layers = Enum.GetValues<ViewerLayer>();
        for (var index = 0; index < _layerKeyDown.Length; index++)
        {
            var key = Key.Number1 + index;
            var down = _keyboard.IsKeyPressed(key);
            if (down && !_layerKeyDown[index])
            {
                if (index < layers.Length)
                {
                    var enabled = _session.Store.ToggleLayer(layers[index]);
                    _status = $"LAYER {layers[index].ToString().ToUpperInvariant()} " +
                              (enabled ? "ON" : "OFF");
                    InvalidateTerrain();
                }
            }

            _layerKeyDown[index] = down;
        }
    }

    private void HandleCutKeys()
    {
        if (_keyboard is null)
        {
            return;
        }

        var cutDown = _keyboard.IsKeyPressed(Key.X);
        if (cutDown && !_cutKeyDown)
        {
            _cutEnabled = !_cutEnabled;
            if (_cutEnabled && _cutZ >= MaximumCutZ)
            {
                _cutZ = _world.VerticalSize * 0.5f;
            }

            // Les blocs enterrés n'apparaissent qu'en coupe : le maillage est reconstruit.
            InvalidateTerrain();
        }

        _cutKeyDown = cutDown;
        var sliceDown = _keyboard.IsKeyPressed(Key.LeftBracket);
        if (sliceDown && !_sliceDownKeyDown)
        {
            _cutEnabled = true;
            _cutZ = Math.Clamp(_cutZ - 2f, 0f, MaximumCutZ);
        }

        _sliceDownKeyDown = sliceDown;
        var sliceUp = _keyboard.IsKeyPressed(Key.RightBracket);
        if (sliceUp && !_sliceUpKeyDown)
        {
            _cutEnabled = true;
            _cutZ = Math.Clamp(_cutZ + 2f, 0f, MaximumCutZ);
        }

        _sliceUpKeyDown = sliceUp;
    }

    private void HandleSelectionKeys()
    {
        if (_keyboard is null)
        {
            return;
        }

        var agentIds = _session.Store.AgentIds;
        var agentSelectKeyDown = _keyboard.IsKeyPressed(Key.Tab);
        if (agentSelectKeyDown && !_agentSelectKeyDown && agentIds.Count > 0)
        {
            var current = _selectedAgentId is { } selected
                ? agentIds.ToList().IndexOf(selected)
                : -1;
            _selectedAgentIndex = (current + 1) % agentIds.Count;
            _selectedAgentId = agentIds[_selectedAgentIndex];
            if (_camera.FollowedAgentId.HasValue)
            {
                UpdateFollowedAgent();
            }
        }

        _agentSelectKeyDown = agentSelectKeyDown;
        var followKeyDown = _keyboard.IsKeyPressed(Key.C);
        if (followKeyDown && !_followKeyDown && _selectedAgentId is { } agentId)
        {
            if (_camera.FollowedAgentId == agentId)
            {
                _camera.StopFollowingAgent();
                _status = "CAMERA FOLLOW OFF";
            }
            else if (UpdateFollowedAgent())
            {
                _status = $"FOLLOWING {agentId}";
            }
            else
            {
                _status = $"AGENT {agentId} NOT VISIBLE";
            }
        }

        _followKeyDown = followKeyDown;
    }

    private bool UpdateFollowedAgent()
    {
        if (_camera.FollowedAgentId is not { } followedAgentId)
        {
            return false;
        }

        if (!_session.Store.TryGetAgent(followedAgentId, out var sample, _interpolation) ||
            sample is null)
        {
            return false;
        }

        return _camera.UpdateFollowedAgent(
            followedAgentId,
            new Vector3(sample.X, sample.Y, sample.Z + 0.9f));
    }

    private void HandleInterventionKeys()
    {
        if (_keyboard is null || !_interventionsEnabled)
        {
            return;
        }

        var placeDown = _keyboard.IsKeyPressed(Key.P);
        if (placeDown && !_placeKeyDown)
        {
            ApplyIntervention(ViewerInterventionKind.Place);
        }

        _placeKeyDown = placeDown;
        var removeDown = _keyboard.IsKeyPressed(Key.R);
        if (removeDown && !_removeKeyDown)
        {
            ApplyIntervention(ViewerInterventionKind.Remove);
        }

        _removeKeyDown = removeDown;
    }

    private void ApplyIntervention(ViewerInterventionKind kind)
    {
        var x = Math.Clamp((int)MathF.Floor(_camera.Target.X), 0, _world.Width - 1);
        var y = Math.Clamp((int)MathF.Floor(_camera.Target.Y), 0, _world.Height - 1);
        var coordinate = kind == ViewerInterventionKind.Place
            ? new BlockCoordinate(x, y, _world.GetSurfaceColumn(x, y).SurfaceZ + 1)
            : FindTopOverride(x, y);
        if (coordinate is null)
        {
            _status = "REMOVE: NO OVERRIDE AT TARGET";
            return;
        }

        var intervention = new ViewerIntervention(
            ++_interventionSequence,
            _tick,
            kind,
            coordinate.Value,
            ItemTypeId: 1,
            MaterialId: 3);
        ViewerInterventionJournal.Append(_options.InterventionJournal!, intervention);
        if (kind == ViewerInterventionKind.Place)
        {
            _world.SetBlockOverride(coordinate.Value, new BlockOverride(3, 0, IsAir: false));
        }
        else
        {
            _world.RemoveBlockOverride(coordinate.Value);
        }

        _status = $"{intervention.KindName} {coordinate.Value.X},{coordinate.Value.Y},{coordinate.Value.Z} JOURNALED";
        var command = ViewerInterventionJournal.CreateCommand(
            intervention,
            _tick + 1,
            _options.InterventionAgentId);
        SubmitIntervention(intervention, command);
    }

    private BlockCoordinate? FindTopOverride(int x, int y)
    {
        var count = _world.GetColumnOverrideCount(x, y);
        for (var index = count - 1; index >= 0; index--)
        {
            var coordinate = _world.GetColumnOverrideAt(x, y, index);
            if (!_world.TryGetBlockOverride(coordinate, out var block) || block.IsAir)
            {
                continue;
            }

            return coordinate;
        }

        return null;
    }

    private void SubmitIntervention(ViewerIntervention intervention, CommandEnvelope command)
    {
        if (_options.ControlUrl is not { } control)
        {
            if (_embedded is not null && _hostStarted)
            {
                try
                {
                    _embedded.QueueIntervention(command);
                    _status = $"{intervention.KindName} QUEUED AT NEXT TICK";
                }
                catch (Exception exception)
                {
                    _status = $"INTERVENTION REFUSED: {exception.Message}";
                }
            }

            return;
        }

        var request = new
        {
            agentId = command.AgentId,
            kind = intervention.KindName,
            x = intervention.Coordinate.X,
            y = intervention.Coordinate.Y,
            z = intervention.Coordinate.Z,
            itemTypeId = intervention.ItemTypeId,
            materialId = intervention.MaterialId,
        };
        _ = Task.Run(async () =>
        {
            try
            {
                var body = JsonSerializer.Serialize(request, ProtocolJson.Options);
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(
                    $"{control.TrimEnd('/')}/control/intervention",
                    content);
                _status = response.IsSuccessStatusCode
                    ? $"{intervention.KindName} ACCEPTED BY HOST"
                    : $"{intervention.KindName} REFUSED {(int)response.StatusCode}";
            }
            catch (Exception exception)
            {
                _status = $"INTERVENTION ERROR: {exception.Message}";
            }
        });
    }

    public unsafe void OnRender(double deltaTime)
    {
        if (_gl is null)
        {
            return;
        }

        _frameIndex++;

        var samples = _session.Store.SampleAgents(_mode == ViewerMode.Preview ? 1f : _interpolation);
        _heatmap.Observe(samples);

        var agentVertices = Flatten(AgentVisualMeshBuilder.Build(samples, _agentPalette));
        UploadDynamic(_agentMesh, agentVertices);

        var ghostVertices = ViewerOverlayMeshBuilder.BuildConstructionGhosts(
            _session.Store.ConstructionSites);
        UploadDynamic(_ghostMesh, ghostVertices);

        var groundVertices = ViewerOverlayMeshBuilder.BuildGroundItems(
            _session.Store.GroundItems,
            _session.Store.IsLayerEnabled(ViewerLayer.Freshness));
        UploadDynamic(_groundMesh, groundVertices);

        var heatVertices = _session.Store.IsLayerEnabled(ViewerLayer.Paths)
            ? ViewerOverlayMeshBuilder.BuildHeatmap(_heatmap.Cells, _world, _heatmap.Peak)
            : [];
        UploadDynamic(_heatMesh, heatVertices);

        _gl.ClearColor(_time.SkyR, _time.SkyG, _time.SkyB, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.UseProgram(_program);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _atlasTexture);
        _gl.Uniform1(_atlasLocation, 0);
        var aspect = _window.Size.Y <= 0 ? 1f : (float)_window.Size.X / _window.Size.Y;
        var viewProjection = _camera.CreateViewProjection(aspect);
        _gl.UniformMatrix4(_viewProjectionLocation, 1, false, (float*)&viewProjection);
        _gl.Uniform3(_lightTintLocation, _time.LightR, _time.LightG, _time.LightB);
        // Uniforme déclaré float : l'envoyer en entier (glUniform1i) est invalide et laisse
        // la coupe inactive. Le littéral doit rester flottant.
        _gl.Uniform1(_cutEnabledLocation, _cutEnabled ? 1f : 0f);
        _gl.Uniform1(_cutZLocation, _cutZ);
        _gl.Uniform1(_alphaLocation, 1f);
        _gl.Uniform1(_atlasMixLocation, 1f);

        foreach (var mesh in _gpuMeshes.Values)
        {
            var (minimum, maximum) = ViewerFrustumCuller.GetRegionBounds(_world, mesh.RegionId);
            if (!ViewerFrustumCuller.IsVisible(minimum, maximum, viewProjection))
            {
                continue;
            }

            _gl.BindVertexArray(mesh.VertexArray);
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)mesh.VertexCount);
        }

        _gl.Uniform1(_atlasMixLocation, 0f);
        DrawDynamic(_agentMesh);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthMask(false);
        _gl.Uniform1(_alphaLocation, 0.55f);
        DrawDynamic(_groundMesh);
        _gl.Uniform1(_alphaLocation, 0.45f);
        DrawDynamic(_ghostMesh);
        _gl.Uniform1(_alphaLocation, 0.40f);
        DrawDynamic(_heatMesh);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);

        if (_viewportWidth > 0 && _viewportHeight > 0)
        {
            var selected = BuildSelectedSnapshot();
            var hudVertices = ViewerHudMeshBuilder.Build(
                new ViewerHudState(
                    _options.Seed,
                    _world.Width,
                    _world.Height,
                    _session.Store.AgentCount,
                    _camera.Mode,
                    _session.Store.IsLayerEnabled(ViewerLayer.Zones),
                    selected,
                    _camera.FollowedAgentId,
                    _tick,
                    _time,
                    _mode != ViewerMode.Preview,
                    _mode.ToString().ToLowerInvariant(),
                    _session.Store.TryGetAgent(
                        _selectedAgentId ?? 0,
                        out var actionSample,
                        _interpolation) && actionSample is not null
                        ? actionSample.ActiveAction
                        : null,
                    _session.Store.EnabledLayers.OrderBy(layer => layer).ToArray(),
                    _cutZ,
                    _cutEnabled,
                    _session.WorldHash,
                    _session.Resynchronisations,
                    _status),
                _viewportWidth,
                _viewportHeight,
                _hudFont);
            UploadDynamic(_hudMesh, hudVertices);
            _gl.Disable(EnableCap.DepthTest);
            _gl.Uniform1(_atlasMixLocation, 0f);
            _gl.Uniform1(_cutEnabledLocation, 0f);
            _gl.Uniform1(_alphaLocation, 1f);
            var identity = Matrix4x4.Identity;
            _gl.UniformMatrix4(_viewProjectionLocation, 1, false, (float*)&identity);
            _gl.Uniform3(_lightTintLocation, 1f, 1f, 1f);
            DrawDynamic(_hudMesh);
            _gl.Enable(EnableCap.DepthTest);
        }

        if (_screenshotPath is { } screenshotPath &&
            _frameIndex >= _screenshotAfterFrames)
        {
            _screenshotPath = null;
            CaptureScreenshot(screenshotPath);
        }

        if (_options.BenchmarkFrames > 0)
        {
            _frameTimes.Add(deltaTime);
            if (_frameTimes.Count >= _options.BenchmarkFrames)
            {
                WriteBenchmark();
                _window.Close();
            }
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel) => _pendingZoom += wheel.Y;

    /// <summary>Glisser gauche orbite, glisser droit/milieu déplace la carte, molette zoome.</summary>
    private void HandleMouseCamera()
    {
        if (_mouse is null)
        {
            return;
        }

        var position = _mouse.Position;
        var delta = position - _lastMousePosition;
        _lastMousePosition = position;
        if (_mouse.IsButtonPressed(MouseButton.Left))
        {
            if (delta.LengthSquared() > 0f)
            {
                _camera.Rotate(delta.X * 0.006f, -delta.Y * 0.006f);
            }
        }
        else if (_mouse.IsButtonPressed(MouseButton.Right) ||
                 _mouse.IsButtonPressed(MouseButton.Middle))
        {
            if (delta.LengthSquared() > 0f)
            {
                _camera.Pan(-delta.X * 0.5f, delta.Y * 0.5f);
            }
        }

        if (_pendingZoom != 0f)
        {
            _camera.Zoom(-_pendingZoom * 2f);
            _pendingZoom = 0f;
        }
    }

    private void HandleScreenshotKey()
    {
        if (_keyboard is null)
        {
            return;
        }

        var down = _keyboard.IsKeyPressed(Key.F12);
        if (down && !_screenshotKeyDown)
        {
            _screenshotPath = _options.ScreenshotPath ??
                Path.Combine(Environment.CurrentDirectory, $"viewer-{_tick}.png");
            _screenshotAfterFrames = _frameIndex + 1;
            _status = "SCREENSHOT REQUESTED";
        }

        _screenshotKeyDown = down;
    }

    /// <summary>Lit le tampon de la fenêtre et l'encode en PNG (F12 ou --screenshot).</summary>
    private unsafe void CaptureScreenshot(string path)
    {
        if (_gl is null || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        var width = _viewportWidth;
        var height = _viewportHeight;
        var rgba = new byte[checked(width * height * 4)];
        fixed (byte* pixels = rgba)
        {
            _gl.ReadPixels(
                0,
                0,
                (uint)width,
                (uint)height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);
        }

        try
        {
            var fullPath = ViewerScreenshot.Save(path, width, height, rgba);
            _status = $"SCREENSHOT: {fullPath}";
            Console.WriteLine($"PRISM viewer screenshot written to {fullPath}");
            if (_options.ScreenshotPath is not null)
            {
                _window.Close();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _status = $"SCREENSHOT ERROR: {exception.Message}";
            Console.Error.WriteLine(_status);
        }
    }

    private void DrawDynamic(GpuMesh mesh)
    {
        if (_gl is null || mesh.VertexArray == 0 || mesh.VertexCount == 0)
        {
            return;
        }

        _gl.BindVertexArray(mesh.VertexArray);
        _gl.DrawArrays(GLEnum.Triangles, 0, (uint)mesh.VertexCount);
    }

    private AgentEntitySnapshot? BuildSelectedSnapshot()
    {
        if (_selectedAgentId is not { } agentId)
        {
            return null;
        }

        if (_session.Store.TryGetAgent(agentId, out var sample, _interpolation) && sample is not null)
        {
            return new AgentEntitySnapshot(
                agentId,
                AgentPosition.AtCell(
                    (int)MathF.Floor(sample.X),
                    (int)MathF.Floor(sample.Y),
                    (int)MathF.Floor(sample.Z)),
                new AgentBodyProfile(),
                sample.Inventory);
        }

        return _previewAgents.FirstOrDefault(agent => agent.AgentId == agentId);
    }

    public void OnResize(Vector2D<int> size)
    {
        _viewportWidth = Math.Max(size.X, 0);
        _viewportHeight = Math.Max(size.Y, 0);
        _gl?.Viewport(0, 0, (uint)_viewportWidth, (uint)_viewportHeight);
    }

    public void OnClosing()
    {
        _recorder?.Dispose();
        _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _embedded?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _http.Dispose();
        if (_gl is null)
        {
            return;
        }

        foreach (var mesh in _gpuMeshes.Values)
        {
            _gl.DeleteBuffer(mesh.Buffer);
            _gl.DeleteVertexArray(mesh.VertexArray);
        }

        foreach (var mesh in new[] { _hudMesh, _agentMesh, _ghostMesh, _groundMesh, _heatMesh })
        {
            if (mesh.VertexArray != 0)
            {
                _gl.DeleteBuffer(mesh.Buffer);
                _gl.DeleteVertexArray(mesh.VertexArray);
            }
        }

        if (_atlasTexture != 0)
        {
            _gl.DeleteTexture(_atlasTexture);
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        _input?.Dispose();
        _gl.Dispose();
    }

    private void InvalidateTerrain()
    {
        _mesher.InvalidateAll();
        ClearGpuTerrainMeshes();
    }

    private void UpdateTerrainMeshes()
    {
        if (_gl is null)
        {
            return;
        }

        var layers = _session.Store.EnabledLayers.OrderBy(layer => layer).ToArray();
        var palette = _basePalette with
        {
            ZoneOverlay = _session.Store.IsLayerEnabled(ViewerLayer.Zones),
            ActiveLayers = layers,
            ShowBuried = _cutEnabled,
        };
        foreach (var mesh in _mesher.Update(_world, _zones, palette, MeshBudgetPerFrame))
        {
            if (_gpuMeshes.Remove(mesh.RegionId, out var previous))
            {
                _gl.DeleteBuffer(previous.Buffer);
                _gl.DeleteVertexArray(previous.VertexArray);
            }

            _gpuMeshes.Add(mesh.RegionId, UploadStaticMesh(_gl, mesh.Vertices, mesh.RegionId));
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

    private void WriteBenchmark()
    {
        if (_frameTimes.Count == 0)
        {
            return;
        }

        var sorted = _frameTimes.OrderBy(value => value).ToArray();
        var p50 = sorted[sorted.Length / 2];
        var p95 = sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * 0.95) - 1, 0, sorted.Length - 1)];
        var average = _frameTimes.Average();
        var path = _options.BenchmarkOutput ?? "B8-viewer.json";
        var payload = new
        {
            benchmark = "B8",
            mode = _mode.ToString().ToLowerInvariant(),
            frames = _frameTimes.Count,
            width = _viewportWidth,
            height = _viewportHeight,
            agents = _session.Store.AgentCount,
            regions = _gpuMeshes.Count,
            averageFrameMs = Math.Round(average * 1000d, 2),
            p50FrameMs = Math.Round(p50 * 1000d, 2),
            p95FrameMs = Math.Round(p95 * 1000d, 2),
            framesPerSecond = Math.Round(1d / Math.Max(average, 0.000001d), 2),
            platform = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, ProtocolJson.Options));
        _status = $"B8 WRITTEN: {path}";
        Console.WriteLine($"B8 viewer benchmark written to {path}");
    }

    private static float[] Flatten(IReadOnlyList<AgentVisualMesh> meshes)
    {
        var total = meshes.Sum(mesh => mesh.Vertices.Length);
        var vertices = new float[total];
        var offset = 0;
        foreach (var mesh in meshes)
        {
            mesh.Vertices.CopyTo(vertices, offset);
            offset += mesh.Vertices.Length;
        }

        return vertices;
    }

    private unsafe void UploadDynamic(GpuMesh mesh, float[] vertices)
    {
        if (_gl is null)
        {
            return;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, mesh.Buffer);
        if (vertices.Length == 0)
        {
            mesh.VertexCount = 0;
            return;
        }

        fixed (float* data = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                data,
                BufferUsageARB.DynamicDraw);
        }

        mesh.VertexCount = vertices.Length / TerrainMeshBuilder.FloatsPerVertex;
    }

    private static GpuMesh CreateDynamicMesh(GL gl)
    {
        var vertexArray = gl.GenVertexArray();
        var buffer = gl.GenBuffer();
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
        return new GpuMesh(vertexArray, buffer, 0, 0);
    }

    private static GpuMesh UploadStaticMesh(GL gl, float[] vertices, int regionId)
    {
        var mesh = CreateDynamicMesh(gl);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, mesh.Buffer);
        gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(vertices.Length * sizeof(float)),
            vertices,
            BufferUsageARB.StaticDraw);
        return new GpuMesh(
            mesh.VertexArray,
            mesh.Buffer,
            vertices.Length / TerrainMeshBuilder.FloatsPerVertex,
            regionId);
    }

    private static unsafe uint UploadAtlas(GL gl, ViewerTextureAtlas atlas)
    {
        var texture = gl.GenTexture();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, texture);
        fixed (byte* pixels = atlas.Rgba)
        {
            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                (uint)atlas.Width,
                (uint)atlas.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);
        }

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return texture;
    }

    private static uint CreateProgram(GL gl)
    {
        const string vertexSource = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec3 aColor;
            layout (location = 2) in vec2 aTexture;
            uniform mat4 uViewProjection;
            out vec3 vColor;
            out vec3 vWorldPosition;
            out vec2 vTexture;
            void main()
            {
                vColor = aColor;
                vWorldPosition = aPosition;
                vTexture = aTexture;
                gl_Position = uViewProjection * vec4(aPosition, 1.0);
            }
            """;
        const string fragmentSource = """
            #version 330 core
            in vec3 vColor;
            in vec3 vWorldPosition;
            in vec2 vTexture;
            uniform vec3 uLightTint;
            uniform sampler2D uAtlas;
            uniform float uAtlasMix;
            uniform float uCutEnabled;
            uniform float uCutZ;
            uniform float uAlpha;
            out vec4 FragColor;
            float materialGrain(vec3 position)
            {
                vec3 cell = floor(position * 3.0);
                float noise = fract(sin(dot(cell, vec3(127.1, 311.7, 74.7))) * 43758.5453);
                return mix(0.94, 1.04, noise);
            }
            void main()
            {
                if (uCutEnabled > 0.5 && vWorldPosition.z > uCutZ)
                {
                    discard;
                }
                vec3 tile = mix(vec3(1.0), texture(uAtlas, vTexture).rgb, uAtlasMix);
                FragColor = vec4(vColor * tile * materialGrain(vWorldPosition) * uLightTint, uAlpha);
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

    private sealed class GpuMesh(uint vertexArray, uint buffer, int vertexCount, int regionId)
    {
        public uint VertexArray { get; } = vertexArray;
        public uint Buffer { get; } = buffer;
        public int VertexCount { get; set; } = vertexCount;
        public int RegionId { get; init; } = regionId;
    }
}
