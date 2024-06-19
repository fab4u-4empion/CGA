using System.Numerics;
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

        public static float Smoothstep(float a, float b, float x)
        {
            float t = Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}