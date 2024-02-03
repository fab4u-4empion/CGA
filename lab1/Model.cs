using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace lab1
{
    public class Tangent
    {
        public Vector3 T = new();
        public sbyte S = 0;
    }

    public class Model
    {
        public List<Vector4> Positions = [];
        public List<Vector3> Normals = [];
        public List<Vector2> UV = [];
        public List<Material> Materials = [];
        public List<Tangent> Tangents = [];
        public Vector4[] ViewVertices;

        public Dictionary<string, int> MaterialNames = [];

        public List<int> PositionIndices = [];
        public List<int> UVIndices = [];
        public List<int> NormalIndices = [];
        public List<int> MaterialIndices = [];
        public List<int> TangentIndices = [];

        public List<int> OpaqueFacesIndices = [];
        public List<int> TransparentFacesIndices = [];        

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

            Positions.Add(new(x, y, z, 1));
        }

        public void AddFace((int, int, int) v1, (int, int, int) v2, (int, int, int) v3, int materialIndex, int faceIndex)
        {
            MaterialIndices.Add(materialIndex);
            PositionIndices.AddRange([v1.Item1 - 1, v2.Item1 - 1, v3.Item1 - 1]);
            UVIndices.AddRange([v1.Item2 - 1, v2.Item2 - 1, v3.Item2 - 1]);
            NormalIndices.AddRange([v1.Item3 - 1, v2.Item3 - 1, v3.Item3 - 1]);

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
            int tangentIndex = 0;
            List<Vector3> tangent = [];
            List<Vector3> bitangent = [];
            Dictionary<(int, int, int, int, int), int> tangentDictionary = [];

            for (int i = 0; i < PositionIndices.Count; i += 3) 
            {
                Vector4 p0 = Positions[PositionIndices[i]];
                Vector4 p1 = Positions[PositionIndices[i + 1]];
                Vector4 p2 = Positions[PositionIndices[i + 2]];

                Vector2 uv0 = UV[UVIndices[i]];
                Vector2 uv1 = UV[UVIndices[i + 1]];
                Vector2 uv2 = UV[UVIndices[i + 2]];

                Vector3 e1 = new Vector3(p1.X, p1.Y, p1.Z) - new Vector3(p0.X, p0.Y, p0.Z);
                Vector3 e2 = new Vector3(p2.X, p2.Y, p2.Z) - new Vector3(p0.X, p0.Y, p0.Z);

                float x1 = uv1.X - uv0.X, x2 = uv2.X - uv0.X;
                float y1 = uv1.Y - uv0.Y, y2 = uv2.Y - uv0.Y;

                float r = 1 / (x1 * y2 - x2 * y1);    
                Vector3 t = (e1 * y2 - e2 * y1) * r;
                Vector3 b = (e2 * x1 - e1 * x2) * r;

                for (int j = i; j < i + 3; j++)
                {
                    Vector3 n = Normals[NormalIndices[j]];
                    int s = float.Sign(Vector3.Dot(Vector3.Cross(t, b), n));

                    (int, int, int, int, int) key = (PositionIndices[j], UVIndices[j], NormalIndices[j], MaterialIndices[i / 3], s);

                    if (!tangentDictionary.TryGetValue(key, out int ti))
                    {
                        tangentDictionary.Add(key, tangentIndex);
                        
                        ti = tangentIndex;
                        Tangents.Add(new());
                        tangent.Add(new());
                        bitangent.Add(new());
                        tangentIndex++;
                    }

                    TangentIndices.Add(ti);
                    tangent[ti] += t;
                    bitangent[ti] += b;
                }
            }

            for (int i = 0; i < PositionIndices.Count; i++)
            {
                Vector3 t = tangent[TangentIndices[i]];
                Vector3 b = bitangent[TangentIndices[i]];
                Vector3 n = Normals[NormalIndices[i]];

                Tangents[TangentIndices[i]].T = Vector3.Normalize(t - Vector3.Dot(t, n) * n);
                Tangents[TangentIndices[i]].S = (sbyte)float.Sign(Vector3.Dot(Vector3.Cross(t, b), n));
            }

        }
    }
}
