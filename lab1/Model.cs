using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace lab1
{
    public class Model
    {
        public List<Vector4> Vertices = new();
        
        public void AddVertex(float x, float y, float z)
        {
            Vertices.Add(new(x, y, x, 1));
        }
    }
}
