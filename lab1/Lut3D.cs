using System.IO;
using System.Numerics;
using static System.Globalization.CultureInfo;
using static System.Numerics.Vector3;
using static System.Single;
using static System.StringSplitOptions;

namespace lab1
{
    public class Lut3D
    {
        public int Size { get; } = 0;
        public Vector3[] Data { get; } = null!;

        public Lut3D(string path)
        {
            int count = 0;

            foreach (string line in File.ReadLines(path))
            {
                string[] tokens = line.Split(' ', RemoveEmptyEntries);

                if (TryParse(tokens[0], InvariantCulture, out float r) &&
                    TryParse(tokens[1], InvariantCulture, out float g) &&
                    TryParse(tokens[2], InvariantCulture, out float b))
                {
                    Data[count++] = Create(r, g, b);
                }
                else if (tokens[0] == "LUT_3D_SIZE")
                {
                    Size = int.Parse(tokens[1]);
                    Data = new Vector3[Size * Size * Size];
                }
            }
        }

        public Vector3 this[int i, int j, int k]
        {
            get => Data[i + (j + k * Size) * Size];
        }

        public Vector3 TetrahedralSample(Vector3 color)
        {
            color = Clamp(color, Zero, One);

            float r = color.X * (Size - 1);
            float g = color.Y * (Size - 1);
            float b = color.Z * (Size - 1);

            int r0 = (int)Floor(r);
            int g0 = (int)Floor(g);
            int b0 = (int)Floor(b);

            int r1 = (int)Ceiling(r);
            int g1 = (int)Ceiling(g);
            int b1 = (int)Ceiling(b);

            float dr = r - r0;
            float dg = g - g0;
            float db = b - b0;

            if (dr > dg)
            {
                if (dg > db)
                {
                    return (1 - dr) * this[r0, g0, b0] +
                           (dr - dg) * this[r1, g0, b0] +
                           (dg - db) * this[r1, g1, b0] +
                           (db - 0) * this[r1, g1, b1];
                }
                else if (dr > db)
                {
                    return (1 - dr) * this[r0, g0, b0] +
                           (dr - db) * this[r1, g0, b0] +
                           (db - dg) * this[r1, g0, b1] +
                           (dg - 0) * this[r1, g1, b1];
                }
                else
                {
                    return (1 - db) * this[r0, g0, b0] +
                           (db - dr) * this[r0, g0, b1] +
                           (dr - dg) * this[r1, g0, b1] +
                           (dg - 0) * this[r1, g1, b1];
                }
            }
            else
            {
                if (db > dg)
                {
                    return (1 - db) * this[r0, g0, b0] +
                           (db - dg) * this[r0, g0, b1] +
                           (dg - dr) * this[r0, g1, b1] +
                           (dr - 0) * this[r1, g1, b1];
                }
                else if (db > dr)
                {
                    return (1 - dg) * this[r0, g0, b0] +
                           (dg - db) * this[r0, g1, b0] +
                           (db - dr) * this[r0, g1, b1] +
                           (dr - 0) * this[r1, g1, b1];
                }
                else
                {
                    return (1 - dg) * this[r0, g0, b0] +
                           (dg - dr) * this[r0, g1, b0] +
                           (dr - db) * this[r1, g1, b0] +
                           (db - 0) * this[r1, g1, b1];
                }
            }
        }
    }
}