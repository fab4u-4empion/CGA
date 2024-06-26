﻿using System.Numerics;
using System.Windows.Media;
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

        public static Color Vector3ToColor(Vector3 color)
        {
            color *= 255f;
            return Color.FromRgb((byte)color.X, (byte)color.Y, (byte)color.Z);
        }

        public static Vector3 ColorToVector3(Color color)
        {
            return new Vector3(color.R, color.G, color.B) / 255f;
        }

        public static (double, double) R2(double seed, int n)
        {
            double x = (seed + n * 0.75487766624669276005) % 1;
            double y = (seed + n * 0.56984029099805326591) % 1;
            return (x, y);
        }
    }
}