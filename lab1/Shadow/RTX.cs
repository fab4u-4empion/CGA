using System;
using System.Numerics;
using static lab1.Utils;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1.Shadow
{
    public class RTX
    {
        public static int ShadowRayCount { get; set; } = 1;
        public static int RTAORayCount { get; set; } = 1;
        public static float RTAORayDistance { get; set; } = 1;

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

        public static float GetLightIntensityBVH(Lamp lamp, Vector3 orig, Vector3 normal)
        {
            float result = 0, total = 0;
            double seed = Random.Shared.NextDouble();
            Vector3 baseDirection = lamp.GetL(orig);
            Matrix4x4 worldMatrix = CreateWorldMatrix(baseDirection);

            if (lamp.Type == LampType.Point)
            {
                float r = lamp.Radius;
                float d = Distance(orig, lamp.Position);

                if (r > d)
                    return 1;

                float r2 = r * r, d2 = d * d;
                float cosRange = 1 - Sqrt(1 - r2 / d2);

                for (int j = 0; j < ShadowRayCount; j++)
                {
                    (double x, double y) = FibonacciLattice(seed, j, ShadowRayCount);
                    float phi = float.Tau * (float)x, theta = Acos(1 - cosRange * (float)y);
                    Vector3 dir = Transform(SphericalToCartesian(phi, theta, 1), worldMatrix);

                    (float sinTheta, float cosTheta) = SinCos(theta);
                    float dist = d * cosTheta - Sqrt(r2 - d2 * sinTheta * sinTheta);

                    float cosThetaHat = Max(0, Dot(normal, dir));
                    result += BVH.IntersectBVH(orig, dir, dist, 0) ? 0 : cosThetaHat;
                    total += cosThetaHat;
                }
            }
            else
            {
                float cosRange = 1 - Cos(DegreesToRadians(lamp.Angle * 0.5f));

                for (int j = 0; j < ShadowRayCount; j++)
                {
                    (double x, double y) = FibonacciLattice(seed, j, ShadowRayCount);
                    float phi = float.Tau * (float)x, theta = Acos(1 - cosRange * (float)y);
                    Vector3 dir = Transform(SphericalToCartesian(phi, theta, 1), worldMatrix);

                    float cosThetaHat = Max(0, Dot(normal, dir));
                    result += BVH.IntersectBVH(orig, dir, float.PositiveInfinity, 0) ? 0 : cosThetaHat;
                    total += cosThetaHat;
                }
            }

            return MaxNumber(0, Min(1, result / total));
        }

        public static float GetAmbientOcclusionBVH(Vector3 orig, Vector3 normal)
        {
            float result = 0;
            double seed = Random.Shared.NextDouble();
            Matrix4x4 worldMatrix = CreateWorldMatrix(normal);

            for (int j = 0; j < RTAORayCount; j++)
            {
                (double x, double y) = FibonacciLattice(seed, j, RTAORayCount);
                float phi = float.Tau * (float)x, theta = Asin(Sqrt((float)y));
                Vector3 dir = Transform(SphericalToCartesian(phi, theta, 1), worldMatrix);

                bool intersects = BVH.IntersectBVH(orig, dir, RTAORayDistance, 0);

                if (!intersects && LightingConfig.DrawGround)
                {
                    float t = (BVH.Nodes![0].aabbMin.Y - orig.Y) / dir.Y;
                    intersects = IsFinite(t) && t > 1e-4f && t < RTAORayDistance;
                }

                result += intersects ? 0 : 1;
            }

            return result / RTAORayCount;
        }
    }
}