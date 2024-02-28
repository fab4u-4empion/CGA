using System.Numerics;
using static System.Numerics.Vector3;
using static System.Single;
using lab1.Shadow;
using static lab1.LightingConfig;

namespace lab1.Shaders
{
    public class PBR
    {
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

        public static Vector3 GetPixelColorMetallic(
            Vector3 baseColor,
            float metallic,
            float roughness,
            float ao,
            float opacity,
            float dissolve,
            Vector3 emission,
            Vector3 n,
            Vector3 clearCoatN,
            float clearCoat,
            float clearCoatRougness,
            Vector3 camera,
            Vector3 p,
            int faceIndex
        )
        {
            roughness *= roughness;
            clearCoatRougness *= clearCoatRougness;

            Vector3 N = Normalize(n);
            Vector3 CN = Normalize(clearCoatN);
            Vector3 V = Normalize(camera - p);

            float NdotV = Dot(N, V);

            int useSpecular = NdotV < 0 ? 0 : 1;

            NdotV = Max(Dot(N, V), 0);
            float CNdotV = Max(Dot(CN, V), 0);

            Vector3 F0 = Lerp(new(0.04f), baseColor, metallic);

            Vector3 color = Zero;

            for (int i = 0; i < Lights.Count; i++)
            {
                Vector3 L = Normalize(Lights[i].Position - p);
                Vector3 H = Normalize(V + L);

                if (Dot(N, L) <= 0)
                    continue;

                float distance = Distance(Lights[i].Position, p);

                float intensity = UseShadow ? RTX.GetLightIntensityBVH(Lights[i].Position, p, faceIndex) : 1;

                float NdotL = Max(Dot(N, L), 0);
                float CNdotL = Max(Dot(CN, L), 0);
                float NdotH = Max(Dot(N, H), 0);
                float VdotH = Max(Dot(V, H), 0);
                float CNdotH = Max(Dot(CN, H), 0);

                float distribution = Distribution(NdotH, roughness);
                float visibility = Visibility(NdotV, NdotL, roughness);
                Vector3 reflectance = FresnelSchlick(VdotH, F0);

                Vector3 diffuse = (1 - metallic) * baseColor / Pi * opacity;
                Vector3 specular = reflectance * visibility * distribution * useSpecular;

                Vector3 irradiance = Lights[i].Color * Lights[i].Intensity / (distance * distance);

                float clearCoatDistribution = Distribution(CNdotH, clearCoatRougness);
                float clearCoatVisibility = Visibility(CNdotV, CNdotL, clearCoatRougness);
                Vector3 clearCoatReflectance = FresnelSchlick(VdotH, new(0.04f)) * clearCoat;

                Vector3 clearCoatSpecular = clearCoatReflectance * clearCoatVisibility * clearCoatDistribution * useSpecular;

                color += (((One - reflectance) * diffuse + specular) * (One - clearCoatReflectance) * NdotL + clearCoatSpecular * CNdotL) * irradiance * intensity;
            }

            color += baseColor * ao * AmbientIntensity * opacity + emission * EmissionIntensity;

            return color * dissolve;
        }

        public static Vector3 GetPixelColorSpecular(
            Vector3 baseColor,
            Vector3 F0,
            float roughness,
            float ao,
            float opacity,
            float dissolve,
            Vector3 emission,
            Vector3 n,
            Vector3 clearCoatN,
            float clearCoat,
            float clearCoatRougness,
            Vector3 camera,
            Vector3 p,
            int faceIndex
        )
        {
            roughness *= roughness;
            clearCoatRougness *= clearCoatRougness;

            Vector3 N = Normalize(n);
            Vector3 CN = Normalize(clearCoatN);
            Vector3 V = Normalize(camera - p);

            float NdotV = Dot(N, V);

            int useSpecular = NdotV < 0 ? 0 : 1;

            NdotV = Max(Dot(N, V), 0);
            float CNdotV = Max(Dot(CN, V), 0);

            Vector3 color = Zero;

            for (int i = 0; i < Lights.Count; i++)
            {
                Vector3 L = Normalize(Lights[i].Position - p);
                Vector3 H = Normalize(V + L);

                if (Dot(N, L) <= 0)
                    continue;

                float distance = Distance(Lights[i].Position, p);

                float intensity = UseShadow ? RTX.GetLightIntensityBVH(Lights[i].Position, p, faceIndex) : 1;

                float NdotL = Max(Dot(N, L), 0);
                float CNdotL = Max(Dot(CN, L), 0);
                float NdotH = Max(Dot(N, H), 0);
                float VdotH = Max(Dot(V, H), 0);
                float CNdotH = Max(Dot(CN, H), 0);

                float distribution = Distribution(NdotH, roughness);
                float visibility = Visibility(NdotV, NdotL, roughness);
                Vector3 reflectance = FresnelSchlick(VdotH, F0);

                Vector3 diffuse = baseColor / Pi * opacity;
                Vector3 specular = reflectance * visibility * distribution * useSpecular;

                Vector3 irradiance = Lights[i].Color * Lights[i].Intensity / (distance * distance);

                float clearCoatDistribution = Distribution(CNdotH, clearCoatRougness);
                float clearCoatVisibility = Visibility(CNdotV, CNdotL, clearCoatRougness);
                Vector3 clearCoatReflectance = FresnelSchlick(VdotH, new(0.04f)) * clearCoat;

                Vector3 clearCoatSpecular = clearCoatReflectance * clearCoatVisibility * clearCoatDistribution * useSpecular;

                color += (((One - reflectance) * diffuse + specular) * (One - clearCoatReflectance) * NdotL + clearCoatSpecular * CNdotL) * irradiance * intensity;
            }

            color += baseColor * ao * AmbientIntensity * opacity + emission * EmissionIntensity;

            return color * dissolve;
        }
    }
}
