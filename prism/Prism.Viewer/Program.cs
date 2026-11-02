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
    Console.WriteLine("  --help           Show this help");
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
var meshes = TerrainMeshBuilder.BuildRegions(
    generated.Grid,
    generated.Zones,
    new TerrainMaterialPalette(1, 2, 3, 4, 5, 6));

var windowOptions = WindowOptions.Default;
windowOptions.API = new GraphicsAPI(
    ContextAPI.OpenGL,
    ContextProfile.Core,
    ContextFlags.Default,
    new APIVersion(3, 3));
windowOptions.Size = new Vector2D<int>(1280, 800);
windowOptions.Title = $"PRISM viewer — seed {options.Seed} — WASD/QE move, arrows look";
windowOptions.FramesPerSecond = 60;
windowOptions.UpdatesPerSecond = 60;
using var window = Window.Create(windowOptions);
var viewer = new ViewerWindow(window, generated.Grid, meshes);
window.Load += viewer.OnLoad;
window.Update += viewer.OnUpdate;
window.Render += viewer.OnRender;
window.FramebufferResize += viewer.OnResize;
window.Closing += viewer.OnClosing;
window.Run();

internal sealed record ViewerOptions(ulong Seed, int Size, bool ShowHelp)
{
    public static ViewerOptions Parse(string[] args)
    {
        ulong seed = 12_345;
        var size = 96;
        var showHelp = false;
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
                default:
                    throw new ArgumentException($"Unknown or invalid argument: {args[index]}");
            }
        }

        if (size is < 16 or > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "World size must be between 16 and 512.");
        }

        return new ViewerOptions(seed, size, showHelp);
    }
}

internal sealed class ViewerWindow(
    IWindow window,
    WorldGrid world,
    IReadOnlyList<TerrainRegionMesh> meshes)
{
    private readonly FreeCamera _camera = FreeCamera.LookAt(
        new Vector3(world.Width / 2f, world.Height / 2f, 30f),
        MathF.PI / 4,
        -0.55f,
        Math.Max(world.Width, world.Height) * 1.15f);
    private GL? _gl;
    private IInputContext? _input;
    private IKeyboard? _keyboard;
    private uint _program;
    private int _viewProjectionLocation;
    private readonly List<GpuRegionMesh> _gpuMeshes = [];

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
        foreach (var mesh in meshes)
        {
            _gpuMeshes.Add(UploadMesh(_gl, mesh));
        }

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
        if (_keyboard.IsKeyPressed(Key.Escape))
        {
            window.Close();
        }

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

    public unsafe void OnRender(double deltaTime)
    {
        if (_gl is null)
        {
            return;
        }

        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.UseProgram(_program);
        var aspect = window.Size.Y <= 0 ? 1f : (float)window.Size.X / window.Size.Y;
        var viewProjection = _camera.CreateViewProjection(aspect);
        _gl.UniformMatrix4(_viewProjectionLocation, 1, false, (float*)&viewProjection);
        foreach (var mesh in _gpuMeshes)
        {
            _gl.BindVertexArray(mesh.VertexArray);
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)mesh.VertexCount);
        }
    }

    public void OnResize(Vector2D<int> size) =>
        _gl?.Viewport(0, 0, (uint)Math.Max(size.X, 0), (uint)Math.Max(size.Y, 0));

    public void OnClosing()
    {
        if (_gl is null)
        {
            return;
        }

        foreach (var mesh in _gpuMeshes)
        {
            _gl.DeleteBuffer(mesh.Buffer);
            _gl.DeleteVertexArray(mesh.VertexArray);
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        _input?.Dispose();
        _gl.Dispose();
    }

    private static GpuRegionMesh UploadMesh(GL gl, TerrainRegionMesh mesh)
    {
        var vertexArray = gl.GenVertexArray();
        var buffer = gl.GenBuffer();
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(mesh.Vertices.Length * sizeof(float)),
            mesh.Vertices,
            BufferUsageARB.StaticDraw);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        return new GpuRegionMesh(vertexArray, buffer, mesh.Vertices.Length / 6);
    }

    private static uint CreateProgram(GL gl)
    {
        const string vertexSource = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec3 aColor;
            uniform mat4 uViewProjection;
            out vec3 vColor;
            void main()
            {
                vColor = aColor;
                gl_Position = uViewProjection * vec4(aPosition, 1.0);
            }
            """;
        const string fragmentSource = """
            #version 330 core
            in vec3 vColor;
            out vec4 FragColor;
            void main()
            {
                FragColor = vec4(vColor, 1.0);
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

    private sealed record GpuRegionMesh(uint VertexArray, uint Buffer, int VertexCount);
}

internal sealed class FreeCamera(Vector3 position, float yaw, float pitch)
{
    private const float MinimumPitch = -1.52f;
    private const float MaximumPitch = 1.52f;

    public Vector3 Position { get; private set; } = position;
    private float Yaw { get; set; } = yaw;
    private float Pitch { get; set; } = pitch;

    public static FreeCamera LookAt(Vector3 target, float yaw, float pitch, float distance)
    {
        var direction = new Vector3(
            MathF.Cos(yaw) * MathF.Cos(pitch),
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch));
        return new FreeCamera(target - direction * distance, yaw, pitch);
    }

    public void Move(Vector3 input, float deltaTime)
    {
        if (input.LengthSquared() <= 0)
        {
            return;
        }

        input = Vector3.Normalize(input);
        var forward = new Vector3(MathF.Cos(Yaw), MathF.Sin(Yaw), 0);
        var right = new Vector3(-MathF.Sin(Yaw), MathF.Cos(Yaw), 0);
        Position += (forward * input.Y + right * input.X + Vector3.UnitZ * input.Z) * (30f * deltaTime);
    }

    public void Rotate(float yawDelta, float pitchDelta)
    {
        Yaw += yawDelta;
        Pitch = Math.Clamp(Pitch + pitchDelta, MinimumPitch, MaximumPitch);
    }

    public Matrix4x4 CreateViewProjection(float aspectRatio)
    {
        var forward = new Vector3(
            MathF.Cos(Yaw) * MathF.Cos(Pitch),
            MathF.Sin(Yaw) * MathF.Cos(Pitch),
            MathF.Sin(Pitch));
        var view = Matrix4x4.CreateLookAt(Position, Position + forward, Vector3.UnitZ);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, Math.Max(aspectRatio, 0.01f), 0.1f, 2000f);
        return view * projection;
    }
}
