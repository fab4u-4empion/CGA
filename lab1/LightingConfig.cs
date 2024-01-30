using System.Collections.Generic;
using System.Numerics;
using System.Windows.Documents;
using System.Windows.Media.Media3D;

namespace lab1
{
    public struct Lamp
    {
        public Vector3 Position;
        public Vector3 Color;
        public float Intensity;
    }

    public class LightingConfig
    {
        public static float AmbientIntensity = 0.15f;

        public static float EmissionIntensity = 1;

        public static int CurrentLamp = 0;

        public static bool DrawLights = true;

        public static List<Lamp> Lights = [
            new() { Position = new(10, 10, 10), Color = new(1, 0.5f, 1), Intensity = 100},
            new() { Position = new(-10, 10, 10), Color = new(0.5f, 1f, 0.5f), Intensity = 100 },
            new() { Position = new(10, 10, -10), Color = new(0.5f, 0.5f, 1), Intensity = 100 },
            new() { Position = new(-10, 10, -10), Color = new(0.5f, 1, 1), Intensity = 100 },
        ];

        public static void ChangeLamp(int delta)
        {
            CurrentLamp = Lights.Count > 0 ? (CurrentLamp + delta) & (Lights.Count - 1) : -1;
        }

        public static void ChangeLampIntensity(float delta)
        {
            if (CurrentLamp > -1)
            {
                Lamp lamp = Lights[CurrentLamp];
                lamp.Intensity = float.Max(lamp.Intensity + delta, 0);
                Lights[CurrentLamp] = lamp;
            }
        }

        public static void ChangeLampPosition(Vector3 delta)
        {
            if (CurrentLamp > -1)
            {
                Lamp lamp = Lights[CurrentLamp];
                lamp.Position += delta;
                Lights[CurrentLamp] = lamp;
            }
        }
    }
}