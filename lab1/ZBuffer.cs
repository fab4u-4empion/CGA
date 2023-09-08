using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace lab1
{
    public class ZBuffer
    {
        private float[,] buffer;

        public ZBuffer(int w, int h)
        {
            buffer = new float[w, h];
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    buffer[i, j] = float.MaxValue;
                }
            }
        }

        public float this[int x, int y] {
            get => buffer[x, y];
            set => buffer[x, y] = value;
        }
    }
}
