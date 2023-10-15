using Rasterization;
using System.Collections.Generic;
using System.Numerics;

namespace lab1
{
    public class Model
    {
        public List<Vector4> Vertices = new();
        public List<List<Vector3>> Faces = new();
        public List<Vector3> Normals = new();
        public List<Vector2> UV = new();

        public Pbgra32Bitmap DiffuseMap { get; set; }
        public Pbgra32Bitmap NormalMap { get; set; }
        public Pbgra32Bitmap SpecularMap { get; set; }

        public float Scale { get; set; }

        public Vector3 Translation { get; set; }

        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public float Roll { get; set; }

        private float minX = float.MaxValue;
        private float minY = float.MaxValue;
        private float minZ = float.MaxValue;

        private float maxX = float.MinValue;
        private float maxY = float.MinValue;
        private float maxZ = float.MinValue;

        public Model() {
            Scale = 1.0f;
            Translation = new Vector3(0, 0, 0);
            Yaw = 0.0f;
            Pitch = 0.0f;
            Roll = 0.0f;
        }
        
        public void AddVertex(float x, float y, float z)
        {
            maxX = float.Max(maxX, x);
            maxY = float.Max(maxY, y);
            maxZ = float.Max(maxZ, z);

            minX = float.Min(minX, x);
            minY = float.Min(minY, y);
            minZ = float.Min(minZ, z);

            Vertices.Add(new(x, y, z, 1));
        }

        public void AddFace(List<Vector3> vertices)
        {
            Faces.Add(vertices);
        }

        public void AddNormal(float x, float y, float z)
        {
            Normals.Add(new(x, y, z));
        }

        public void AddUV(float u, float v)
        {
            UV.Add(new(u, v));
        }

        public void TransformModelParams()
        {
            float X = -minX - (maxX - minX) / 2;
            float Y = -minY - (maxY - minY) / 2;
            float Z = -minZ - (maxZ - minZ) / 2;
            Scale = 11.4106541f / (float.Abs(maxY) + float.Abs(minY));
            Translation = new(X * Scale, Y * Scale, Z * Scale);
        }

        public Vector3 GetDiffuse(float u, float v)
        {
            return DiffuseMap.GetPixel((int)(u * DiffuseMap.PixelWidth), (int)(v * DiffuseMap.PixelHeight));
        }

        public Vector3 GetNormal(float u, float v)
        {
            return NormalMap.GetPixel((int)(u * NormalMap.PixelWidth), (int)(v * NormalMap.PixelHeight));
        }

        public Vector3 GetSpecular(float u, float v)
        {
            return SpecularMap.GetPixel((int)(u * SpecularMap.PixelWidth), (int)(v * SpecularMap.PixelHeight));
        }
    }
}
