using Rasterization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab1
{
    public class Kernel
    {
        public string Name { get; set; }
        public int Radius { get; set; }
        public float Intensity { get; set; }
    }

    public class BloomConfig
    {
        public static List<Kernel> Kernels = [];

        public static Pbgra32Bitmap KernelImg = null;
    }
}
