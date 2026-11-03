using System.Numerics;

namespace Prism.Viewer;

public enum ViewerCameraMode
{
    Orbit,
    Free,
}

public sealed class ViewerCamera
{
    private const float MinimumPitch = -1.52f;
    private const float MaximumPitch = 1.52f;
    private const float MinimumOrbitDistance = 2f;
    private const float MaximumOrbitDistance = 2000f;
    private const float MovementSpeed = 30f;
    private const float ZoomSpeed = 30f;

    private Vector3 _position;
    private Vector3 _target;
    private float _yaw;
    private float _pitch;
    private float _orbitDistance;

    private ViewerCamera(Vector3 target, float yaw, float pitch, float distance)
    {
        _target = target;
        _yaw = yaw;
        _pitch = Math.Clamp(pitch, MinimumPitch, MaximumPitch);
        _orbitDistance = Math.Clamp(distance, MinimumOrbitDistance, MaximumOrbitDistance);
        _position = target - Forward * _orbitDistance;
    }

    public ViewerCameraMode Mode { get; private set; } = ViewerCameraMode.Orbit;
    public Vector3 Position => _position;
    public Vector3 Target => _target;
    public float OrbitDistance => _orbitDistance;
    public ulong? FollowedAgentId { get; private set; }

    public static ViewerCamera LookAt(Vector3 target, float yaw, float pitch, float distance)
    {
        if (!float.IsFinite(target.X) ||
            !float.IsFinite(target.Y) ||
            !float.IsFinite(target.Z) ||
            !float.IsFinite(yaw) ||
            !float.IsFinite(pitch) ||
            !float.IsFinite(distance) ||
            distance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Camera parameters must be finite and distance must be positive.");
        }

        return new ViewerCamera(target, yaw, pitch, distance);
    }

    public void SetMode(ViewerCameraMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (Mode == mode)
        {
            return;
        }

        if (mode == ViewerCameraMode.Orbit)
        {
            _target = _position + Forward * _orbitDistance;
        }
        else
        {
            _position = _target - Forward * _orbitDistance;
        }

        Mode = mode;
    }

    public void FollowAgent(ulong agentId, Vector3 position)
    {
        ValidatePosition(position);
        var offset = position - _target;
        _target = position;
        if (Mode == ViewerCameraMode.Orbit)
        {
            UpdateOrbitPosition();
        }
        else
        {
            _position += offset;
        }

        FollowedAgentId = agentId;
    }

    public bool UpdateFollowedAgent(ulong agentId, Vector3 position)
    {
        ValidatePosition(position);
        if (FollowedAgentId != agentId)
        {
            return false;
        }

        var offset = position - _target;
        _target = position;
        if (Mode == ViewerCameraMode.Orbit)
        {
            UpdateOrbitPosition();
        }
        else
        {
            _position += offset;
        }

        return true;
    }

    public void StopFollowingAgent() => FollowedAgentId = null;

    public void Move(Vector3 input, float deltaTime)
    {
        ValidateDeltaTime(deltaTime);
        if (!IsFinite(input))
        {
            throw new ArgumentOutOfRangeException(nameof(input));
        }

        var horizontal = new Vector2(input.X, input.Y);
        if (horizontal.LengthSquared() > 0)
        {
            horizontal = Vector2.Normalize(horizontal);
            var forward = new Vector3(MathF.Cos(_yaw), MathF.Sin(_yaw), 0);
            var right = new Vector3(-MathF.Sin(_yaw), MathF.Cos(_yaw), 0);
            var offset = (forward * horizontal.Y + right * horizontal.X) * (MovementSpeed * deltaTime);
            if (Mode == ViewerCameraMode.Orbit)
            {
                _target += offset;
                UpdateOrbitPosition();
            }
            else
            {
                _position += offset;
            }
        }

        if (input.Z != 0)
        {
            if (Mode == ViewerCameraMode.Orbit)
            {
                Zoom(input.Z * ZoomSpeed * deltaTime);
            }
            else
            {
                _position += Vector3.UnitZ * input.Z * MovementSpeed * deltaTime;
            }
        }
    }

    public void Rotate(float yawDelta, float pitchDelta)
    {
        if (!float.IsFinite(yawDelta) || !float.IsFinite(pitchDelta))
        {
            throw new ArgumentOutOfRangeException(nameof(yawDelta));
        }

        _yaw += yawDelta;
        _pitch = Math.Clamp(_pitch + pitchDelta, MinimumPitch, MaximumPitch);
        if (Mode == ViewerCameraMode.Orbit)
        {
            UpdateOrbitPosition();
        }
    }

    public void Zoom(float distanceDelta)
    {
        if (!float.IsFinite(distanceDelta))
        {
            throw new ArgumentOutOfRangeException(nameof(distanceDelta));
        }

        if (Mode == ViewerCameraMode.Orbit)
        {
            _orbitDistance = Math.Clamp(
                _orbitDistance + distanceDelta,
                MinimumOrbitDistance,
                MaximumOrbitDistance);
            UpdateOrbitPosition();
        }
        else
        {
            _position -= Forward * distanceDelta;
        }
    }

    public Matrix4x4 CreateViewProjection(float aspectRatio)
    {
        var target = Mode == ViewerCameraMode.Orbit ? _target : _position + Forward;
        var view = Matrix4x4.CreateLookAt(_position, target, Vector3.UnitZ);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3,
            Math.Max(aspectRatio, 0.01f),
            0.1f,
            2000f);
        return view * projection;
    }

    private Vector3 Forward =>
        new(
            MathF.Cos(_yaw) * MathF.Cos(_pitch),
            MathF.Sin(_yaw) * MathF.Cos(_pitch),
            MathF.Sin(_pitch));

    private void UpdateOrbitPosition() => _position = _target - Forward * _orbitDistance;

    private static void ValidateDeltaTime(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void ValidatePosition(Vector3 position)
    {
        if (!IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
    }
}
