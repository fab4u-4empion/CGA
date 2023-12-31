﻿using Rasterization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace lab1
{
    public class Material
    {
        public List<Buffer<Vector3>> Diffuse = new(15);
        public List<Buffer<Vector3>> Normals = new(15);
        public List<Buffer<Vector3>> MRAO = new(15);
        public List<Buffer<Vector3>> Emission = new(15);
        public List<Buffer<Vector3>> Trasmission = new(15);

        public List<Buffer<Vector3>> ClearCoat = new(15);
        public List<Buffer<Vector3>> ClearCoatRoughness = new(15);
        public List<Buffer<Vector3>> ClearCoatNormals = new(15);

        public float Pm = 0;
        public float Pr = 1;
        public Vector3 Kd = Vector3.Zero;

        public static bool UsingMIPMapping = false;
        public static int MaxAnisotropy = 1;

        private List<Buffer<Vector3>> CalculateMIP(Pbgra32Bitmap src, bool useSrgbToLinearTransform = false, bool isNormal = false)
        {
            List<Buffer<Vector3>> lvls = new(15);

            Buffer<Vector3> mainLvl = new(src.PixelWidth, src.PixelHeight);
            for (int x = 0; x < src.PixelWidth; x++)
            {
                for (int y = 0; y < src.PixelHeight; y++)
                {
                    mainLvl[x, y] = useSrgbToLinearTransform ? ToneMapping.SrgbToLinear(src.GetPixel(x, y)) : src.GetPixel(x, y);
                    if (isNormal)
                        mainLvl[x, y] = 2 * mainLvl[x, y] - Vector3.One;
                }
            }

            lvls.Add(mainLvl);

            int sizeW = src.PixelWidth;
            int sizeH = src.PixelHeight;
            int currentLvl = 0;

            do
            {
                Buffer<Vector3> nextLvl = new(sizeW / 2, sizeH / 2);
                for (int x = 0; x < lvls[currentLvl].Width; x += 2)
                {
                    for (int y = 0; y < lvls[currentLvl].Height; y += 2)
                    {
                        Vector3 color = lvls[currentLvl][x, y]
                        + lvls[currentLvl][x + 1, y]
                        + lvls[currentLvl][x, y + 1]
                        + lvls[currentLvl][x + 1, y + 1];
                        color /= 4;
                        nextLvl[x / 2, y / 2] = color;
                    }
                }
                lvls.Add(nextLvl);
                currentLvl++;
                sizeW /= 2;
                sizeH /= 2;
            } while (sizeW > 1 && sizeH > 1);

            return lvls;
        }

        public void AddDiffuse(Pbgra32Bitmap src)
        {
            Diffuse.AddRange(CalculateMIP(src, true));
        }

        public void AddNormals(Pbgra32Bitmap src)
        {
            Normals.AddRange(CalculateMIP(src, false, true));
        }

        public void AddMRAO(Pbgra32Bitmap src)
        {
            MRAO.AddRange(CalculateMIP(src));
        }

        public void AddEmission(Pbgra32Bitmap src)
        {
            Emission.AddRange(CalculateMIP(src, true));
        }

        public void AddTrasmission(Pbgra32Bitmap src)
        {
            Trasmission.AddRange(CalculateMIP(src));
        }

        public void AddClearCoat(Pbgra32Bitmap src)
        {
            ClearCoat.AddRange(CalculateMIP(src));
        }

        public void AddClearCoatRoughness(Pbgra32Bitmap src)
        {
            ClearCoatRoughness.AddRange(CalculateMIP(src));
        }

        public void AddClearCoatNormals(Pbgra32Bitmap src)
        {
            ClearCoatNormals.AddRange(CalculateMIP(src, false, true));
        }

        private static Vector3 GetColor(Buffer<Vector3> src, Vector2 uv)
        {
            float u = uv.X * src.Width - 0.5f;
            float v = uv.Y * src.Height - 0.5f;

            int x0 = (int)float.Floor(u);
            int y0 = (int)float.Floor(v);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float u_ratio = u - x0;
            float v_ratio = v - y0;

            x0 &= (src.Width - 1);
            x1 &= (src.Width - 1);

            y0 &= (src.Height - 1);
            y1 &= (src.Height - 1);

            return Vector3.Lerp(
                Vector3.Lerp(src[x0, y0], src[x1, y1], u_ratio),
                Vector3.Lerp(src[x0, y1], src[x1, y1], u_ratio),
                v_ratio
            );
        }

        private Vector3 GetColorFromTexture(List<Buffer<Vector3>> src, Vector2 uv, Vector3 def, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            if (src.Count == 0)
                return def;

            if (UsingMIPMapping)
            {
                float length1 = ((uv3 - uv1) * src[0].Size).Length();
                float length2 = ((uv4 - uv2) * src[0].Size).Length();

                float max = float.Max(length1, length2);
                float min = float.Min(length1, length2);

                float aniso = float.Min(max / min, MaxAnisotropy);

                int N = (int)float.Round(aniso, MidpointRounding.AwayFromZero);
                float lvl = float.Clamp(float.Log2(max / aniso), 0, src.Count - 1);

                int mainLvl = (int)lvl;
                int nextLvl = int.Min(mainLvl + 1, src.Count - 1);

                (Vector2 a, Vector2 b) = length1 > length2 ? (uv1, uv3) : (uv2, uv4);

                if (N == 1)
                {
                    Vector2 p = (a + b) * 0.5f;

                    return Vector3.Lerp(GetColor(src[mainLvl], p), GetColor(src[nextLvl], p), lvl - mainLvl);
                }
                else
                {
                    Vector2 k = (b - a) / N;
                    a += 0.5f * k;

                    Vector3 mainColor = Vector3.Zero;
                    Vector3 nextColor = Vector3.Zero;

                    for (int i = 0; i < N; i++, a += k)
                    {
                        mainColor += GetColor(src[mainLvl], a);
                        nextColor += GetColor(src[nextLvl], a);
                    }

                    return Vector3.Lerp(mainColor, nextColor, lvl - mainLvl) / N;
                }
            }
            else
            {
                return GetColor(src[0], uv);
            }
        }

        public Vector3 GetDiffuse(Vector2 uv, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            return GetColorFromTexture(Diffuse, uv, ToneMapping.SrgbToLinear(Kd), uv1, uv2, uv3, uv4);
        }

        public Vector3 GetEmission(Vector2 uv, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            return GetColorFromTexture(Emission, uv, Vector3.Zero, uv1, uv2, uv3, uv4);
        }

        public float GetTrasmission(Vector2 uv, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            return GetColorFromTexture(Trasmission, uv, Vector3.Zero, uv1, uv2, uv3, uv4).X;
        }

        public (float, float, Vector3) GetClearCoat(Vector2 uv, Vector3 defaultNormal, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            float roughness = GetColorFromTexture(ClearCoatRoughness, uv, Vector3.Zero, uv1, uv2, uv3, uv4).X;
            float clearCoat = GetColorFromTexture(ClearCoat, uv, Vector3.Zero, uv1, uv2, uv3, uv4).X;
            Vector3 normal = GetColorFromTexture(ClearCoatNormals, uv, defaultNormal, uv1, uv2, uv3, uv4);

            return (roughness, clearCoat, normal);
        }

        public Vector3 GetNormal(Vector2 uv, Vector3 defaultNormal, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            
            return GetColorFromTexture(Normals, uv, defaultNormal, uv1, uv2, uv3, uv4);
        }

        public Vector3 GetMRAO(Vector2 uv, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            return GetColorFromTexture(MRAO, uv, new(Pm, Pr, 1), uv1, uv2, uv3, uv4);
        }
    }
}
