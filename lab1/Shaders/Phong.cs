using System.Numerics;

namespace lab1.Shaders
{
    public class Phong
    {
        private static float ka = 0.3f;
        private static float kd = 0.6f;
        private static float ks = 0.6f;

        private static float a = 15f;

        public static Vector3 GetPixelColor(Vector3 baseColor, Vector3 normal, Vector3 light, Vector3 look)
        {
            Vector3 ambient = baseColor * ka;
            Vector3 diffuse = baseColor * kd * float.Max(Vector3.Dot(normal, light), 0);

            Vector3 R = Vector3.Normalize(light - 2 * Vector3.Dot(light, normal) * normal);
            Vector3 specular = baseColor * ks * float.Pow(float.Max(Vector3.Dot(R, look), 0), a);

            Vector3 color = ambient + diffuse + specular;

            return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        }
    }
}
