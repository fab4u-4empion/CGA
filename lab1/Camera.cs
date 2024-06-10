using System.Numerics;
using static System.Single;

namespace lab1
{
    public enum CameraMode { Arcball, Free }

    public class Camera
    {
        public Vector3 Target { get; set; } = Vector3.Zero;
        public Vector3 Position { get; set; }
        public Vector3 Up { get; set; } = Vector3.UnitY;
        public float FoV { get; set; } = float.Pi / 4;
        public Vector3 LookVector { get; set; }
        public CameraMode Mode { get; set; } = CameraMode.Arcball;

        public float Yaw { get => DegreesToRadians(f); }
        public float Pitch { get => DegreesToRadians(o - 90); }

        private float r = 25;
        private float o = 90;
        private float f = 0;

        public Camera() { 
            Position = GetPosition();
            LookVector = GetLookVector();
        }

        private Vector3 GetPosition() {
            return new(
                r * Sin(DegreesToRadians(o)) * Sin(DegreesToRadians(f)),
                r * Cos(DegreesToRadians(o)),
                r * Sin(DegreesToRadians(o)) * Cos(DegreesToRadians(f))
            );
        }

        private Vector3 GetLookVector() { 
            return Vector3.Normalize(Target - Position);
        }

        public void UpdatePosition(float dR, float dO, float dF) {
            r = Max(r + dR, 0.1f);
            o = Min(179, Max(1, o + dO)); 
            f += dF;

            if (Mode == CameraMode.Arcball)
            {
                Position = GetPosition() + Target;
                LookVector = GetLookVector();
            }
            else
            {
                Target = -GetPosition() + Position;
                LookVector = -GetLookVector();
            }
        }

        public void Move(Vector3 delta, bool rotate)
        {
            if (rotate)
            {
                Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(this.Yaw, this.Pitch, 0);
                delta = Vector3.Transform(delta, rotation);
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

        public void Reset()
        {
            r = 25;
            o = 90;
            f = 0;
            Mode = CameraMode.Arcball;
            UpdatePosition(0, 0, 0);
        }
    }
}
