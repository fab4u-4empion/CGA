using System.Numerics;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1.Effects
{
    public enum ToneMapper
    {
        Linear,
        Reinhard,
        ACES,
        AgX,
        PBRNeutral
    }

    public static class ToneMapping
    {
        public static float Exposure { get; set; } = 1f;
        public static ToneMapper ToneMapper { get; set; } = ToneMapper.AgX;

        public static Vector3 Linear(Vector3 color)
        {
            return Clamp(color, Zero, One);
        }

        public static Vector3 Reinhard(Vector3 color)
        {
            return color / (One + color);
        }

        #pragma warning disable format

        private static readonly Matrix4x4 ACESInputMat = new
        (
            0.59719f, 0.07600f, 0.02840f, 0f,
            0.35458f, 0.90834f, 0.13383f, 0f,
            0.04823f, 0.01566f, 0.83777f, 0f,
                  0f,       0f,       0f, 1f
        );

        private static readonly Matrix4x4 ACESOutputMat = new
        (
             1.60475f, -0.10208f, -0.00327f, 0f,
            -0.53108f,  1.10813f, -0.07276f, 0f,
            -0.07367f, -0.00605f,  1.07602f, 0f,
                   0f,        0f,        0f, 1f
        );

        #pragma warning restore format

        private static Vector3 RRTAndODTFit(Vector3 v)
        {
            Vector3 a = v * (v + Create(0.0245786f)) - Create(0.000090537f);
            Vector3 b = v * (0.983729f * v + Create(0.4329510f)) + Create(0.238081f);
            return a / b;
        }

        public static Vector3 AcesFilmic(Vector3 color)
        {
            color /= 0.6f;
            color = Transform(color, ACESInputMat);
            color = RRTAndODTFit(color);
            color = Transform(color, ACESOutputMat);
            color = Clamp(color, Zero, One);
            return color;
        }

        #pragma warning disable format

        private static readonly Matrix4x4 LinearRec709ToLinearFilmLightEGamut = new
        (
            0.5594630473276861f, 0.0762332608733703f, 0.0655375095152927f, 0f,
            0.3047758110283366f, 0.7879523952184488f, 0.1645427298716744f, 0f,
            0.1358129414038276f, 0.1357748488287584f, 0.7697415276874705f, 0f,
                             0f,                  0f,                  0f, 1f
        );

        #pragma warning restore format

        private static readonly Vector3 AgXMinEV = Create(-12.47393f);
        private static readonly Vector3 AgXMaxEV = Create(12.5260688117f);
        private static readonly Lut3D AgXBaseSrgb = new("AgX_Base_sRGB.cube");

        public static Vector3 AgX(Vector3 color)
        {
            color = Max(Zero, Transform(color, LinearRec709ToLinearFilmLightEGamut));
            color = Clamp((Log2(color) - AgXMinEV) / (AgXMaxEV - AgXMinEV), Zero, One);
            color = AgXBaseSrgb.TetrahedralSample(color);
            color = Create(Pow(color.X, 2.4f), Pow(color.Y, 2.4f), Pow(color.Z, 2.4f));
            return color;
        }

        public static Vector3 PBRNeutral(Vector3 color)
        {
            const float startCompression = 0.8f - 0.04f;
            const float desaturation = 0.15f;

            float x = Min(color.X, Min(color.Y, color.Z));
            float offset = x < 0.08f ? x - 6.25f * x * x : 0.04f;
            color -= Create(offset);

            float peak = Max(color.X, Max(color.Y, color.Z));
            if (peak < startCompression) return color;

            const float d = 1 - startCompression;
            float newPeak = 1 - d * d / (peak + d - startCompression);
            color *= newPeak / peak;

            float g = 1 - 1 / (desaturation * (peak - newPeak) + 1);
            return Lerp(color, newPeak * One, g);
        }

        public static Vector3 SrgbToLinear(Vector3 color)
        {
            return Create(color.X <= 0.04045f ? color.X / 12.92f : Pow((color.X + 0.055f) / 1.055f, 2.4f),
                          color.Y <= 0.04045f ? color.Y / 12.92f : Pow((color.Y + 0.055f) / 1.055f, 2.4f),
                          color.Z <= 0.04045f ? color.Z / 12.92f : Pow((color.Z + 0.055f) / 1.055f, 2.4f));
        }

        public static Vector3 LinearToSrgb(Vector3 color)
        {
            return Create(color.X <= 0.0031308f ? 12.92f * color.X : 1.055f * Pow(color.X, 1 / 2.4f) - 0.055f,
                          color.Y <= 0.0031308f ? 12.92f * color.Y : 1.055f * Pow(color.Y, 1 / 2.4f) - 0.055f,
                          color.Z <= 0.0031308f ? 12.92f * color.Z : 1.055f * Pow(color.Z, 1 / 2.4f) - 0.055f);
        }

        public static Vector3 CompressColor(Vector3 color)
        {
            color *= Exposure;
            color = ToneMapper switch
            {
                ToneMapper.Reinhard => Reinhard(color),
                ToneMapper.ACES => AcesFilmic(color),
                ToneMapper.AgX => AgX(color),
                ToneMapper.PBRNeutral => PBRNeutral(color),
                ToneMapper.Linear or _ => Linear(color)
            };
            color = LinearToSrgb(color);
            return color;
        }
    }
}