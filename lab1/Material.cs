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

        public float Pm = 0;
        public float Pr = 1;
        public Vector3 Kd = Vector3.Zero;

        public Vector3 GetDiffuse(float u, float v)
        {
            return Diffuse == null ? Kd : Diffuse.GetPixel((int)(u * Diffuse.PixelWidth), (int)(v * Diffuse.PixelHeight));
        }

        public Vector3 GetEmission(float u, float v)
        {
            return Emission == null ? Vector3.Zero : Emission.GetPixel((int)(u * Emission.PixelWidth), (int)(v * Emission.PixelHeight));
        }

        public Vector3 GetNormal(float u, float v)
        {
            return Normals == null ? Vector3.Zero : 2 * Normals.GetPixel((int)(u * Normals.PixelWidth), (int)(v * Normals.PixelHeight)) - Vector3.One;
        }

        public Vector3 GetMRAO(float u, float v)
        {
            return MRAO == null ? new(Pm, Pr, 1) : MRAO.GetPixel((int)(u * MRAO.PixelWidth), (int)(v * MRAO.PixelHeight));
        }
    }
}
