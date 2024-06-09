using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Documents;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1.Shadow
{
    public class RTX
    {
        private static Random random = new Random();

        public static int RayCount = 1;
        public static float LightSize = 0.005f;

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

            if (lamp.Type == LampTypes.Directional)
                return BVH.IntersectBVH(orig, Normalize(lamp.Position), PositiveInfinity, 0) ? 0 : 1;

            Vector3 position = lamp.Position;

            for (int j = 0; j < RayCount; j++)
            {
                float phi = random.NextSingle() * 2 * Pi;
                float theta = Acos(random.NextSingle() * 2 - 1);

                Vector3 LP = new(
                    position.X + LightSize * Sin(theta) * Cos(phi),
                    position.Y + LightSize * Sin(theta) * Sin(phi),
                    position.Z + LightSize * Cos(theta)
                );

                Vector3 dir = Normalize(LP - orig);
                float dist = Distance(LP, orig);

                result += BVH.IntersectBVH(orig, dir, dist, 0) ? 0 : baseIntensity;
            }

            return result;
        }

        public static float GetLightIntensity(Vector3 light, Vector3 orig)
        {
            if (BVH.Tris == null) return 1f;

            float result = 0;

            float baseIntensity = 1f / RayCount;

            for (int j = 0; j < RayCount; j++)
            {
                bool intersected = false;

                float phi = random.NextSingle() * 2 * Pi;
                float theta = Acos(random.NextSingle() * 2 - 1);

                Vector3 LP = new(
                    light.X + LightSize * Sin(theta) * Cos(phi),
                    light.Y + LightSize * Sin(theta) * Sin(phi),
                    light.Z + LightSize * Cos(theta)
                );

                Vector3 dir = Normalize(LP - orig);
                float dist = Distance(LP, orig);

                for (int i = 0; i < BVH.Tris.Length; i++)
                {
                    float d = RTX.IntersectTriangle(orig, dir, BVH.Tris[i].v0, BVH.Tris[i].v1, BVH.Tris[i].v2);
                    if (d > -0 && d < dist)
                    {
                        intersected = true;
                        break;
                    }
                }

                result += intersected ? 0 : baseIntensity;
            }

            return result;
        }

        public static float GetLightIntensitySquare(Vector3 light, Vector3 orig)
        {
            if (BVH.Tris == null) return 1f;

            float result = 0;

            float baseIntensity = 1f / RayCount;

            Vector3 v1 = new(light.X - LightSize / 2, light.Y, light.Z + LightSize / 2);
            Vector3 v2 = new(light.X - LightSize / 2, light.Y, light.Z - LightSize / 2);
            Vector3 v3 = new(light.X + LightSize / 2, light.Y, light.Z - LightSize / 2);
            Vector3 v4 = new(light.X - LightSize / 2, light.Y, light.Z + LightSize / 2);

            for (int j = 0; j < RayCount; j++)
            {
                bool intersected = false;

                float t1 = random.NextSingle();
                float t2 = random.NextSingle();

                Vector3 a = (1 - t1) * v1 + t1 * v2;
                Vector3 b = (1 - t1) * v4 + t1 * v3;

                Vector3 LP = (1 - t2) * a + t2 * b;

                Vector3 dir = Normalize(LP - orig);
                Vector3 N = Normalize(LP - new Vector3(LP.X, LP.Y - 1, LP.Z));
                float dist = Distance(LP, orig);

                for (int i = 0; i < BVH.Tris.Length; i++)
                {
                    float d = RTX.IntersectTriangle(orig, dir, BVH.Tris[i].v0, BVH.Tris[i].v1, BVH.Tris[i].v2);
                    if (d > -0 && d < dist)
                    {
                        intersected = true;
                        break;
                    }
                }

                result += intersected ? 0 : baseIntensity * Max(Dot(N, dir), 0);
            }

            return result;
        }
    }
}
