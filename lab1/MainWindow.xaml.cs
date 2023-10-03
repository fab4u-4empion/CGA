﻿using Rasterization;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Threading;

namespace lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;

        Model model = new();
        Camera camera = new();
        ZBuffer ZBuffer;
        Vector3 Light = Vector3.Normalize(new(5, 10, 15));
        int smoothing = 1;

        SpinLock[,] spins;

        Point mouse_position;
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ParseModelFromFile(string path)
        {
            using (StreamReader reader = new(path))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine().Trim();

                    if (line.StartsWith("v "))
                    {
                        List<float> coordinates = line
                            .Remove(0, 2)
                            .Split(" ")
                            .Select(c =>
                                float.Parse(c, CultureInfo.InvariantCulture)
                             )
                            .ToList();
                        model.AddVertex(coordinates[0], coordinates[1], coordinates[2]);
                    }

                    if (line.StartsWith("f "))
                    {
                        List<Vector3> vertices = line
                                .Remove(0, 2)
                                .Split(" ")
                                .Select(v =>
                                {
                                    List<int> indexes = v
                                        .Split("/")
                                        .Select(i => int.Parse(i, CultureInfo.InvariantCulture))
                                        .ToList();
                                    return new Vector3(indexes[0], indexes[1], indexes[2]);
                                }
                                )
                                .ToList();
                        for (int i = 0; i < vertices.Count - 2;  i++)
                        {
                            model.AddFace(new() {
                                vertices[0],
                                vertices[i + 1],
                                vertices[i + 2]
                            });
                        }
                    }

                    if (line.StartsWith("vn "))
                    {
                        List<float> coordinates = line
                            .Remove(0, 3)
                            .Split(" ")
                            .Select(c =>
                                float.Parse(c, CultureInfo.InvariantCulture)
                             )
                            .ToList();
                        model.AddNormal(coordinates[0], coordinates[1], coordinates[2]);
                    }
                }
            }
        }

        private Vector4[] TransformCoordinates()
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(model.Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(model.Yaw, model.Pitch, model.Roll);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(model.Translation);
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(camera.FoV, (float)bitmap.PixelWidth / (float)bitmap.PixelHeight, 0.1f, 1000);
            Matrix4x4 modelViewProjectionMatrix = modelMatrix * viewMatrix * projectionMatrix;
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(0, 0, bitmap.PixelWidth, bitmap.PixelHeight, 0, 1);

            Vector4[] windowVertices = new Vector4[model.Vertices.Count];
            for (int i = 0; i < windowVertices.Length; i++)
            {
                windowVertices[i] = Vector4.Transform(model.Vertices[i], modelViewProjectionMatrix);
                windowVertices[i] /= windowVertices[i].W;
                windowVertices[i] = Vector4.Transform(windowVertices[i], viewportMatrix);
            }

            return windowVertices;
        }

        private Vector3 GetColor(Vector3 normal, Vector3 color)
        {
            float c = Math.Max(Vector3.Dot(normal, Light), 0);
            return Vector3.Multiply(color, c);
        }

        public static Vector3 GetAverageColor(Vector3 color1, Vector3 color2, Vector3 color3)
        {
            float sumR = color1.X + color2.X + color3.X;
            float sumG = color1.Y + color2.Y + color3.Y;
            float sumB = color1.Z + color2.Z + color3.Z;

            return Vector3.Divide(new(sumR, sumG, sumB), 3);
        }

        private Vector3 GetFaceColor(List<Vector3> face, Vector3 color)
        {
            Vector3 normal1 = model.Normals[(int)face[0].Z - 1];
            Vector3 normal2 = model.Normals[(int)face[1].Z - 1];
            Vector3 normal3 = model.Normals[(int)face[2].Z - 1];

            Vector3 color1 = GetColor(normal1, color);
            Vector3 color2 = GetColor(normal2, color);
            Vector3 color3 = GetColor(normal3, color);

            return GetAverageColor(color1, color2, color3);
        }

        private float GetNormal(Vector4[] vertices)
        {
            Vector4 v1 = vertices[0];
            Vector4 v2 = vertices[1];
            Vector4 v3 = vertices[2];

            Vector2 s1 = new(v2.X - v1.X, v2.Y - v1.Y);
            Vector2 s2 = new(v3.X - v2.X, v3.Y - v2.Y);
            return s1.X * s2.Y - s1.Y * s2.X;
        }

        private List<float> Interpolate(float i0, float d0, float i1, float d1)
        {

            List<float> result = null;
            if (i0 == i1)
            {
                result = new(1)
                {
                    d0
                };
                return result;
            }
            int size = (int)i1 - (int)i0 + 1;
            result = new(size);
            

            float a = (d1 - d0) / (i1 - i0);
            float d = d0;
            for (int i = (int)i0; i <= (int)i1; i++, d += a)
            {
                result.Add(d);
            }
            return result;
        }

        private void DrawLine(Vector4 a, Vector4 b, Vector3 color)
        {
            if (Math.Abs(b.X - a.X) > Math.Abs(b.Y - a.Y))
            {
                if (a.X > b.X)
                    (b, a) = (a, b);
                List<float> ys = Interpolate(a.X, a.Y, b.X, b.Y);
                List<float> zs = Interpolate(a.X, a.Z, b.X, b.Z);
                for (int x = (int)a.X; x <= (int)b.X; x++)
                {
                    DrawPixel(x, (int)ys[x - (int)a.X], zs[x - (int)a.X], color);
                }
            } 
            else
            {
                if (a.Y > b.Y)
                    (b, a) = (a, b);
                List<float> xs = Interpolate(a.Y, a.X, b.Y, b.X);
                List<float> zs = Interpolate(a.Y, a.Z, b.Y, b.Z);
                for (int y = (int)a.Y; y <= (int)b.Y; y++)
                {
                    DrawPixel((int)xs[y - (int)a.Y], y, zs[y - (int)a.Y], color);
                }
            }
        }

        private void FillFace(Vector4 a, Vector4 b, Vector4 c, Vector3 color)
        {
            if (b.Y < a.Y)
                (a, b) = (b, a);
            if (c.Y < a.Y)
                (a, c) = (c, a);
            if (c.Y < b.Y)
                (b, c) = (c, b);

            Vector4 k1 = (c - a) / (c.Y - a.Y);
            Vector4 k2 = (b - a) / (b.Y - a.Y);
            Vector4 k3 = (c - b) / (c.Y - b.Y);

            int top = int.Max((int)float.Ceiling(a.Y), 0);
            int bottom = int.Min((int)float.Ceiling(c.Y), bitmap.PixelHeight);

            for (int y = top; y < bottom; y++)
            {
                Vector4 p1 = a + (y - a.Y) * k1;
                Vector4 p2 = y < b.Y ? a + (y - a.Y) * k2 : b + (y - b.Y) * k3;
                if (p1.X > p2.X)
                    (p1, p2) = (p2, p1);

                Vector4 k = (p2 - p1) / (p2.X - p1.X);
                int left = int.Max((int)float.Ceiling(p1.X), 0);
                int right = int.Min((int)float.Ceiling(p2.X), bitmap.PixelWidth);

                for (int x = left; x < right; x++) { 
                    Vector4 p = p1 + (x - p1.X) * k;
                    DrawPixel(x, y, p.Z, color);
                }
            }
        }

        private void DrawFace(List<Vector3> face, Vector4[] vertices)
        {
            Vector3 color = GetFaceColor(face, new(0.5f, 0.5f, 0.5f));

            //DrawLine(vertices[0], vertices[1], new(0, 0, 0));
            //DrawLine(vertices[0], vertices[2], new(0, 0, 0));
            //DrawLine(vertices[1], vertices[2], new(0, 0, 0));

            FillFace(vertices[0], vertices[1], vertices[2], color);
        }

        private void DrawPixel(int x, int y, float z, Vector3 color)
        {
            if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight && z > 0 && z < 1)
            {
                bool gotLock = false;
                bool flag = true;
                while (flag)
                {
                    try
                    {
                        spins[x, y].Enter(ref gotLock);
                        if (gotLock && z <= ZBuffer[x, y])
                        {
                            bitmap.SetPixel(x, y, color);
                            ZBuffer[x, y] = z;
                        }
                    }
                    finally
                    {
                        if (gotLock)
                        {
                            spins[x, y].Exit();
                            flag = false;
                        }
                    }
                }
            }
        }

        private void Draw()
        {
            
            DateTime t = DateTime.Now;
            bitmap.Clear();
            Vector4[] vertices = TransformCoordinates();
            
            ZBuffer.Clear();
            
            bitmap.Source.Lock();
            
            Parallel.ForEach(model.Faces, face =>
            {

                Vector4[] faceVerts = new Vector4[3];
                faceVerts[0] = vertices[(int)face[0].X - 1];
                faceVerts[1] = vertices[(int)face[1].X - 1];
                faceVerts[2] = vertices[(int)face[2].X - 1];
    
                if (GetNormal(faceVerts) <= 0)
                    DrawFace(face, faceVerts);
            });
       
            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            
            bitmap.Source.Unlock();
            Time.Content = ((DateTime.Now - t).Milliseconds).ToString() + " ms";


        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bitmap = new((int)Grid.ActualWidth * smoothing, (int)Grid.ActualHeight * smoothing);
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];
            for (int i = 0; i < bitmap.PixelWidth; i++)
            {
                for (int j = 0; j < bitmap.PixelHeight; j++)
                {
                    spins[i, j] = new();
                }
            }
            //model.Translation = new(0, -10, 0);
            //ParseModelFromFile("./model/tree.obj");
            ParseModelFromFile("./model/shovel_low.obj");
            model.Translation = new(0, -6, -3);
            //ParseModelFromFile("./model/doom.obj");
            //model.Translation = new(0, -1, 0);
            //model.Translation = new(-54, -54, 0);
            //ParseModelFromFile("./model/cube.obj");
            ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            Draw();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point current_position = e.GetPosition(this);
                camera.UpdatePosition(0, 0, (float)(current_position.X - mouse_position.X) * -0.5f);
                camera.UpdatePosition(0, (float)(current_position.Y - mouse_position.Y) * -0.5f, 0);
                Draw();
            }
            mouse_position = e.GetPosition(this);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mouse_position = e.GetPosition(this);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                camera.UpdatePosition(-1f, 0, 0);
            } else
            {
                camera.UpdatePosition(1f, 0, 0);
            }
            Draw();
        }
    }
}
