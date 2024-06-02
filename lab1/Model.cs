using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Media.Imaging;

namespace lab1
{
    public class Model
    {
        public List<Vector3> Positions = [];
        public List<Vector3> Normals = [];
        public List<Vector2> UV = [];
        public List<Material> Materials = [];
        public List<Vector3> Tangents = [];
        public Vector4[] ProjectionVertices;

        public Dictionary<string, int> MaterialNames = [];

        public List<int> PositionIndices = [];
        public List<int> UVIndices = [];
        public List<int> NormalIndices = [];
        public List<int> MaterialIndices = [];
        public List<int> TangentIndices = [];
        public List<sbyte> Signs = [];

        public List<int> OpaqueFacesIndices = [];
        public List<int> TransparentFacesIndices = [];

        public Dictionary<string, List<Buffer<Vector3>>> Textures = [];

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

        public float X = 0;
        public float Y = 0;
        public float Z = 0;

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

            Positions.Add(new(x, y, z));
        }

        public List<Buffer<Vector3>> AddTexture(string uri, bool useSrgbToLinearTransform = false, bool isNormal = false)
        {
            if (File.Exists(uri))
            {
                if (!Textures.TryGetValue(uri, out List<Buffer<Vector3>> texture))
                {
                    texture = Material.CalculateMIP(new(new BitmapImage(new Uri(uri, UriKind.Relative))), useSrgbToLinearTransform, isNormal);
                    Textures.Add(uri, texture);
                }

                return texture;
            }
            
            return null;
        }

        public void AddFace(String v1, String v2, String v3, int materialIndex, int faceIndex)
        {
            MaterialIndices.Add(materialIndex);

            int[] p1 = v1.Split("/").Select(x => int.Parse(x)).ToArray();
            int[] p2 = v2.Split("/").Select(x => int.Parse(x)).ToArray();
            int[] p3 = v3.Split("/").Select(x => int.Parse(x)).ToArray();

            PositionIndices.AddRange([p1[0] - 1, p2[0] - 1, p3[0] - 1]);
            UVIndices.AddRange([p1[1] - 1, p2[1] - 1, p3[1] - 1]);
            NormalIndices.AddRange([p1[2] - 1, p2[2] - 1, p3[2] - 1]);

            if (Materials.Count > 0)
            {
                if (Materials[materialIndex].BlendMode == BlendModes.Opaque)
                    OpaqueFacesIndices.Add(faceIndex);
                else
                    TransparentFacesIndices.Add(faceIndex);
            }
            else
            {
                OpaqueFacesIndices.Add(faceIndex);
            }
        }

        public void AddNormal(float x, float y, float z)
        {
            Normals.Add(new(x, y, z));
        }

        public void AddUV(float u, float v)
        {
            UV.Add(new(u, v));
        }

        public Vector3 GetCenter()
        {
            X = (maxX + minX) / 2;
            Y = (maxY + minY) / 2;
            Z = (maxZ + minZ) / 2;
            return new Vector3(X, Y, Z);
        }

        public float GetMinZoomR ()
        {
            return float.Max(float.Max(maxX - minX, maxY - minY), maxZ - minZ);
        }

        public void CalculateTangents()
        {
            Dictionary<(int, int, int, int, int), int> tangentDictionary = [];

            for (int i = 0; i < PositionIndices.Count; i += 3) 
            {
                Vector3 p0 = Positions[PositionIndices[i]];
                Vector3 p1 = Positions[PositionIndices[i + 1]];
                Vector3 p2 = Positions[PositionIndices[i + 2]];

                Vector2 uv0 = UV[UVIndices[i]];
                Vector2 uv1 = UV[UVIndices[i + 1]];
                Vector2 uv2 = UV[UVIndices[i + 2]];

                Vector3 e1 = p1 - p0;
                Vector3 e2 = p2 - p0;

                float x1 = uv1.X - uv0.X, x2 = uv2.X - uv0.X;
                float y1 = uv0.Y - uv1.Y, y2 = uv0.Y - uv2.Y;

                float r = 1 / (x1 * y2 - x2 * y1);    
                Vector3 t = (e1 * y2 - e2 * y1) * r;
                Vector3 b = (e2 * x1 - e1 * x2) * r;

                if (r == float.PositiveInfinity || r == float.NegativeInfinity)
                {
                    t = Vector3.Zero; 
                    b = Vector3.Zero;
                }

                sbyte sign = (sbyte)float.Sign(Vector3.Dot(Vector3.Cross(e1, e2), Vector3.Cross(t, b)));
                Signs.Add(sign);

                for (int j = i; j < i + 3; j++)
                {
                    (int, int, int, int, int) key = (PositionIndices[j], UVIndices[j], NormalIndices[j], MaterialIndices[i / 3], sign);

                    if (!tangentDictionary.TryGetValue(key, out int tangentIndex))
                    {
                        tangentDictionary.Add(key, Tangents.Count);
                        tangentIndex = Tangents.Count;
                        Tangents.Add(new());
                    }

                    TangentIndices.Add(tangentIndex);
                    Vector3 n = Normals[NormalIndices[j]];

                    Tangents[tangentIndex] += t - Vector3.Dot(t, n) * n;
                }
            }

            for (int i = 0; i < PositionIndices.Count; i++)
            {
                if (Tangents[TangentIndices[i]].Length() > 0)
                    Tangents[TangentIndices[i]] = Vector3.Normalize(Tangents[TangentIndices[i]]);
            }

        }
    }
}
