using System.Numerics;

namespace lab1.Shaders
{
    public class Phong
    {
        private static Vector3 ka = new(0.5f, 0.5f, 0.5f);
        private static Vector3 kd = new(0.5f, 0.5f, 0.5f);
        private static Vector3 ks = new(1, 1, 1);

        private static float a = 20f;

        public static Vector3 GetPixelColor(Vector3 baseColor, Vector3 normal, Vector3 spec, Vector3 light, Vector3 look)
        {
            Vector3 ambient = baseColor * ka;
            Vector3 diffuse = baseColor * kd * float.Max(Vector3.Dot(normal, light), 0);
            Vector3 specular = baseColor * spec * float.Pow(
                float.Max(
                    Vector3.Dot(
                        Vector3.Normalize(Vector3.Reflect(light, normal)), 
                        look
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
