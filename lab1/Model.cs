using Rasterization;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace lab1
{
    public class Model
    {
        public List<Vector4> Vertices = new();
        public List<List<Vector3>> Faces = new();
        public List<Vector3> Normals = new();
        public List<Vector2> UV = new();
        public List<Material> Materials = new();
        public List<int> FacesMaterials = new();
        public Dictionary<string, int> MaterialsIndexes = new();

        public List<int> OpaqueFacesIndexes = new();
        public List<int> TransparentFacesIndexes = new();

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

            Vertices.Add(new(x, y, z, 1));
        }

        public void AddFace(List<Vector3> vertex, int materialIndex)
        {
            Faces.Add(vertex);
            FacesMaterials.Add(materialIndex);

            if (Materials[materialIndex].BlendMode == BlendModes.Opaque)
                OpaqueFacesIndexes.Add(Faces.Count - 1);
            else
                TransparentFacesIndexes.Add(Faces.Count - 1);
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
    }
}
