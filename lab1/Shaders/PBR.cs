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
        public static float LightIntensity = 100;
        public static float LP = 50;
        public static float AmbientIntensity = 0.15f;
        public static float EmissionIntensity = 1;

        public static bool UseShadow = false;

        //public static float X = 0;
        //public static float Y = 3.6f;
        //public static float Z = 0;

        public static float X = 5;
        public static float Y = 5;
        public static float Z = 5;

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

        public static void ChangeLightsPos()
        {
            Lights = new Vector3[] {
                new(LP, LP, LP),
                new(-LP, LP, LP),
                new(LP, LP, -LP),
                new(-LP, LP, -LP),
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
            int faceIndex
        )
        {
            albedo = ToneMapping.SrgbToLinear(albedo);
            emission = ToneMapping.SrgbToLinear(emission);
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

                float distance = Distance(Lights[i], p);

                float intensity = UseShadow ? RTX.GetLightIntensityBVH(Lights[i], p, faceIndex) : 1;

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

                color += (kD * albedo / Pi + specular) * radiance * NdotL * intensity;
            }

            color += albedo * ao * AmbientIntensity + emission * EmissionIntensity;

            float luminance = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;
            float x = float.Clamp((luminance ) / 5f, 0, 1);
            float factor = x * x * (3 - 2 * x);
            return (color, color * factor);
        }
    }
}
