using System.Numerics;
using static lab1.Utils;
using static System.Numerics.Matrix4x4;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public enum CameraMode { Arcball, Free }

    public class Camera
    {
        public Vector3 Target { get; set; } = Zero;
        public Vector3 Position { get; set; }
        public Vector3 Up { get; set; } = UnitY;
        public float FoV { get; set; } = Pi / 4f;
        public CameraMode Mode { get; set; } = CameraMode.Arcball;

        public float Yaw { get => DegreesToRadians(f); }
        public float Pitch { get => DegreesToRadians(o - 90); }

        private float r = 25;
        private float o = 90;
        private float f = 0;

        public Camera()
        {
            Position = GetPosition();
        }

        private Vector3 GetPosition()
        {
            return SphericalToCartesian(DegreesToRadians(f), DegreesToRadians(o), r);
        }

        public void UpdatePosition(float dR, float dO, float dF)
        {
            r = Max(r + dR, 0.1f);
            o = Min(179, Max(1, o + dO));
            f += dF;

            if (Mode == CameraMode.Arcball)
                Position = GetPosition() + Target;
            else
                Target = -GetPosition() + Position;
        }

        public void Move(Vector3 delta, bool rotate)
        {
            if (rotate)
            {
                Matrix4x4 rotation = CreateFromYawPitchRoll(Yaw, Pitch, 0);
                delta = Transform(delta, rotation);
            }

            if (Mode == CameraMode.Arcball)
            {
                Target += delta;
                Position = GetPosition() + Target;
            }
            else
            {
                Position += delta;
                Target = -GetPosition() + Position;
            }
        }

        public void Reset(Vector3 target)
        {
            r = 25;
            o = 90;
            f = 0;
            Mode = CameraMode.Arcball;
            FoV = Pi / 4f;
            Target = target;
            UpdatePosition(0, 0, 0);
        }
    }
}