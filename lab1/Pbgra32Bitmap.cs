using System.Numerics;
using System.Windows.Media.Imaging;
using static System.Windows.Media.PixelFormats;

namespace lab1
{
    public unsafe class Pbgra32Bitmap
    {
        private byte* BackBuffer { get; set; }
        private int BackBufferStride { get; set; }
        private int BytesPerPixel { get; set; }

        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }
        public WriteableBitmap Source { get; private set; }

        public Pbgra32Bitmap(int pixelWidth, int pixelHeight)
        {
            Source = new(pixelWidth, pixelHeight, 96, 96, Pbgra32, null);
            InitializeProperties();
        }

        public Pbgra32Bitmap(BitmapSource source)
        {
            Source = new(source.Format != Pbgra32 ? new FormatConvertedBitmap(source, Pbgra32, null, 0) : source);
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            PixelWidth = Source.PixelWidth;
            PixelHeight = Source.PixelHeight;
            BackBuffer = (byte*)Source.BackBuffer;
            BackBufferStride = Source.BackBufferStride;
            BytesPerPixel = Source.Format.BitsPerPixel / 8;
        }

        private byte* GetPixelAddress(int x, int y)
        {
            return BackBuffer + y * BackBufferStride + x * BytesPerPixel;
        }

        public Vector3 GetPixel(int x, int y)
        {
            byte* pixel = GetPixelAddress(x, y);
            float b = pixel[0] / 255f;
            float g = pixel[1] / 255f;
            float r = pixel[2] / 255f;
            return new(r, g, b);
        }

        public void SetPixel(int x, int y, Vector3 color)
        {
            byte* pixel = GetPixelAddress(x, y);
            pixel[0] = (byte)(255 * color.Z);
            pixel[1] = (byte)(255 * color.Y);
            pixel[2] = (byte)(255 * color.X);
            pixel[3] = 255;
        }
    }
}