using System.Numerics;
using static System.Single;
using static System.Numerics.Vector3;
using static lab1.LightingConfig;
using lab1.Shadow;

namespace lab1.Shaders
{
    public class Phong
    {
        public static Vector3 GetPixelColor(
            Vector3 baseColor, 
            Vector3 n, 
            Vector3 spec, 
            Vector3 camera, 
            Vector3 p, 
            int faceIndex,
            Vector3 emission,
            float opacity,
            float dissolve,
            float AO,
            float glossiness)
        {
            Vector3 color = baseColor * AmbientIntensity * AO * opacity + emission * EmissionIntensity;

            Vector3 V = Normalize(camera - p);
            Vector3 N = Normalize(n);

            float a2 = glossiness * glossiness;
            float a4 = a2 * a2;

            for (int i = 0; i < Lights.Count; i++)
            {
                Vector3 L = Normalize(Lights[i].Position - p);
                Vector3 H = Normalize(V + L);

                if (Dot(N, L) <= 0)
                    continue;

                float intensity = UseShadow ? RTX.GetLightIntensityBVH(Lights[i].Position, p, faceIndex) : 1;

                float distance = Distance(Lights[i].Position, p);

                Vector3 specular = spec * a4 * Pow(Max(Dot(H, N), 0), a4 * 1024f) * 5f;

                color += (baseColor * opacity / Pi + specular) * Lights[i].Intensity * Lights[i].Color / (distance * distance) * intensity * Max(Dot(N, L), 0);
            }

            return color * dissolve;
        }
    }
}
