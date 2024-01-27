using System;
using System.Numerics;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public enum ToneMappingMode
    {
        ACES,
        AgX
    }

    public enum AgXLookMode
    {
        DEFAULT,
        PUNCHY,
        GOLDEN
    }

    public class ToneMapping
    {
        public static ToneMappingMode Mode = ToneMappingMode.ACES;
        public static AgXLookMode LookMode = AgXLookMode.DEFAULT;

        public static Vector3 AcesFilmic(Vector3 color)
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

            return Clamp(color, Zero, One);
        }

        public static Vector3 AgX(Vector3 color)
        {
            color = new(
                Dot(new(0.856627153315983f, 0.0951212405381588f, 0.0482516061458583f), color),
                Dot(new(0.137318972929847f, 0.761241990602591f, 0.101439036467562f), color),
                Dot(new(0.11189821299995f, 0.0767994186031903f, 0.811302368396859f), color)
            );

            float min_ev = -12.47393f;
            float max_ev = 4.026069f;

            color = new(Log2(color.X), Log2(color.Y), Log2(color.Z));
            color = (color - min_ev * One) / (max_ev - min_ev);

            Vector3 x2 = color * color;
            Vector3 x4 = x2 * x2;
            Vector3 x6 = x4 * x2;

            color = 
                - 17.86f * x6 * color
                + 78.01f * x6
                - 126.7f * x4 * color
                + 92.06f * x4
                - 28.72f * x2 * color
                + 4.361f * x2
                - 0.1718f * color
                + 0.002857f * One;

            return color;
        }

        public static Vector3 AgXEotf(Vector3 color)
        {
            color = new(
                Dot(new(1.1271005818144366432f, -0.1106066430966032116f, -0.016493938717834568156f), color),
                Dot(new(-0.14132976349843826566f, 1.1578237022162717623f, -0.016493938717834252651f), color),
                Dot(new(-0.14132976349843824773f, -0.11060664309660291788f, 1.2519364065950402828f), color)
            );

            color = new(Pow(color.X, 2.2f), Pow(color.Y, 2.2f), Pow(color.Z, 2.2f));

            return Clamp(color, Zero, One);
        }

        public static Vector3 AgXLook(Vector3 color)
        {
            Vector3 lw = new(0.2126f, 0.7152f, 0.0722f);
            Vector3 luma = new(Dot(color, lw));

            Vector3 slope = One;
            Vector3 power = One;
            float sat = 1.0f;

            if (LookMode == AgXLookMode.PUNCHY)
            {
                slope = new(1.0f);
                power = new(1.35f, 1.35f, 1.35f);
                sat = 1.4f;
            }

            if (LookMode == AgXLookMode.GOLDEN)
            {
                slope = new(1.0f, 0.9f, 0.5f);
                power = new(0.8f);
                sat = 0.8f;
            }

            color *= slope;

            color = new(Pow(color.X, power.X), Pow(color.Y, power.Y), Pow(color.Z, power.Z));
            return luma + sat * (color - luma);
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

        public static Vector3 CompressColor(Vector3 color)
        {
            if (Mode == ToneMappingMode.ACES) return LinearToSrgb(AcesFilmic(color));
            if (Mode == ToneMappingMode.AgX) return LinearToSrgb(AgXEotf(AgXLook(AgX(color))));
            return color;
        }
    }
}
