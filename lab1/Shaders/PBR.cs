﻿using lab1.Shadow;
using System.Numerics;
using static lab1.LightingConfig;
using static System.Numerics.Vector3;
using static System.Single;

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
            return MinNumber(1e8f, a2 / (float.Pi * k * k));
        }

        private static float Visibility(float NdotV, float NdotL, float roughness)
        {
            float a2 = roughness * roughness;
            float v = NdotL * Sqrt(NdotV * NdotV * (1 - a2) + a2);
            float l = NdotV * Sqrt(NdotL * NdotL * (1 - a2) + a2);
            return Min(1e8f, 0.5f / (v + l));
        }

        public static Vector3 GetPixelColor(
            Vector3 baseColor,
            Vector3 F0,
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
            Vector3 o
        )
        {
            float r2 = roughness * roughness;
            float cr2 = clearCoatRougness * clearCoatRougness;

            Vector3 N = Normalize(n);
            Vector3 CN = Normalize(clearCoatN);
            Vector3 V = Normalize(camera - p);

            float NdotV = Max(Dot(N, V), 0);
            float CNdotV = Max(Dot(CN, V), 0);

            Vector3 color = Zero;

            F0 = Lerp(F0, baseColor, metallic);
            Vector3 albedo = (1 - metallic) * baseColor;
            Vector3 diffuse = albedo / float.Pi * opacity;

            for (int i = 0; i < Lights.Count; i++)
            {
                Vector3 L = Lights[i].GetL(p);

                float NdotL = Dot(N, L);

                if (NdotL <= 0)
                    continue;

                Vector3 H = Normalize(V + L);

                float intensity = UseShadow ? RTX.GetLightIntensityBVH(Lights[i], o, N) : 1;

                float CNdotL = Max(Dot(CN, L), 0);
                float NdotH = Max(Dot(N, H), 0);
                float VdotH = Max(Dot(V, H), 0);
                float CNdotH = Max(Dot(CN, H), 0);

                float distribution = Distribution(NdotH, r2);
                float visibility = Visibility(NdotV, NdotL, r2);
                Vector3 reflectance = FresnelSchlick(VdotH, F0);

                Vector3 specular = reflectance * visibility * distribution;
                Vector3 irradiance = Lights[i].GetIrradiance(p);

                float clearCoatDistribution = Distribution(CNdotH, cr2);
                float clearCoatVisibility = Visibility(CNdotV, CNdotL, cr2);
                Vector3 clearCoatReflectance = FresnelSchlick(VdotH, new(0.04f)) * clearCoat;

                Vector3 clearCoatSpecular = clearCoatReflectance * clearCoatVisibility * clearCoatDistribution;

                color += (((One - reflectance) * diffuse + specular) * (One - clearCoatReflectance) * NdotL + clearCoatSpecular * CNdotL) * irradiance * intensity;
            }

            Vector3 ambientReflectance = F0;
            Vector3 ambientDiffuse = albedo / float.Pi * opacity;
            Vector3 ambientIrradiance = GetIBLDiffuseColor(N);

            float lod = roughness * (IBLSpecularMap.Count - 1);
            int lod0 = (int)lod, lod1 = int.Min(lod0 + 1, IBLSpecularMap.Count - 1);

            Vector3 R = Reflect(-V, N);

            Vector3 ambientSpecularLight0 = GetIBLSpecularColor(R, lod0);
            Vector3 ambientSpecularLight1 = GetIBLSpecularColor(R, lod1);
            Vector3 ambientSpecularLight = Lerp(ambientSpecularLight0, ambientSpecularLight1, lod - lod0);
            Vector3 brdf = BRDFLLUT.GetColor(NdotV, 1 - roughness);

            Vector3 ambientSpecular = ambientSpecularLight * (ambientReflectance * brdf.X + new Vector3(brdf.Y));

            Vector3 clearCoatReflectanceIBL = new Vector3(0.04f) * clearCoat;
            lod = clearCoatRougness * (IBLSpecularMap.Count - 1);
            lod0 = (int)lod;
            lod1 = int.Min(lod0 + 1, IBLSpecularMap.Count - 1);
            R = Reflect(-V, CN);

            Vector3 coatSpecularLight0 = GetIBLSpecularColor(R, lod0);
            Vector3 coatSpecularLight1 = GetIBLSpecularColor(R, lod1);
            Vector3 coatSpecularLight = Lerp(coatSpecularLight0, coatSpecularLight1, lod - lod0);
            brdf = BRDFLLUT.GetColor(CNdotV, 1 - clearCoatRougness);
            Vector3 clearCoatSpecularIBL = coatSpecularLight * (clearCoatReflectanceIBL * brdf.X + new Vector3(brdf.Y) * clearCoat);

            if (UseRTAO) ao = RTX.GetAmbientOcclusionBVH(o, N);
            color += (((One - ambientReflectance) * ambientDiffuse * ambientIrradiance + ambientSpecular) * (One - clearCoatReflectanceIBL) + clearCoatSpecularIBL) * ao;

            color += emission * EmissionIntensity;

            return color * dissolve;
        }
    }
}