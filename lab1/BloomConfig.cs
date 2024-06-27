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
        public static List<Kernel> Kernels { get; set; } = [
            new() { Radius = 1, Intensity = 0.1f, Name = "Default 0" },
            new() { Radius = 25, Intensity = 0.3f, Name = "Default 1" },
            new() { Radius = 50, Intensity = 1f, Name = "Default 2" },
        ];

        public static Pbgra32Bitmap? KernelImg { get; set; } = null;
    }
}