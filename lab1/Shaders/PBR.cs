using System.Numerics;
using System.Windows.Documents;
using static System.Numerics.Vector3;
using static System.Single;
using System.Collections.Generic;
using lab1.Shadow;

namespace lab1.Shaders
{
    public class PBR
    {
        public static float LightIntensity = 10_000;
        public static float LP = 50;
        public static float AmbientIntensity = 0.15f;
        public static float EmissionIntensity = 1;

        public static bool UseShadow = false;

        public static float X = 0;
        public static float Y = 3.6f;
        public static float Z = 0;

        private static Vector3[] Lights = new Vector3[] {
            new (LP, LP, LP),
            new (-LP, LP, LP),
            new (LP, LP, -LP),
            new (-LP, LP, -LP),
            //new(-1.6f, 3.6f, -1.6f),
            //new(1.6f, 3.6f, -1.6f),
            //new(-1.6f, 3.6f, 1.6f),
            //new(X, Y, Z)
        }; 

        private static Vector3[] LightsColors = new Vector3[] {
            new (1, 0.5f, 1f),
            new (0.5f, 1f, 0.5f),
            new (0.5f, 0.5f, 1),
            new (0.5f, 1, 1)
            //new(1, 1, 1)
            //new(1, 0, 0),
            //new(0, 1, 0),
            //new(0, 0, 1),
        };

        private static Vector3 FresnelSchlick(float VdotH, Vector3 F0)
        {
            float t = 1 - VdotH;
            float t2 = t * t;
            float t5 = t2 * t2 * t;
            return F0 + (One - F0) * t5;
        }

        private static float Distribution(float NdotH, float roughness)
        {
            float a2 = roughness * roughness;

            float k = NdotH * NdotH * (a2 - 1) + 1;
            float d = a2 / (Pi * k * k);

            return d < 1e12f ? d : 1e12f;
        }

        private static float Visibility(float NdotV, float NdotL, float roughness)
        {
            float a2 = roughness * roughness;

            float v = NdotL * Sqrt(NdotV * NdotV * (1 - a2) + a2);
            float l = NdotV * Sqrt(NdotL * NdotL * (1 - a2) + a2);

            return Min(1e12f, 0.5f / (v + l));
        }

        public static Vector3 AsecFilmic(Vector3 color)
        {
            color = new(
                Dot(new(0.59719f, 0.35458f, 0.04823f), color),
                Dot(new(0.07600f, 0.90834f, 0.01566f), color),
                Dot(new(0.02840f, 0.13383f, 0.83777f), color)
            );

            color = (color * (color + new Vector3(0.0245786f)) - new Vector3(0.000090537f)) /
                (color * (0.983729f * color + new Vector3(0.4329510f)) + new Vector3(0.238081f));

            color = new(
                Dot(new(1.60475f, -0.53108f, -0.07367f), color),
                Dot(new(-0.10208f, 1.10813f, -0.00605f), color),
                Dot(new(-0.00327f, -0.07276f, 1.07602f), color)
            );

            return Clamp(color, Vector3.Zero, One);
        }

        public static Vector3 SrgbToLinear(Vector3 color)
        {
            static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : Pow((c + 0.055f) / 1.055f, 2.4f);
            return new(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z));
        }

        public static Vector3 LinearToSrgb(Vector3 color)
        {
            static float LinearToSrgb(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * Pow(c, 1 / 2.4f) - 0.055f;
            return new(LinearToSrgb(color.X), LinearToSrgb(color.Y), LinearToSrgb(color.Z));
        }

        public static void ChangeLightsPos()
        {
            Lights = new Vector3[] {
                new (LP, LP, LP),
                new (-LP, LP, LP),
                new (LP, LP, -LP),
                new (-LP, LP, -LP),
                //new(-1.6f, 3.6f, -1.6f),
                //new(1.6f, 3.6f, -1.6f),
                //new(-1.6f, 3.6f, 1.6f),
            };
            //Lights = new Vector3[] {
            //    new(X, Y, Z)
            //};
        }

        public static (Vector3, Vector3) GetPixelColor(
            Vector3 albedo, 
            float metallic, 
            float roughness, 
            float ao, 
            Vector3 emission, 
            Vector3 n, 
            Vector3 camera, 
            Vector3 p,
            List<List<Vector3>> faces,
            int faceIndex
        )
        {
            albedo = SrgbToLinear(albedo);
            emission = SrgbToLinear(emission);
            roughness *= roughness;

            Vector3 N = Normalize(n);
            Vector3 V = Normalize(camera - p);

            float NdotV = Max(Dot(N, V), 0);

            Vector3 F0 = Lerp(new(0.04f), albedo, metallic);

            Vector3 color = Zero;

            for (int i = 0; i < Lights.Length; i++)
            {
                Vector3 L = Normalize(Lights[i] - p);
                Vector3 H = Normalize(V + L);

                float distance = (Lights[i] - p).Length();

                if (!UseShadow || !RTX.CheckIntersection(L, p, distance, faces, faceIndex))
                {
                    float attenuation = 1.0f / (distance * distance);
                    Vector3 radiance = LightsColors[i] * attenuation * LightIntensity;

                    float NdotH = Max(Dot(N, H), 0);
                    float NdotL = Max(Dot(N, L), 0);

                    float D = Distribution(NdotH, roughness);
                    float G = Visibility(NdotV, NdotL, roughness);
                    Vector3 F = FresnelSchlick(NdotH, F0);

                    Vector3 kS = F;
                    Vector3 kD = Vector3.One - kS;
                    kD *= 1 - metallic;

                    Vector3 specular = D * G * F;

                    color += (kD * albedo / Pi + specular) * radiance * NdotL;
                }
            }

            color += albedo * ao * AmbientIntensity + emission * EmissionIntensity;

            float luminance = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;
            float x = float.Clamp((luminance ) / 5f, 0, 1);
            float factor = x * x * (3 - 2 * x);
            return (color, color * factor);
        }
    }
}
