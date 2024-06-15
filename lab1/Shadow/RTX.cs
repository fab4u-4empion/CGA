using System;
using System.Numerics;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1.Shadow
{
    public class RTX
    {
        public static int RayCount = 1;
        public static float LightSize = 0f;
        public static float Angle = 10f;

        private static Vector3 SphericalToCartesian(float phi, float theta, float radius)
        {
            float projection = Sin(theta);

            return new Vector3(Sin(phi) * projection, Cos(theta), Cos(phi) * projection) * radius;
        }

        public static bool IntersectAABB(Vector3 O, Vector3 D, Vector3 bmin, Vector3 bmax)
        {
            Vector3 t1 = (bmin - O) / D;
            Vector3 t2 = (bmax - O) / D;
            float tmin = Max(Max(Min(t1.X, t2.X), Min(t1.Y, t2.Y)), Min(t1.Z, t2.Z));
            float tmax = Min(Min(Max(t1.X, t2.X), Max(t1.Y, t2.Y)), Max(t1.Z, t2.Z));
            return tmax >= tmin && tmax > 0;
        }

        public static float IntersectTriangle(Vector3 orig, Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a;
            Vector3 e2 = c - a;

            Vector3 pvec = Cross(dir, e2);
            float det = Dot(e1, pvec);

            if (det < 1e-8f && det > -1e-8f)
            {
                return 0;
            }

            float inv_det = 1 / det;
            Vector3 tvec = orig - a;
            float u = Dot(tvec, pvec) * inv_det;

            if (u < 0 || u > 1)
            {
                return 0;
            }

            Vector3 qvec = Cross(tvec, e1);
            float v = Dot(dir, qvec) * inv_det;

            if (v < 0 || u + v > 1)
            {
                return 0;
            }

            return Dot(e2, qvec) * inv_det;
        }

        public static float GetLightIntensityBVH(Lamp lamp, Vector3 orig) {
            float result = 0;

            float baseIntensity = 1f / RayCount;

            Vector3 position = lamp.Position;

            Vector3 baseDirection = lamp.GetDirection();

            float cos = 1 - Cos(DegreesToRadians(Angle * 0.5f));

            for (int j = 0; j < RayCount; j++)
            {
                Vector3 dir = Zero;
                float dist = 0;

                if (lamp.Type == LampTypes.Point)
                {
                    float phi = (2 * Pi) * Random.Shared.NextSingle();
                    float theta = Acos(Random.Shared.NextSingle() * 2 - 1);

                    Vector3 LP = position + SphericalToCartesian(phi, theta, LightSize);

                    dir = Normalize(LP - orig);
                    dist = Distance(LP, orig);
                }

                if (lamp.Type == LampTypes.Directional)
                {
                    float phi = (2 * Pi) * Random.Shared.NextSingle();
                    float theta = Acos(1 - cos * Random.Shared.NextSingle());

                    dir = SphericalToCartesian(phi, theta, 1);

                    Vector3 xAxis = Cross(baseDirection, UnitZ);
                    xAxis = xAxis.Equals(Zero) ? UnitX : Normalize(xAxis);
                    Vector3 zAxis = Cross(xAxis, baseDirection);

                    dir = xAxis * dir.X + baseDirection * dir.Y + zAxis * dir.Z;
                    dist = PositiveInfinity;
                }

                result += BVH.IntersectBVH(orig, dir, dist, 0) ? 0 : baseIntensity;
            }

            return result;
        }
    }
}
