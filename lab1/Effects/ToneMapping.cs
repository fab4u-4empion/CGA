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

        private static readonly Matrix4x4 LinearRec2020ToLinearSrgb = new
        (
             1.6605f, -0.1246f, -0.0182f, 0f,
            -0.5876f,  1.1329f, -0.1006f, 0f,
            -0.0728f, -0.0083f,  1.1187f, 0f,
                  0f,       0f,       0f, 1f
        );

        private static readonly Matrix4x4 LinearSrgbToLinearRec2020 = new
        (
            0.6274f, 0.0691f, 0.0164f, 0f,
            0.3293f, 0.9195f, 0.0880f, 0f,
            0.0433f, 0.0113f, 0.8956f, 0f,
                 0f,      0f,      0f, 1f
        );

        private static readonly Matrix4x4 AgXInsetMatrix = new
        (
            0.8566271533159830f, 0.1373189729298470f, 0.1118982129999500f, 0f,
            0.0951212405381588f, 0.7612419906025910f, 0.0767994186031903f, 0f,
            0.0482516061458583f, 0.1014390364675620f, 0.8113023683968590f, 0f,
                             0f,                  0f,                  0f, 1f
        );

        private static readonly Matrix4x4 AgXOutsetMatrix = new
        (
             1.1271005818144368f, -0.1413297634984383f, -0.1413297634984383f, 0f,
            -0.1106066430966032f,  1.1578237022162720f, -0.1106066430966029f, 0f,
            -0.0164939387178346f, -0.0164939387178343f,  1.2519364065950405f, 0f,
                              0f,                   0f,                   0f, 1f
        );

        private static readonly Vector3 AgXMinEV = Create(-12.47393f);
        private static readonly Vector3 AgXMaxEV = Create(4.026069f);

        private static Vector3 AgXDefaultContrastApprox(Vector3 x)
        {
            Vector3 x2 = x * x;
            Vector3 x4 = x2 * x2;
            Vector3 x6 = x4 * x2;
            return - 17.860f * x6 * x
                   + 78.010f * x6
                   - 126.70f * x4 * x
                   + 92.060f * x4
                   - 28.720f * x2 * x
                   + 4.3610f * x2
                   - 0.1718f * x
                   + Create(0.002857f);
        }

        #pragma warning restore format

        public static Vector3 AgX(Vector3 color)
        {
            color = Transform(color, LinearSrgbToLinearRec2020);
            color = Transform(Max(Zero, color), AgXInsetMatrix);
            color = Clamp((Log2(color) - AgXMinEV) / (AgXMaxEV - AgXMinEV), Zero, One);
            color = AgXDefaultContrastApprox(color);
            color = Transform(color, AgXOutsetMatrix);
            color = Create(Pow(color.X, 2.4f), Pow(color.Y, 2.4f), Pow(color.Z, 2.4f));
            color = Transform(color, LinearRec2020ToLinearSrgb);
            color = Clamp(color, Zero, One);
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