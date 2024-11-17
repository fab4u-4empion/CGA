using System.Numerics;
using static lab1.LightingConfig;
using static System.Numerics.Vector3;
using static System.Single;
using static System.Int32;

namespace lab1.Shaders
{
    public class Toon
    {
        public static Vector3 GetPixelColor(
            Vector3 baseColor, 
            Vector3 n, 
            Vector3 p, 
            Vector3 emission, 
            int d, 
            Buffer<int> ViewBuffer, 
            Buffer<byte> CountBuffer, 
            int x, 
            int y
        )
        {
            Vector3 color = Zero;

            for (int i = -d; i <= d; i++)
                for (int j = -d; j <= d; j++)
                    if (
                        ViewBuffer[Clamp(x + i, 0, ViewBuffer.Width - 1), Clamp(y + j, 0, ViewBuffer.Height - 1)] == -1
                        &&
                        CountBuffer[Clamp(x + i, 0, ViewBuffer.Width - 1), Clamp(y + j, 0, ViewBuffer.Height - 1)] == 0
                    )
                    {
                        return One;
                    }

            Vector3 N = Normalize(n);

            int lvl = 2;
            float step = 1f / lvl;

            for (int i = 0; i < Lights.Count; i++)
            {
                Lamp lamp = Lights[i];

                Vector3 L = lamp.GetL(p);

                float dot = Floor(Max(Dot(N, L), 0) * (lvl + 1)) * step;

                color += baseColor * lamp.GetIrradiance(p) * dot / float.Pi;
            }

            color += baseColor * AmbientColor + emission * EmissionIntensity;

            return color;
        }
    }
}
