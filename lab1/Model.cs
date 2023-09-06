using System.Collections.Generic;
using System.Numerics;

namespace lab1
{
    public class Model
    {
        public List<Vector4> Vertices = new();
        public List<List<Vector3>> Faces = new();

        public float Scale { get; set; }

        public Vector3 Translation { get; set; }

        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public float Roll { get; set; }

        public Model() {
            Scale = 1.0f;
            Translation = new Vector3(0, -6, -2);
            Yaw = 0.0f;
            Pitch = 0.0f;
            Roll = 0.0f;
        }
        
        public void AddVertex(float x, float y, float z)
        {
            Vertices.Add(new(x, y, z, 1));
        }

        public void AddFace(List<Vector3> face)
        {
            Faces.Add(face);
        }
    }
}
