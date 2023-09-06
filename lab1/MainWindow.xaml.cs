using Rasterization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;
        List<Point> setedPixels = new List<Point>();
        Model model = new();

        float z = 25;
        float x = 0;
        float y = 5;

        float camera_x = 0;
        float camera_y = 5;
        float camera_z = 25;

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
                        model.AddFace(
                            line
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
                                .ToList()
                        );
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
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(new Vector3(camera_x, camera_y, camera_z), new Vector3(x, y, z - 25), new Vector3(0, 1, 0));
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 4, (float)bitmap.PixelWidth / (float)bitmap.PixelHeight, 0.1f, 1000);
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

        private void ClearBitmap()
        {
            foreach(Point p in setedPixels)
            {
                bitmap.ClearPixel((int)p.X, (int)p.Y);
            }
        }

        private void DrawLine(Point a, Point b)
        {
            double x1 = a.X;
            double y1 = a.Y;
            double x2 = b.X;
            double y2 = b.Y;

            int deltaX = Math.Abs((int)(x1 - x2));
            int deltaY = Math.Abs((int)(y1 - y2));

            int L = (int)Math.Max(deltaX, deltaY);

            if (L == 0)
            {
                if (x1 >= 0 && y1 >= 0 && x1 < bitmap.PixelWidth && y1 < bitmap.PixelHeight)
                {
                    bitmap.SetPixel((int)x1, (int)y1, Vector3.Zero);
                    setedPixels.Add(new(x1, y1));
                }
                return;
            }

            double dX = (x2 - x1) / L;
            double dY = (y2 - y1) / L;

            double x = x1;
            double y = y1;

            L++;
            while (L > 0)
            {
                if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight)
                {
                    bitmap.SetPixel((int)x, (int)y, Vector3.Zero);
                    setedPixels.Add(new(x, y));
                }
                x += dX;
                y += dY;
                L--;
            }
        }

        private void Draw()
        {
            Vector4[] vertices = TransformCoordinates();

            ClearBitmap();

            setedPixels.Clear();

            bitmap.Source.Lock();

            for (int i = 0; i < vertices.Length; i++)
            {
                int x = (int)vertices[i].X;
                int y = (int)vertices[i].Y;
                if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight)
                {
                    bitmap.SetPixel(x, y, Vector3.Zero);
                    setedPixels.Add(new(x, y));
                }
            }

            foreach (List<Vector3> face in model.Faces)
            {
                for (int i = 0; i < face.Count - 1; i++)
                {
                    DrawLine(
                        new(vertices[(int)face[i].X - 1].X, vertices[(int)face[i].X - 1].Y),
                        new(vertices[(int)face[i + 1].X - 1].X, vertices[(int)face[i + 1].X - 1].Y)
                    );
                }
                DrawLine(
                    new(vertices[(int)face[face.Count - 1].X - 1].X, vertices[(int)face[face.Count - 1].X - 1].Y),
                    new(vertices[(int)face[0].X - 1].X, vertices[(int)face[0].X - 1].Y)
                );
            }

            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.Source.Unlock();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bitmap = new((int)Grid.ActualWidth, (int)Grid.ActualHeight);
            Canvas.Source = bitmap.Source;
            ParseModelFromFile("./model/shovel_low.obj");
            Draw();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W:
                    {
                        z--;
                        camera_z--;
                        break;
                    }

                case Key.S:
                    {
                        camera_z++;
                        z++;
                        break;
                    }

                case Key.A:
                    {
                        x--;
                        camera_x--;
                        break;
                    }

                case Key.D:
                    {
                        x++;
                        camera_x++;
                        break;
                    }

                case Key.Q:
                    {
                        y--;
                        camera_y--;
                        break;
                    }
                case Key.E:
                    {
                        y++;
                        camera_y++;
                        break;
                    }


            }
            Draw();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point current_position = e.GetPosition(this);
                if (current_position.X < mouse_position.X)
                {
                    camera_x += 1;
                } else
                {
                    camera_x -= 1;
                }
                if (current_position.Y < mouse_position.Y)
                {
                    camera_y += 1;
                }
                else
                {
                    camera_y -= 1;
                }

            }
            mouse_position = e.GetPosition(this);
            Draw();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mouse_position = e.GetPosition(this);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                model.Scale += 0.1f;
            } else
            {
                model.Scale -= 0.1f;
            }
            Draw();
        }
    }
}
