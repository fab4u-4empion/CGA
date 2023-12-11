using Rasterization;
using System.Numerics;

namespace lab1
{
    public class Material
    {
        public Pbgra32Bitmap? Diffuse = null;
        public Pbgra32Bitmap? Normals = null;
        public Pbgra32Bitmap? MRAO = null;
        public Pbgra32Bitmap? Emission = null;
        public Pbgra32Bitmap? Trasmission = null;

        public Pbgra32Bitmap? ClearCoat = null;
        public Pbgra32Bitmap? ClearCoatRoughness = null;
        public Pbgra32Bitmap? ClearCoatNormals = null;

        public float Pm = 0;
        public float Pr = 1;
        public Vector3 Kd = Vector3.Zero;

        public static bool UsingBilinearFilter = false;

        public Vector3 GetDiffuse(float u, float v)
        {
            if (Diffuse == null)
                return Kd;
            if (UsingBilinearFilter)
            {
                u *= Diffuse.PixelWidth;
                v *= Diffuse.PixelHeight;
                int x = (int)float.Floor(u);
                int y = (int)float.Floor(v);
                float u_ratio = u - x;
                float v_ratio = v - y;
                float u_opp = 1 - u_ratio;
                float v_opp = 1 - v_ratio;
                Vector3 cXY = Diffuse.GetPixel(x, y);
                Vector3 cX1Y = Diffuse.GetPixel(x + 1, y);
                Vector3 cXY1 = Diffuse.GetPixel(x, y + 1);
                Vector3 cX1Y1 = Diffuse.GetPixel(x + 1, y + 1);
                return (cXY * u_opp + cX1Y * u_ratio) * v_opp + (cXY1 * u_opp + cX1Y1 * u_ratio) * v_ratio;
            }
            return Diffuse.GetPixel((int)(u * Diffuse.PixelWidth), (int)(v * Diffuse.PixelHeight));
        }

        public Vector3 GetEmission(float u, float v)
        {
            return Emission == null ? Vector3.Zero : Emission.GetPixel((int)(u * Emission.PixelWidth), (int)(v * Emission.PixelHeight));
        }

        public float GetTrasmission(float u, float v)
        {
            return Trasmission == null ? 0 : Trasmission.GetPixel((int)(u * Trasmission.PixelWidth), (int)(v * Trasmission.PixelHeight)).X;
        }

        public (float, float, Vector3) GetClearCoat(float u, float v, Vector3 defaultNormal)
        {
            float roughness = ClearCoatRoughness == null ? 0 : ClearCoatRoughness.GetPixel((int)(u * ClearCoatRoughness.PixelWidth), (int)(v * ClearCoatRoughness.PixelHeight)).X;
            float clearCoat = ClearCoat == null ? 0 : ClearCoat.GetPixel((int)(u * ClearCoat.PixelWidth), (int)(v * ClearCoat.PixelHeight)).X;
            Vector3 normal = ClearCoatNormals == null ? defaultNormal : 2 * ClearCoatNormals.GetPixel((int)(u * ClearCoatNormals.PixelWidth), (int)(v * ClearCoatNormals.PixelHeight)) - Vector3.One;
            return (roughness, clearCoat, normal);
        }

        public Vector3 GetNormal(float u, float v, Vector3 defaultNormal)
        {
            if (Normals == null)
                return defaultNormal;
            if (UsingBilinearFilter)
            {
                u *= Normals.PixelWidth;
                v *= Normals.PixelHeight;
                int x = (int)float.Floor(u);
                int y = (int)float.Floor(v);
                float u_ratio = u - x;
                float v_ratio = v - y;
                float u_opp = 1 - u_ratio;
                float v_opp = 1 - v_ratio;
                Vector3 cXY = Normals.GetPixel(x, y);
                Vector3 cX1Y = Normals.GetPixel(x + 1, y);
                Vector3 cXY1 = Normals.GetPixel(x, y + 1);
                Vector3 cX1Y1 = Normals.GetPixel(x + 1, y + 1);
                return 2 * ((cXY * u_opp + cX1Y * u_ratio) * v_opp + (cXY1 * u_opp + cX1Y1 * u_ratio) * v_ratio) - Vector3.One;
            }
            return 2 * Normals.GetPixel((int)(u * Normals.PixelWidth), (int)(v * Normals.PixelHeight)) - Vector3.One;
        }

        public Vector3 GetMRAO(float u, float v)
        {
            if (MRAO == null)
                return new(Pm, Pr, 1);
            if (UsingBilinearFilter)
            {
                u *= MRAO.PixelWidth;
                v *= MRAO.PixelHeight;
                int x = (int)float.Floor(u);
                int y = (int)float.Floor(v);
                float u_ratio = u - x;
                float v_ratio = v - y;
                float u_opp = 1 - u_ratio;
                float v_opp = 1 - v_ratio;
                Vector3 cXY = MRAO.GetPixel(x, y);
                Vector3 cX1Y = MRAO.GetPixel(x + 1, y);
                Vector3 cXY1 = MRAO.GetPixel(x, y + 1);
                Vector3 cX1Y1 = MRAO.GetPixel(x + 1, y + 1);
                return (cXY * u_opp + cX1Y * u_ratio) * v_opp + (cXY1 * u_opp + cX1Y1 * u_ratio) * v_ratio;
            }
            return MRAO.GetPixel((int)(u * MRAO.PixelWidth), (int)(v * MRAO.PixelHeight));
        }
    }
}
