﻿using lab1.Effects;
using System.Collections.Generic;
using System.Numerics;
using static lab1.Utils;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public enum LampType
    {
        Point,
        Directional
    }

    public class Lamp
    {
        public Vector3 Position = Zero;
        public Vector3 Color = One;
        public float Intensity = 100;
        public string Name = "";
        public float Theta = 45;
        public float Phi = 45;
        public LampType Type = LampType.Point;
        public float Radius = 0;
        public float Angle = 0.526f;

        public Vector3 GetIrradiance(Vector3 point)
        {
            if (Type == LampType.Directional)
                return Intensity * Color;

            float distance = Distance(Position, point);

            return Intensity * Color / (distance * distance);
        }

        public Vector3 GetL(Vector3 point)
        {
            if (Type == LampType.Directional)
                return SphericalToCartesian(DegreesToRadians(Phi), DegreesToRadians(Theta), 1);

            return Normalize(Position - point);
        }
    }

    public class LightingConfig
    {
        public static Vector3 AmbientColor { get; set; } = ToneMapping.SrgbToLinear(Create(0.251f));

        public static float EmissionIntensity { get; set; } = 100;

        public static int CurrentLamp { get; set; } = 0;

        public static bool DrawLamps { get; set; } = true;

        public static bool DrawGround { get; set; } = false;

        public static bool UseShadow { get; set; } = false;

        public static bool UseRTAO { get; set; } = false;

        public static HDRTexture? IBLDiffuseMap { get; set; } = null;
        public static List<HDRTexture> IBLSpecularMap { get; set; } = [];
        public static HDRTexture BRDFLLUT { get; set; } = new();

        public static List<Lamp> Lights { get; } = [
            new() { Position = new(10, 10, 10), Color = new(1, 0.5f, 1), Intensity = 500, Name = "Default 0" },
            new() { Position = new(-10, 10, 10), Color = new(0.5f, 1f, 0.5f), Intensity = 500, Name = "Default 1" },
            new() { Position = new(10, 10, -10), Color = new(0.5f, 0.5f, 1), Intensity = 500, Name = "Default 2" },
            new() { Position = new(-10, 10, -10), Color = new(0.5f, 1, 1), Intensity = 500, Name = "Default 3" },
        ];

        public static void ChangeLamp(int delta)
        {
            if (Lights.Count > 0)
                CurrentLamp = int.Clamp(CurrentLamp + delta, 0, Lights.Count - 1);
            else
                CurrentLamp = -1;
        }

        public static void ChangeLampIntensity(float delta)
        {
            if (CurrentLamp > -1)
            {
                if (Lights[CurrentLamp].Type == LampType.Directional)
                    Lights[CurrentLamp].Intensity = Max(Lights[CurrentLamp].Intensity + delta / 10, 0);
                else
                    Lights[CurrentLamp].Intensity = Max(Lights[CurrentLamp].Intensity + delta, 0);
            }
        }

        public static void ChangeLampSize(float deltaRadius, float deltaAngle)
        {
            if (CurrentLamp > -1)
            {
                if (Lights[CurrentLamp].Type == LampType.Directional)
                    Lights[CurrentLamp].Angle = Clamp(Lights[CurrentLamp].Angle + deltaAngle, 0, 90);
                else
                    Lights[CurrentLamp].Radius = Max(Lights[CurrentLamp].Radius + deltaRadius, 0);
            }
        }

        public static void ChangeLampPosition(Vector3 delta)
        {
            if (CurrentLamp > -1)
            {
                Lamp lamp = Lights[CurrentLamp];
                if (lamp.Type == LampType.Directional)
                {
                    lamp.Theta = Clamp(lamp.Theta + delta.Z * 10, 0, 180);
                    lamp.Phi += delta.X * 10;
                    lamp.Phi = (lamp.Phi % 360 + 360) % 360;
                }
                else
                {
                    Lights[CurrentLamp].Position += delta;
                }
            }
        }

        public static Vector3 GetIBLDiffuseColor(Vector3 n)
        {
            if (IBLDiffuseMap == null)
                return float.Pi * AmbientColor;

            return IBLDiffuseMap.GetColor(n);
        }

        public static Vector3 GetIBLSpecularColor(Vector3 n, int lod)
        {
            if (IBLSpecularMap.Count == 0)
                return AmbientColor;

            return IBLSpecularMap[lod].GetColor(n);
        }
    }
}