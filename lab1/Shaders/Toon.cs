using System.Numerics;
using static System.Numerics.Vector3;
using static System.Single;
using static lab1.LightingConfig;

namespace lab1.Shaders
{
    public class Toon
    {
        private static Vector3[] colors = [
            new(0f, 0f, 0f), new(0.214f, 0.214f, 0.214f), new(0.523f, 0.523f, 0.523f), new(1f, 1f, 1f), new(1f, 0f, 1f), new(0.214f, 0f, 0.214f), new(1f, 0f, 0f), new(0.214f, 0f, 0f), new(1f, 1f, 0f), new(0.214f, 0.214f, 0f), new(0f, 1f, 0f), new(0f, 0.214f, 0f), new(0f, 1f, 1f), new(0f, 0.214f, 0.214f), new(0f, 0f, 1f), new(0f, 0f, 0.214f), new(0.609f, 0.107f, 0.107f), new(0.871f, 0.216f, 0.216f), new(0.955f, 0.216f, 0.168f), new(0.815f, 0.305f, 0.194f), new(1f, 0.351f, 0.194f), new(0.716f, 0.007f, 0.045f), new(0.445f, 0.016f, 0.016f), new(1f, 0.527f, 0.597f), new(1f, 0.468f, 0.533f), new(1f, 0.141f, 0.457f), new(1f, 0.007f, 0.291f), new(0.57f, 0.007f, 0.235f), new(0.709f, 0.162f, 0.291f), new(1f, 0.351f, 0.194f), new(1f, 0.212f, 0.08f), new(1f, 0.125f, 0.063f), new(1f, 0.06f, 0f), new(1f, 0.262f, 0f), new(1f, 0.376f, 0f), new(1f, 0.679f, 0f), new(1f, 1f, 0.745f), new(1f, 0.955f, 0.611f), new(0.955f, 0.955f, 0.645f), new(1f, 0.863f, 0.665f), new(1f, 0.776f, 0.462f), new(1f, 0.701f, 0.484f), new(0.854f, 0.807f, 0.402f), new(0.871f, 0.791f, 0.262f), new(0.509f, 0.474f, 0.147f), new(0.791f, 0.791f, 0.955f), new(0.687f, 0.521f, 0.687f), new(0.724f, 0.351f, 0.724f), new(0.854f, 0.223f, 0.854f), new(0.701f, 0.162f, 0.672f), new(0.49f, 0.091f, 0.651f), new(0.291f, 0.162f, 0.709f), new(0.254f, 0.024f, 0.76f), new(0.296f, 0f, 0.651f), new(0.319f, 0.032f, 0.604f), new(0.258f, 0f, 0.258f), new(0.07f, 0f, 0.223f), new(0.144f, 0.102f, 0.611f), new(0.065f, 0.047f, 0.258f), new(1f, 0.94f, 0.716f), new(1f, 0.832f, 0.611f), new(1f, 0.776f, 0.553f), new(1f, 0.731f, 0.417f), new(0.914f, 0.731f, 0.451f), new(0.731f, 0.48f, 0.242f), new(0.645f, 0.457f, 0.262f), new(0.502f, 0.275f, 0.275f), new(0.905f, 0.371f, 0.117f), new(0.701f, 0.376f, 0.014f), new(0.48f, 0.238f, 0.003f), new(0.611f, 0.235f, 0.05f), new(0.645f, 0.141f, 0.013f), new(0.258f, 0.06f, 0.007f), new(0.351f, 0.085f, 0.026f), new(0.376f, 0.023f, 0.023f), new(0.417f, 1f, 0.028f), new(0.212f, 1f, 0f), new(0.201f, 0.973f, 0f), new(0.032f, 0.611f, 0.032f), new(0.314f, 0.964f, 0.314f), new(0.279f, 0.854f, 0.279f), new(0f, 0.955f, 0.323f), new(0f, 1f, 0.212f), new(0.045f, 0.451f, 0.165f), new(0.027f, 0.258f, 0.095f), new(0.016f, 0.258f, 0.016f), new(0f, 0.127f, 0f), new(0.323f, 0.611f, 0.032f), new(0.147f, 0.271f, 0.017f), new(0.091f, 0.147f, 0.028f), new(0.133f, 0.611f, 0.402f), new(0.275f, 0.502f, 0.275f), new(0.014f, 0.445f, 0.402f), new(0f, 0.258f, 0.258f), new(1f, 0.955f, 0.955f), new(0.871f, 1f, 0.871f), new(0.914f, 1f, 0.955f), new(0.871f, 1f, 1f), new(0.871f, 0.94f, 1f), new(0.94f, 0.94f, 1f), new(0.914f, 0.914f, 0.914f), new(1f, 0.914f, 0.854f), new(0.914f, 0.914f, 0.716f), new(0.982f, 0.914f, 0.791f), new(1f, 0.955f, 0.871f), new(1f, 1f, 0.871f), new(0.955f, 0.832f, 0.679f), new(0.955f, 0.871f, 0.791f), new(1f, 0.871f, 0.914f), new(1f, 0.776f, 0.752f), new(0.716f, 0.716f, 0.716f), new(0.651f, 0.651f, 0.651f), new(0.397f, 0.397f, 0.397f), new(0.141f, 0.141f, 0.141f), new(0.185f, 0.246f, 0.319f), new(0.162f, 0.216f, 0.279f), new(0.028f, 0.078f, 0.078f),
        ];

        private static Vector3 TransformColor(Vector3 color)
        {
            int index = 0;
            float nDist = float.MaxValue;

            for (int i = 0; i < colors.Length; i++)
            {
                float dist = Distance(color, colors[i]);
                if (dist < nDist)
                {
                    index = i;
                    nDist = dist;
                }
            }

            return colors[index];
        }

        public static Vector3 GetPixelColor(Vector3 baseColor, Vector3 n, Vector3 p, Vector3 camera, Vector3 emission)
        {
            Vector3 color = Zero;
            baseColor = TransformColor(baseColor);
            Vector3 N = Normalize(n);

            int lvl = 2;
            float step = 1f / lvl;

            for (int i = 0; i < Lights.Count; i++)
            {
                Lamp lamp = Lights[i];

                Vector3 L = lamp.GetL(p);

                float dot = Floor(Max(Dot(N, L), 0) * (lvl + 1)) * step;

                color += baseColor * lamp.GetIrradiance(p) * dot / Pi;
            }

            color += baseColor * AmbientIntensity * 0.1f + emission * EmissionIntensity;

            return color;
        }
    }
}
