using System.Numerics;

namespace lab1
{
    public class Buffer<T>(int width, int height)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public Vector2 Size { get; } = new(width, height);
        public T[] Array { get; } = new T[width * height];

        public ref T this[int x, int y]
        {
            get => ref Array[x * Height + y];
        }
    }
}
