using System.Numerics;

namespace lab1
{
    public class Camera
    {
        public Vector3 Target { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Up { get; set; }
        public float FoV { get; set; }
        public Vector3 LookVector { get; set; }

        public float Yaw { get => DegToRad(f); }
        public float Pitch { get => DegToRad(o - 90); }

        private float r;
        private float o;
        private float f;

        public Camera() { 
            Target = Vector3.Zero;
            r = 25;
            o = 90;
            f = 0;
            Position = GetPosition();
            FoV = float.Pi / 4;
            Up = new Vector3(0, 1, 0);
            LookVector = GetLookVector();
        }

        private float DegToRad(float deg)
        {
            return deg / 180f * float.Pi;
        }

        private Vector3 GetPosition() {
            return new Vector3(
                r * (float)float.Sin(DegToRad(o)) * float.Sin(DegToRad(f)),
                r * (float)float.Cos(DegToRad(o)),
                r * (float)float.Sin(DegToRad(o)) * float.Cos(DegToRad(f))
            );
        }

        private Vector3 GetLookVector() { 
            return Vector3.Normalize(Target - Position);
        }

        public void UpdatePosition(float dR, float dO, float dF) {
            r = float.Max(r + dR, 0.1f);
            o = float.Min(179, float.Max(1, o + dO)); 
            f += dF;
            Position = GetPosition() + Target;
            LookVector = GetLookVector();
        }

        public void Move(Vector3 delta)
        {
            Target += delta;
            Position = GetPosition() + Target;
        }
    }
}
