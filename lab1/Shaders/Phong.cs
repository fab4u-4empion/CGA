using System.Numerics;

namespace lab1.Shaders
{
    public class Phong
    {
        private static float ka = 0.3f;
        private static float kd = 1f;
        private static Vector3 Is = new(1, 1, 1);

        private static float a = 20f;

        public static Vector3 GetPixelColor(Vector3 baseColor, Vector3 n, float spec, Vector3 light, Vector3 camera, Vector3 p)
        {
            Vector3 L = Vector3.Normalize(light - p);
            Vector3 V = Vector3.Normalize(camera - p);
            Vector3 N = Vector3.Normalize(n);

            Vector3 ambient = baseColor * ka;
            Vector3 diffuse = baseColor * kd * float.Max(Vector3.Dot(N, L), 0);
            Vector3 specular = Is * spec * float.Pow(
                float.Max(
                    Vector3.Dot(
                        Vector3.Normalize(Vector3.Reflect(-L, N)), 
                        V
                    ), 
                    0
                ), 
                a
            );

            Vector3 color = ambient + diffuse + specular;

            return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        }
    }
}
