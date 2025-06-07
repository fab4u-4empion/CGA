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
            new() { Radius = 2, Intensity = 1f, Name = "Default 0" },
            new() { Radius = 4, Intensity = 1f, Name = "Default 1" },
            new() { Radius = 8, Intensity = 1f, Name = "Default 2" },
            new() { Radius = 16, Intensity = 1f, Name = "Default 3" },
            new() { Radius = 32, Intensity = 1f, Name = "Default 4" },
            new() { Radius = 64, Intensity = 1f, Name = "Default 5" },
        ];

        public static Pbgra32Bitmap? KernelImg { get; set; } = null;
    }
}