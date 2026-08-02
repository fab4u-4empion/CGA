using System;
using System.Numerics;

namespace lab1
{
    public class Buffer<T>(int width, int height)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int Length { get => Array.Length; }
        public Vector2 Size { get; } = new(width, height);
        private T[] Array { get; } = new T[width * height];

        public ref T this[int i]
        {
            get => ref Array[i];
        }

        public ref T this[int x, int y]
        {
            get => ref Array[y * Width + x];
        }

        public Span<T> GetRow(int y)
        {
            return Array.AsSpan(y * Width, Width);
        }

        public static implicit operator T[](Buffer<T> buffer) => buffer.Array;
        public static implicit operator Array(Buffer<T> buffer) => buffer.Array;
    }
}