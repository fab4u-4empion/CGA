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
                Dot(new(0.842479062253094f, 0.0784335999999992f, 0.0792237451477643f), color),
                Dot(new(0.0423282422610123f, 0.878468636469772f, 0.0791661274605434f), color),
                Dot(new(0.0423756549057051f, 0.0784336f, 0.879142973793104f), color)
            );

            float min_ev = -12.47393f;
            float max_ev = 4.026069f;

            color = Clamp(new(Log2(color.X), Log2(color.Y), Log2(color.Z)), min_ev * One, max_ev * One);
            color = (color - min_ev * One) / (max_ev - min_ev);

            Vector3 x2 = color * color;
            Vector3 x4 = x2 * x2;

            color = 
                +15.5f * x4 * x2
                - 40.14f * x4 * color
                + 31.96f * x4
                - 6.868f * x2 * color
                + 0.4298f * x2
                + 0.1191f * color
                - 0.00232f * One;

            return color;
        }

        public static Vector3 AgXEotf(Vector3 color)
        {
            color = new(
                Dot(new(1.19687900512017f, -0.0980208811401368f, -0.0990297440797205f), color),
                Dot(new(-0.0528968517574562f, 1.15190312990417f, -0.0989611768448433f), color),
                Dot(new(-0.0529716355144438f, -0.0980434501171241f, 1.15107367264116f), color)
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

            color = color * slope;

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
