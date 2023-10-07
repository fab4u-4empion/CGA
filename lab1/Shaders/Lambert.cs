using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace lab1.Shaders
{
    public class Lambert
    {
        private static Vector3 GetColor(Vector3 normal, Vector3 color, Vector3 light)
        {
            float c = float.Max(Vector3.Dot(normal, light), 0);
            return Vector3.Multiply(color, c);
        }

        public static Vector3 GetAverageColor(Vector3 color1, Vector3 color2, Vector3 color3)
        {
            float sumR = color1.X + color2.X + color3.X;
            float sumG = color1.Y + color2.Y + color3.Y;
            float sumB = color1.Z + color2.Z + color3.Z;

            return Vector3.Divide(new(sumR, sumG, sumB), 3);
        }

        public static Vector3 GetFaceColor(Vector3 n1, Vector3 n2, Vector3 n3, Vector3 baseColor, Vector3 lightVector)
        {
            Vector3 color1 = GetColor(n1, baseColor, lightVector);
            Vector3 color2 = GetColor(n2, baseColor, lightVector);
            Vector3 color3 = GetColor(n3, baseColor, lightVector);

            return GetAverageColor(color1, color2, color3);
        }
    }
}
