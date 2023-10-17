using System.Numerics;

namespace lab1.Shaders
{
    public class PBR
    {
        private static Vector3[] Lights = new Vector3[] { 
            new (50, 50, 50),
            new (-50, 50, 50),
            new (50, 50, -50),
            new (-50, 50, -50),
        };

        private static Vector3[] LightsColors = new Vector3[] {
            new (1, 0.5f, 1),
            new (0.5f, 1, 0.5f),
            new (0.5f, 0.5f, 1),
            new (0.5f, 1, 1)
        };

        private static Vector3 FresnelSchlick(float VdotH, Vector3 F0)
        {
            float t = 1 - VdotH;
            float t2 = t * t;
            float t5 = t2 * t2 * t;
            return F0 + (Vector3.One - F0) * t5;
        }

        private static float Distribution(float NdotH, float roughness)
        {
            float a = roughness * roughness;
            float a2 = a * a;

            float k = NdotH * NdotH * (a2 - 1) + 1;
            float d = a2 / (float.Pi * k * k);

            return d < 1e12f ? d : 1e12f;
        }

        private static float Visibility(float NdotV, float NdotL, float roughness)
        {
            float a = roughness * roughness;
            float a2 = a * a;

            float v = NdotL * float.Sqrt(NdotV * NdotV * (1 - a2) + a2);
            float l = NdotV * float.Sqrt(NdotL * NdotL * (1 - a2) + a2);

            return float.Min(1e12f, 0.5f / (v + l));
        }

        private static Vector3 AsecFilmic(Vector3 color)
        {
            color = new(
                Vector3.Dot(new(0.59719f, 0.35458f, 0.04823f), color),
                Vector3.Dot(new(0.07600f, 0.90834f, 0.01566f), color),
                Vector3.Dot(new(0.02840f, 0.13383f, 0.83777f), color)
            );

            color = (color * (color + new Vector3(0.0245786f)) - new Vector3(0.000090537f)) /
                (color * (0.983729f * color + new Vector3(0.4329510f)) + new Vector3(0.238081f));

            color = new(
                Vector3.Dot(new(1.60475f, -0.53108f, -0.07367f), color),
                Vector3.Dot(new(-0.10208f, 1.10813f, -0.00605f), color),
                Vector3.Dot(new(-0.00327f, -0.07276f, 1.07602f), color)
            );

            return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        }

        private static Vector3 SrgbToLinear(Vector3 color)
        {
            static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : float.Pow((c + 0.055f) / 1.055f, 2.4f);
            return new(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z));
        }

        private static Vector3 LinearToSrgb(Vector3 color)
        {
            static float LinearToSrgb(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * float.Pow(c, 1 / 2.4f) - 0.055f;
            return new(LinearToSrgb(color.X), LinearToSrgb(color.Y), LinearToSrgb(color.Z));
        }

        public static Vector3 GetPixelColor(Vector3 albedo, float metallic, float roughness, float ao, Vector3 n, Vector3 camera, Vector3 p)
        {
            albedo = SrgbToLinear(albedo);

            Vector3 N = Vector3.Normalize(n);
            Vector3 V = Vector3.Normalize(camera - p);

            Vector3 F0 = Vector3.Lerp(new(0.04f), albedo, metallic);

            Vector3 L0 = Vector3.Zero;
            for (int i = 0; i < Lights.Length; i++)
            {
                // calculate per-light radiance
                Vector3 L = Vector3.Normalize(Lights[i] - p);
                Vector3 H = Vector3.Normalize(V + L);
                float distance = (Lights[i] - p).Length();
                float attenuation = 1.0f / (distance * distance);
                Vector3 radiance = LightsColors[i] * attenuation * 5000;

                // cook-torrance brdf
                float D = Distribution(float.Max(Vector3.Dot(N, H), 0), roughness);
                float G = Visibility(float.Max(Vector3.Dot(N, V), 0), float.Max(Vector3.Dot(N, L), 0), roughness);
                Vector3 F = FresnelSchlick(float.Max(Vector3.Dot(H, V), 0), F0);

                Vector3 kS = F;
                Vector3 kD = Vector3.One - kS;
                kD *= 1 - metallic;

                Vector3 specular = D * G * F;

                // add to outgoing radiance Lo
                float NdotL = float.Max(Vector3.Dot(N, L), 0);
                L0 += (kD * albedo / float.Pi + specular) * radiance * NdotL;
            }

            Vector3 ambient = new Vector3(0.03f) * albedo * ao;

            Vector3 color = ambient + L0;
            color = LinearToSrgb(AsecFilmic(color));

            return color;
        }
    }
}
