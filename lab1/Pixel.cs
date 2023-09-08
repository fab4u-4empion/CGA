using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace lab1
{
    public class Pixel
    {
        public int X { get; set; }
        public int Y { get; set; }
        public float Z { get; set; }

        public Pixel(Vector4 v) {
            X = (int)v.X;
            Y = (int)v.Y;
            Z = v.Z;
        }

        public Pixel(int x, int y, float z) {
            X = x;
            Y = y;
            Z = z;
        }

        public Pixel Copy() {
            return new(X, Y, Z);
        }
    }
}
