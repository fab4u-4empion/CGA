using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace lab1
{
    public class ZBuffer
    {
        private float[,] buffer;

        private int w = 0;
        private int h = 0;

        public ZBuffer(int w, int h)
        {
            buffer = new float[w, h];
            this.w = w;
            this.h = h;
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    buffer[i, j] = float.MaxValue;
                }
            }
        }

        public void Clear()
        {

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
