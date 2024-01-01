using Rasterization;
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
        public List<Vector3[,]> Diffuse = new(15);
        public List<Vector3[,]> Normals = new(15);
        public List<Vector3[,]> MRAO = new(15);
        public List<Vector3[,]> Emission = new(15);
        public List<Vector3[,]> Trasmission = new(15);

        public List<Vector3[,]> ClearCoat = new(15);
        public List<Vector3[,]> ClearCoatRoughness = new(15);
        public List<Vector3[,]> ClearCoatNormals = new(15);

        public float Pm = 0;
        public float Pr = 1;
        public Vector3 Kd = Vector3.Zero;

        public static bool UsingMIPMapping = false;

        private List<Vector3[,]> CalculateMIP(Pbgra32Bitmap src, bool useSrgbToLinearTransform = false, bool isNormal = false)
        {
            List<Vector3[,]> lvls = new(15);

            Vector3[,] mainLvl = new Vector3[src.PixelHeight, src.PixelWidth];
            for (int row = 0; row < src.PixelHeight; row++)
            {
                for (int col = 0; col < src.PixelWidth; col++)
                {
                    mainLvl[row, col] = useSrgbToLinearTransform ? ToneMapping.SrgbToLinear(src.GetPixel(col, row)) : src.GetPixel(col, row);
                    if (isNormal)
                        mainLvl[row, col] = 2 * mainLvl[row, col] - Vector3.One;
                }
            }

            lvls.Add(mainLvl);

            int sizeW = src.PixelWidth;
            int sizeH = src.PixelHeight;
            int currentLvl = 0;

            do
            {
                Vector3[,] nextLvl = new Vector3[sizeH / 2, sizeW / 2];
                for (int row = 0; row < lvls[currentLvl].GetLength(0); row += 2)
                {
                    for (int col = 0; col < lvls[currentLvl].GetLength(1); col += 2)
                    {
                        Vector3 color = lvls[currentLvl][row, col]
                        + lvls[currentLvl][row + 1, col]
                        + lvls[currentLvl][row, col + 1]
                        + lvls[currentLvl][row + 1, col + 1];
                        color /= 4;
                        nextLvl[row / 2, col / 2] = color;
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

        private static Vector3 GetColor(Vector3[,] src, Vector2 uv)
        {
            float u = uv.X * src.GetLength(1) - 0.5f;
            float v = uv.Y * src.GetLength(0) - 0.5f;

            int x0 = (int)float.Floor(u);
            int y0 = (int)float.Floor(v);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float u_ratio = u - x0;
            float v_ratio = v - y0;

            x0 &= (src.GetLength(1) - 1);
            x1 &= (src.GetLength(1) - 1);

            y0 &= (src.GetLength(0) - 1);
            y1 &= (src.GetLength(0) - 1);

            return Vector3.Lerp(
                Vector3.Lerp(src[y0, x0], src[y0, x1], u_ratio),
                Vector3.Lerp(src[y1, x0], src[y1, x1], u_ratio),
                v_ratio
            );
        }

        private Vector3 GetColorFromTexture(List<Vector3[,]> src, Vector2 uv, Vector3 def, Vector2 uvx, Vector2 uvy)
        {
            if (src.Count == 0)
                return def;

            if (UsingMIPMapping)
            {
                Vector2 size = new(src[0].GetLength(1), src[0].GetLength(0));

                Vector2 duvdx = (uvx - uv) * size;
                Vector2 duvdy = (uvy - uv) * size;

                float lvl = float.Clamp(0.5f * float.Log2(float.Max(Vector2.Dot(duvdx, duvdx), Vector2.Dot(duvdy, duvdy))), 0, src.Count - 1);

                int mainLvl = (int)lvl;
                int nextLvl = int.Min(mainLvl + 1, src.Count - 1);

                Vector3 mainColor = GetColor(src[mainLvl], uv);

                Vector3 nextColor = GetColor(src[nextLvl], uv);

                return Vector3.Lerp(mainColor, nextColor, lvl - mainLvl);
            }
            else
            {
                return GetColor(src[0], uv);
            }
        }

        public Vector3 GetDiffuse(Vector2 uv, Vector2 uvx, Vector2 uvy)
        {
            return GetColorFromTexture(Diffuse, uv, ToneMapping.SrgbToLinear(Kd), uvx, uvy);
        }

        public Vector3 GetEmission(Vector2 uv, Vector2 uvx, Vector2 uvy)
        {
            return GetColorFromTexture(Emission, uv, Vector3.Zero, uvx, uvy);
        }

        public float GetTrasmission(Vector2 uv, Vector2 uvx, Vector2 uvy)
        {
            return GetColorFromTexture(Trasmission, uv, Vector3.Zero, uvx, uvy).X;
        }

        public (float, float, Vector3) GetClearCoat(Vector2 uv, Vector3 defaultNormal, Vector2 uvx, Vector2 uvy)
        {
            float roughness = GetColorFromTexture(ClearCoatRoughness, uv, Vector3.Zero, uvx, uvy).X;
            float clearCoat = GetColorFromTexture(ClearCoat, uv, Vector3.Zero, uvx, uvy).X;
            Vector3 normal = GetColorFromTexture(ClearCoatNormals, uv, defaultNormal, uvx, uvy);

            return (roughness, clearCoat, normal);
        }

        public Vector3 GetNormal(Vector2 uv, Vector3 defaultNormal, Vector2 uvx, Vector2 uvy)
        {
            
            return GetColorFromTexture(Normals, uv, defaultNormal, uvx, uvy);
        }

        public Vector3 GetMRAO(Vector2 uv, Vector2 uvx, Vector2 uvy)
        {
            return GetColorFromTexture(MRAO, uv, new(Pm, Pr, 1), uvx, uvy);
        }
    }
}
