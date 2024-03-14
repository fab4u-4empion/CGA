using Rasterization;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Text;

namespace lab1
{
    public class HDRTexture
    {
        private Buffer<Vector3>? Source;

        public int Width;
        public int Height;

        public static float Angle;

        private static string ReadLine(BinaryReader reader)
        {
            StringBuilder sb = new();

            while(reader.ReadChar() is char c && c != '\n')
                sb.Append(c);

            return sb.ToString();
        }

        private static unsafe float ChangeEndianness(float f)
        {
            uint i = *(uint*)&f;
            i = (i & 0x000000FFU) << 24 | (i & 0x0000FF00U) << 8 |
                (i & 0x00FF0000U) >> 8 | (i & 0xFF000000U) >> 24;
            return *(float*)&i;
        }

        public void Open(string filename)
        {
            using (Stream stream = File.OpenRead(filename))
            using (BinaryReader reader = new(stream, Encoding.Default, true))
            {
                ReadLine(reader);

                int[] size = ReadLine(reader)
                    .Split(" ")
                    .Select(e => int.Parse(e))
                    .ToArray();
                Source = new(size[0], size[1]);
                (Width, Height) = (Source.Width, Source.Height);

                float order = float.Parse(ReadLine(reader), CultureInfo.InvariantCulture);

                for (int y = Source.Height - 1; y > -1; y--)
                {
                    for (int x = 0; x < Source.Width; x++)
                    {
                        float r = reader.ReadSingle();
                        float g = reader.ReadSingle();
                        float b = reader.ReadSingle();

                        if (order == 1)
                        {
                            r = ChangeEndianness(r);
                            g = ChangeEndianness(g);
                            b = ChangeEndianness(b);
                        }

                        Source[x, y] = new(r, g, b);
                    }
                }
            }
        }

        public Pbgra32Bitmap ToLDR()
        {
            Pbgra32Bitmap bmp = new(Width, Height);

            bmp.Source.Lock();

            for (int x = 0; x < Source.Width; x++)
                for (int y = 0; y < Source.Height; y++)
                    bmp.SetPixel(x, y, ToneMapping.LinearToSrgb(ToneMapping.AcesFilmic(Source[x, y])));

            bmp.Source.AddDirtyRect(new(0, 0, Source.Width, Source.Height));
            bmp.Source.Unlock();

            return bmp;
        }

        public Vector3 GetColor(Vector3 N)
        {
            float theta = float.Acos(float.Clamp(N.Y, -1, 1));
            float phi = float.Atan2(N.X, -N.Z) + float.Pi + Angle;

            float x = phi / (2 * float.Pi) * Source.Width - 0.5f;
            float y = theta / float.Pi * Source.Height - 0.5f;

            int x0 = (int)float.Floor(x);
            int y0 = (int)float.Floor(y);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float x_ratio = x - x0;
            float y_ratio = y - y0;

            x0 &= (Source.Width - 1);
            x1 &= (Source.Width - 1);

            y0 = int.Max(0, y0);
            y1 = int.Min(Source.Height - 1, y1);

            return Vector3.Lerp(
                Vector3.Lerp(Source[x0, y0], Source[x1, y0], x_ratio),
                Vector3.Lerp(Source[x0, y1], Source[x1, y1], x_ratio),
                y_ratio
            );
        }

        public Vector3 GetColor(float u, float v)
        {
            float x = u * Source.Width - 0.5f;
            float y = v * Source.Height - 0.5f;

            int x0 = (int)float.Floor(x);
            int y0 = (int)float.Floor(y);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float x_ratio = x - x0;
            float y_ratio = y - y0;

            x0 = int.Max(0, x0); ;
            x1 = int.Min(Source.Width - 1, x1);

            y0 = int.Max(0, y0);
            y1 = int.Min(Source.Height - 1, y1);

            return Vector3.Lerp(
                Vector3.Lerp(Source[x0, y0], Source[x1, y0], x_ratio),
                Vector3.Lerp(Source[x0, y1], Source[x1, y1], x_ratio),
                y_ratio
            );
        }
    }
}
