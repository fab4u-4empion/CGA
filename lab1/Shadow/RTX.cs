using System;
using System.Numerics;
using static lab1.HDRTexture;
using static lab1.LightingConfig;
using static lab1.Utils;
using static System.Numerics.Matrix4x4;
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

        private static Vector3 FresnelSchlick(float VdotH, Vector3 F0)
        {
            float t = 1 - VdotH;
            float t2 = t * t;
            float t5 = t2 * t2 * t;
            return F0 + (One - F0) * t5;
        }

        private static float Distribution(float NdotH, float a2)
        {
            float k = NdotH * NdotH * (a2 - 1) + 1;
            return MinNumber(1e8f, a2 / (float.Pi * k * k));
        }

        private static float Visibility(float NdotV, float NdotL, float a2)
        {
            float v = NdotL * Sqrt(NdotV * NdotV * (1 - a2) + a2);
            float l = NdotV * Sqrt(NdotL * NdotL * (1 - a2) + a2);
            return Min(1e8f, 0.5f / (v + l));
        }

        private static bool IntersectGround(Vector3 orig, Vector3 dir)
        {
            float t = (BVH.Nodes![0].aabbMin.Y - orig.Y) / dir.Y;
            return IsFinite(t) && t > 1e-4f && t < RTAORayDistance;
        }

        public static Vector3 GetAmbientOcclusionBVH(
            Vector3 origin,
            Vector3 point,
            Vector3 normal,
            Vector3 camera,
            Vector3 baseColor,
            Vector3 F0,
            float metallic,
            float roughness,
            float opacity,
            bool shadowCatcher)
        {
            Vector3 radiance = Zero;
            Vector3 totalRadiance = Zero;
            Matrix4x4 worldMatrix = CreateWorldMatrix(normal);

            HDRTexture envMap = IBLSpecularMap[0];
            Buffer<float> p = envMap.JointPdf!;
            float[] Pv = envMap.MarginalCdf!;
            Buffer<float> Pu = envMap.ConditionalCdf!;

            float dPhi = float.Tau / envMap.Width;
            float dTheta = float.Pi / envMap.Height;

            F0 = Lerp(F0, baseColor, metallic);
            float a = roughness * roughness, a2 = a * a;
            Vector3 albedo = (1 - metallic) * baseColor;
            Vector3 diffuse = albedo / float.Pi * opacity;

            Vector3 V = Normalize(camera - point);
            float NdotV = Abs(Dot(normal, V));

            float k1 = 0.5f * (1 - roughness) * (1 + metallic);
            float k2 = 0.25f * (1 - metallic + roughness * (1 + metallic));

            for (int i = 0; i < RTAORayCount; i++)
            {
                float u = Random.Shared.NextSingle();
                float v = Random.Shared.NextSingle();
                float w = Random.Shared.NextSingle();

                if (w < k1)
                {
                    float NdotH = Sqrt((1 - v) / (v * (a2 - 1) + 1));
                    float phiH = float.Tau * u;
                    float thetaH = Acos(NdotH);

                    Vector3 H = TransformNormal(SphericalToCartesian(-phiH + Angle, thetaH, 1), worldMatrix);
                    Vector3 L = Reflect(-V, H);

                    if (Dot(normal, L) is float NdotL && NdotL > 0)
                    {
                        bool vis = !BVH.IntersectBVH(origin, L, float.PositiveInfinity, 0) &&
                                   !(DrawGround && IntersectGround(origin, L));

                        if (vis || shadowCatcher)
                        {
                            float VdotH = Abs(Dot(V, H));

                            Vector3 fresnel = FresnelSchlick(VdotH, F0);
                            float distribution = Distribution(NdotH, a2);
                            float visibility = Visibility(NdotV, NdotL, a2);
                            Vector3 specular = One * distribution * visibility;
                            Vector3 brdf = Lerp(diffuse, specular, fresnel);

                            float phi = -Atan2(L.X, L.Z) + Angle;
                            float theta = Acos(Clamp(L.Y, -1, 1));
                            int x = (int)Floor(phi / dPhi) & (envMap.Width - 1);
                            int y = (int)Floor(theta / dTheta) & (envMap.Height - 1);

                            float pdf1 = NdotL / float.Pi;
                            float pdf2 = p[x, y] / Sin(theta);
                            float pdf3 = distribution * NdotH / (4 * Abs(VdotH));
                            Vector3 estimate = brdf * envMap.GetColor(x, y) * NdotL;
                            Vector3 value = estimate / (k2 * (pdf1 + pdf2) + k1 * pdf3);

                            radiance += vis ? value : Zero;
                            totalRadiance += value;
                        }
                    }
                }
                else if (w < k1 + k2)
                {
                    int y = (y = Array.BinarySearch(Pv, v)) < 0 ? ~y - 1 : y;
                    int x = (x = Pu.GetRow(y).BinarySearch(u)) < 0 ? ~x - 1 : x;
                    float theta = (y + (v - Pv[y]) / (Pv[y + 1] - Pv[y])) * dTheta;
                    float phi = (x + (u - Pu[x, y]) / (Pu[x + 1, y] - Pu[x, y])) * dPhi;

                    Vector3 L = SphericalToCartesian(-phi + Angle, theta, 1);

                    if (Dot(normal, L) is float NdotL && NdotL > 0)
                    {
                        bool vis = !BVH.IntersectBVH(origin, L, float.PositiveInfinity, 0) &&
                                   !(DrawGround && IntersectGround(origin, L));

                        if (vis || shadowCatcher)
                        {
                            Vector3 H = Normalize(L + V);

                            float NdotH = Abs(Dot(normal, H));
                            float VdotH = Abs(Dot(V, H));

                            Vector3 fresnel = FresnelSchlick(VdotH, F0);
                            float distribution = Distribution(NdotH, a2);
                            float visibility = Visibility(NdotV, NdotL, a2);
                            Vector3 specular = One * distribution * visibility;
                            Vector3 brdf = Lerp(diffuse, specular, fresnel);

                            float pdf1 = NdotL / float.Pi;
                            float pdf2 = p[x, y] / Sin(theta);
                            float pdf3 = distribution * NdotH / (4 * Abs(VdotH));
                            Vector3 estimate = brdf * envMap.GetColor(x, y) * NdotL;
                            Vector3 value = estimate / (k2 * (pdf1 + pdf2) + k1 * pdf3);

                            radiance += vis ? value : Zero;
                            totalRadiance += value;
                        }
                    }
                }
                else
                {
                    float phiL = float.Tau * u;
                    float thetaL = Asin(Sqrt(v));

                    Vector3 L = TransformNormal(SphericalToCartesian(-phiL + Angle, thetaL, 1), worldMatrix);

                    if (Dot(normal, L) is float NdotL && NdotL > 0)
                    {
                        bool vis = !BVH.IntersectBVH(origin, L, float.PositiveInfinity, 0) &&
                                   !(DrawGround && IntersectGround(origin, L));

                        if (vis || shadowCatcher)
                        {
                            Vector3 H = Normalize(L + V);

                            float NdotH = Abs(Dot(normal, H));
                            float VdotH = Abs(Dot(V, H));

                            Vector3 fresnel = FresnelSchlick(VdotH, F0);
                            float distribution = Distribution(NdotH, a2);
                            float visibility = Visibility(NdotV, NdotL, a2);
                            Vector3 specular = One * distribution * visibility;
                            Vector3 brdf = Lerp(diffuse, specular, fresnel);

                            float phi = -Atan2(L.X, L.Z) + Angle;
                            float theta = Acos(Clamp(L.Y, -1, 1));
                            int x = (int)Floor(phi / dPhi) & (envMap.Width - 1);
                            int y = (int)Floor(theta / dTheta) & (envMap.Height - 1);

                            float pdf1 = NdotL / float.Pi;
                            float pdf2 = p[x, y] / Sin(theta);
                            float pdf3 = distribution * NdotH / (4 * Abs(VdotH));
                            Vector3 estimate = brdf * envMap.GetColor(x, y) * NdotL;
                            Vector3 value = estimate / (k2 * (pdf1 + pdf2) + k1 * pdf3);

                            radiance += vis ? value : Zero;
                            totalRadiance += value;
                        }
                    }
                }
            }

            return shadowCatcher ? Vector3.Create(MaxNumber(0, Min(1, Dot(radiance, One) / Dot(totalRadiance, One)))) : radiance / RTAORayCount;
        }
    }
}