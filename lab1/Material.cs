using Rasterization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows.Media.Imaging;

namespace lab1
{
    public class Material
    {
        public List<Pbgra32Bitmap> Diffuse = new(15);
        public List<Pbgra32Bitmap> Normals = new(15);
        public List<Pbgra32Bitmap> MRAO = new(15);
        public List<Pbgra32Bitmap> Emission = new(15);
        public List<Pbgra32Bitmap> Trasmission = new(15);

        public List<Pbgra32Bitmap> ClearCoat = new(15);
        public List<Pbgra32Bitmap> ClearCoatRoughness = new(15);
        public List<Pbgra32Bitmap> ClearCoatNormals = new(15);

        public float Pm = 0;
        public float Pr = 1;
        public Vector3 Kd = Vector3.Zero;

        public static bool UsingBilinearFilter = false;

        private List<Pbgra32Bitmap> CalculateMIP(Pbgra32Bitmap src)
        {
            List<Pbgra32Bitmap> lvls = new(15) { src };

            int sizeW = lvls[0].PixelWidth;
            int sizeH = lvls[0].PixelHeight;
            int currentLvl = 0;

            do
            {
                Pbgra32Bitmap nextLvl = new(sizeW / 2, sizeH / 2);
                for (int row = 0; row < sizeH; row += 2)
                {
                    for (int col = 0; col < sizeW; col += 2)
                    {
                        Vector3 color = lvls[currentLvl].GetPixel(col, row)
                            + lvls[currentLvl].GetPixel(col + 1, row)
                            + lvls[currentLvl].GetPixel(col, row + 1)
                            + lvls[currentLvl].GetPixel(col + 1, row + 1);
                        color /= 4;
                        nextLvl.SetPixel(col / 2, row / 2, color);
                    }
                }
                lvls.Add(nextLvl);
                currentLvl++;
                sizeW /= 2;
                sizeH /= 2;
            } while (sizeW > 2 && sizeH > 2);

            return lvls;
        }

        public void AddDiffuse(Pbgra32Bitmap src)
        {
            Diffuse.AddRange(CalculateMIP(src));
        }

        public void AddNormals(Pbgra32Bitmap src)
        {
            Normals.AddRange(CalculateMIP(src));
        }

        public void AddMRAO(Pbgra32Bitmap src)
        {
            MRAO.AddRange(CalculateMIP(src));
        }

        public void AddEmission(Pbgra32Bitmap src)
        {
            Emission.AddRange(CalculateMIP(src));
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
            ClearCoatNormals.AddRange(CalculateMIP(src));
        }

        private Vector3 GetColorFromTexture(List<Pbgra32Bitmap> src, Vector2 uv, Vector3 def, Vector2 uvx, Vector2 uvy)
        {
            if (src.Count == 0)
                return def;

            Vector2 xUV = new(uvx.X * src[0].PixelWidth, uvx.Y * src[0].PixelHeight);
            Vector2 yUV = new(uvy.X * src[0].PixelWidth, uvy.Y * src[0].PixelHeight);
            Vector2 origUV = new(uv.X * src[0].PixelWidth, uv.Y * src[0].PixelHeight);

            Vector2 duvdx = xUV - origUV;
            Vector2 duvdy = yUV - origUV;

            float lvl = float.Clamp(0.5f * float.Log2(float.Max(Vector2.Dot(duvdx, duvdx), Vector2.Dot(duvdy, duvdy))), 0, src.Count - 1);

            int mainLvl = (int)float.Floor(lvl);

            float u = uv.X * (src[mainLvl].PixelWidth - 1);
            float v = uv.Y * (src[mainLvl].PixelHeight - 1);

            int x = (int)float.Floor(u);
            int y = (int)float.Floor(v);

            float u_ratio = u - x;
            float v_ratio = v - y;

            return Vector3.Lerp(
                    Vector3.Lerp(src[mainLvl].GetPixel(x, y), src[mainLvl].GetPixel(x + 1, y), u_ratio),
                    Vector3.Lerp(src[mainLvl].GetPixel(x, y + 1), src[mainLvl].GetPixel(x + 1, y + 1), u_ratio),
                    v_ratio
                );
        }

        public Vector3 GetDiffuse(Vector2 uv, Vector2 uvx, Vector2 uvy)
        {
            return GetColorFromTexture(Diffuse, uv, Kd, uvx, uvy);
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

            return (roughness, clearCoat, 2 * normal - Vector3.One);
        }

        public Vector3 GetNormal(Vector2 uv, Vector3 defaultNormal, Vector2 uvx, Vector2 uvy)
        {
            
            return 2 * GetColorFromTexture(Normals, uv, (defaultNormal + Vector3.One) / 2, uvx, uvy) - Vector3.One;
        }

        public Vector3 GetMRAO(Vector2 uv, Vector2 uvx, Vector2 uvy)
        {
            return GetColorFromTexture(MRAO, uv, new(Pm, Pr, 1), uvx, uvy);
        }
    }
}
