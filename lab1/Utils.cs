using System.Numerics;
using System.Windows.Media;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public static class Utils
    {
        public static Vector3 SphericalToCartesian(float phi, float theta, float radius)
        {
            float projection = Sin(theta);

            return new Vector3(Sin(phi) * projection, Cos(theta), Cos(phi) * projection) * radius;
        }

        public static float PerpDotProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        public static Color Vector3ToColor(Vector3 color)
        {
            color *= 255f;
            return Color.FromRgb((byte)color.X, (byte)color.Y, (byte)color.Z);
        }

        public static Vector3 ColorToVector3(Color color)
        {
            return new Vector3(color.R, color.G, color.B) / 255f;
        }

        public static (double, double) FibonacciLattice(double seed, int i, int n)
        {
            return ((seed + i * 0.61803398874989484821) % 1, (i + 0.5) / n);
        }

        public static Matrix4x4 CreateWorldMatrix(Vector3 yAxis)
        {
            Vector3 xAxis = Cross(yAxis, UnitZ);
            xAxis = xAxis.Equals(Zero) ? UnitX : Normalize(xAxis);
            Vector3 zAxis = Cross(xAxis, yAxis);
            return new(xAxis.X, xAxis.Y, xAxis.Z, 0,
                       yAxis.X, yAxis.Y, yAxis.Z, 0,
                       zAxis.X, zAxis.Y, zAxis.Z, 0,
                       0, 0, 0, 1);
        }
    }
}