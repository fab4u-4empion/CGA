using Rasterization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static System.Int32;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public enum BlendModes
    {
        Opaque,
        AlphaBlending
    }

    public class Material
    {
        public List<Buffer<Vector3>>? Diffuse = null;
        public List<Buffer<Vector3>>? Normals = null;
        public List<Buffer<Vector3>>? MRAO = null;
        public List<Buffer<Vector3>>? Emission = null;
        public List<Buffer<Vector3>>? Transmission = null;
        public List<Buffer<Vector3>>? Dissolve = null;
        public List<Buffer<Vector3>>? Specular = null;

        public List<Buffer<Vector3>>? ClearCoat = null;
        public List<Buffer<Vector3>>? ClearCoatRoughness = null;
        public List<Buffer<Vector3>>? ClearCoatNormals = null;

        public float Pm = 0;
        public float Pr = 1;
        public float Tr = 0;
        public Vector3 Kd = Zero;
        public Vector3 Ks = One;
        public float Pc = 0;
        public float Pcr = 0;
        public float D = 1;

        public BlendModes BlendMode = BlendModes.Opaque;
        public bool UseORM = false;

        public static bool UsingMIPMapping { get; set; } = true;
        public static int MaxAnisotropy { get; set; } = 16;

        public static List<Buffer<Vector3>> CalculateMIP(Pbgra32Bitmap src, bool useSrgbToLinearTransform = false, bool isNormal = false)
        {
            List<Buffer<Vector3>> lvls = new(15);

            Buffer<Vector3> mainLvl = new(src.PixelWidth, src.PixelHeight);

            Parallel.ForEach(Partitioner.Create(0, src.PixelWidth), (range) =>
            {
                for (int x = range.Item1; x < range.Item2; x++)
                {
                    for (int y = 0; y < src.PixelHeight; y++)
                    {
                        mainLvl[x, y] = useSrgbToLinearTransform ? ToneMapping.SrgbToLinear(src.GetPixel(x, y)) : src.GetPixel(x, y);
                        if (isNormal)
                            mainLvl[x, y] = 2 * mainLvl[x, y] - One;
                    }
                }
            });

            lvls.Add(mainLvl);

            int sizeW = src.PixelWidth;
            int sizeH = src.PixelHeight;
            int currentLvl = 0;

            do
            {
                sizeW /= 2;
                sizeH /= 2;

                Buffer<Vector3> nextLvl = new(sizeW, sizeH);

                Parallel.ForEach(Partitioner.Create(0, nextLvl.Width), (range) =>
                {
                    for (int x = range.Item1; x < range.Item2; x++)
                    {
                        for (int y = 0; y < nextLvl.Height; y++)
                        {
                            int px = x * 2;
                            int py = y * 2;

                            Vector3 color = lvls[currentLvl][px, py]
                                + lvls[currentLvl][px + 1, py]
                                + lvls[currentLvl][px, py + 1]
                                + lvls[currentLvl][px + 1, py + 1];
                            color /= 4;
                            nextLvl[x, y] = color;
                        }
                    }
                });

                lvls.Add(nextLvl);

                currentLvl++;

            } while (sizeW > 1 && sizeH > 1);

            return lvls;
        }

        private static Vector3 GetColor(Buffer<Vector3> src, Vector2 uv)
        {
            float u = uv.X * src.Width - 0.5f;
            float v = uv.Y * src.Height - 0.5f;

            int x0 = (int)Floor(u);
            int y0 = (int)Floor(v);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float u_ratio = u - x0;
            float v_ratio = v - y0;

            x0 &= (src.Width - 1);
            x1 &= (src.Width - 1);

            y0 &= (src.Height - 1);
            y1 &= (src.Height - 1);

            return Lerp(
                Lerp(src[x0, y0], src[x1, y0], u_ratio),
                Lerp(src[x0, y1], src[x1, y1], u_ratio),
                v_ratio
            );
        }

        private static Vector3 GetColorFromTexture(List<Buffer<Vector3>>? src, Vector2 uv, Vector3 def, Vector2 uv1, Vector2 uv2)
        {
            if (src == null)
                return def;

            if (UsingMIPMapping)
            {
                float length1 = ((uv - uv1) * src[0].Size).Length();
                float length2 = ((uv - uv2) * src[0].Size).Length();

                float max = Max(length1, length2);
                float min = Min(length1, length2);

                float aniso = MaxMagnitudeNumber(Min(max / min, MaxAnisotropy), 1);

                int N = (int)Round(aniso, MidpointRounding.AwayFromZero);
                float lvl = Clamp(Log2(max / aniso), 0, src.Count - 1);

                int mainLvl = (int)lvl;
                int nextLvl = Min(mainLvl + 1, src.Count - 1);

                (Vector2 a, Vector2 b) = length1 > length2 ? (uv, uv1) : (uv, uv2);

                if (N == 1)
                {
                    return Lerp(GetColor(src[mainLvl], uv), GetColor(src[nextLvl], uv), lvl - mainLvl);
                }
                else
                {
                    Vector2 k = (b - a) / N;
                    a += 0.5f * (k + a - b);

                    Vector3 mainColor = Zero;
                    Vector3 nextColor = Zero;

                    for (int i = 0; i < N; i++, a += k)
                    {
                        mainColor += GetColor(src[mainLvl], a);
                        nextColor += GetColor(src[nextLvl], a);
                    }

                    return Lerp(mainColor, nextColor, lvl - mainLvl) / N;
                }
            }
            else
            {
                return GetColor(src[0], uv);
            }
        }

        public Vector3 GetDiffuse(Vector2 uv, Vector2 uv1, Vector2 uv2)
        {
            return GetColorFromTexture(Diffuse, uv, Kd, uv1, uv2);
        }

        public Vector3 GetSpecular(Vector2 uv, Vector2 uv1, Vector2 uv2)
        {
            return GetColorFromTexture(Specular, uv, Ks, uv1, uv2);
        }

        public Vector3 GetEmission(Vector2 uv, Vector2 uv1, Vector2 uv2)
        {
            return GetColorFromTexture(Emission, uv, Zero, uv1, uv2);
        }

        public float GetTransmission(Vector2 uv, Vector2 uv1, Vector2 uv2)
        {
            return GetColorFromTexture(Transmission, uv, new(Tr), uv1, uv2).X;
        }

        public float GetDissolve(Vector2 uv, Vector2 uv1, Vector2 uv2)
        {
            return GetColorFromTexture(Dissolve, uv, new(D), uv1, uv2).X;
        }

        public (float, float, Vector3) GetClearCoat(Vector2 uv, Vector3 defaultNormal, Vector2 uv1, Vector2 uv2)
        {
            float roughness = GetColorFromTexture(ClearCoatRoughness, uv, new(Pcr), uv1, uv2).X;
            float clearCoat = GetColorFromTexture(ClearCoat, uv, new(Pc), uv1, uv2).X;
            Vector3 normal = GetColorFromTexture(ClearCoatNormals, uv, defaultNormal, uv1, uv2);

            return (roughness, clearCoat, normal);
        }

        public Vector3 GetNormal(Vector2 uv, Vector3 defaultNormal, Vector2 uv1, Vector2 uv2)
        {
            return GetColorFromTexture(Normals, uv, defaultNormal, uv1, uv2);
        }

        public Vector3 GetMRAO(Vector2 uv, Vector2 uv1, Vector2 uv2)
        {
            if (MRAO == null)
                return new(Pm, Pr, 1);

            Vector3 mrao = GetColorFromTexture(MRAO, uv, Zero, uv1, uv2);

            if (UseORM)
                return new(mrao.Z, mrao.Y, mrao.X);

            return mrao;
        }
    }
}
