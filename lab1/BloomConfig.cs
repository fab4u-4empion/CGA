using Rasterization;
using System.Collections.Generic;

namespace lab1
{
    public class Kernel
    {
        public string Name { get; set; } = "";
        public int Radius { get; set; }
        public float Intensity { get; set; }
    }

    public class BloomConfig
    {
        public static List<Kernel> Kernels { get; set; } = [];

        public static Pbgra32Bitmap? KernelImg { get; set; } = null;
    }
}
