using System.Collections.Generic;
using System.Numerics;
using static System.Single;

namespace lab1
{
    public enum LampTypes
    {
        Point,
        Directional
    }

    public class Lamp
    {
        public Vector3 Position = Vector3.Zero;
        public Vector3 Color = Vector3.One;
        public float Intensity = 100;
        public string Name = "";
        public float Theta = 90;
        public float Phi = 0;
        public LampTypes Type = LampTypes.Point;

        public Vector3 GetIrradiance(Vector3 point)
        {
            if (Type == LampTypes.Directional)
                return Intensity * Color;

            float distance = Vector3.Distance(Position, point);

            return Intensity * Color / (distance * distance);
        }

        public Vector3 GetL(Vector3 point)
        {
            if (Type == LampTypes.Directional)
                return GetDirection();

            return Vector3.Normalize(Position - point);
        }

        public Vector3 GetDirection()
        {
            return Vector3.Normalize(new(
                Sin(DegreesToRadians(Theta)) * Sin(DegreesToRadians(Phi)),
                Cos(DegreesToRadians(Theta)),
                Sin(DegreesToRadians(Theta)) * Cos(DegreesToRadians(Phi))
            ));
        }
    }

    public class LightingConfig
    {
        public static float AmbientIntensity = 1f;

        public static float EmissionIntensity = 1;

        public static int CurrentLamp = 0;

        public static bool DrawLights = true;

        public static bool UseShadow = false;

        public static HDRTexture? IBLDiffuseMap = null;
        public static HDRTexture? SkyBox = null;
        public static List<HDRTexture> IBLSpecularMap = [];
        public static HDRTexture BRDFLLUT = new();

        public static List<Lamp> Lights = [
            new() { Position = new(10, 10, 10), Color = new(1, 0.5f, 1), Intensity = 5, Name = "Default 0", Type = LampTypes.Directional},
            /*new() { Position = new(-10, 10, 10), Color = new(0.5f, 1f, 0.5f), Intensity = 500, Name = "Default 1" },
            new() { Position = new(10, 10, -10), Color = new(0.5f, 0.5f, 1), Intensity = 500, Name = "Default 2" },
            new() { Position = new(-10, 10, -10), Color = new(0.5f, 1, 1), Intensity = 500, Name = "Default 3" },*/
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
                Lights[CurrentLamp].Intensity = float.Max(Lights[CurrentLamp].Intensity + delta, 0);
            }
        }

        public static void ChangeLampPosition(Vector3 delta)
        {
            if (CurrentLamp > -1)
            {
                Lamp lamp = Lights[CurrentLamp];
                if (lamp.Type == LampTypes.Directional)
                {
                    lamp.Theta = float.Clamp(lamp.Theta + delta.X * 10, 0, 180);
                    lamp.Phi = lamp.Phi + delta.Y * 10;
                }
                else
                {
                    Lights[CurrentLamp].Position += delta;
                }
            }
        }
    }
}