using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace lab1
{
    public class Camera
    {
        public Vector3 Target { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Up { get; set; }
        public float FoV { get; set; }

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
        }

        private float DegToRad(float deg)
        {
            return deg / 180f * float.Pi;
        }

        private Vector3 GetPosition() {
            return new Vector3(
                r * (float)Math.Sin(DegToRad(o)) * (float)Math.Sin(DegToRad(f)),
                r * (float)Math.Cos(DegToRad(o)),
                r * (float)Math.Sin(DegToRad(o)) * (float)Math.Cos(DegToRad(f))
            );
        }

        public void UpdatePosition(float dR, float dO, float dF) {
            r = Math.Max(r + dR, 10);
            o = Math.Min(179, Math.Max(1, o + dO)); 
            f += dF;
            Position = GetPosition();
        }
    }
}
