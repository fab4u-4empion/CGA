using Rasterization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;
        Model model = new();

        public MainWindow()
        {
            InitializeComponent();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bitmap = new((int)Grid.ActualWidth, (int)Grid.ActualHeight);
            Canvas.Source = bitmap.Source;

            using (StreamReader reader = new("./model/shovel_low.obj"))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine();
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
                }
            }

            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(1);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(0, 0, 0);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(0, 0, 0);
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(new Vector3(0, 5, 30), new Vector3(0, 5, 0), new Vector3(0, 1, 0));
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 4, (float)bitmap.PixelWidth / bitmap.PixelHeight, 0.1f, 1000);
            Matrix4x4 modelViewProjectionMatrix = modelMatrix * viewMatrix * projectionMatrix;
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(0, 0, bitmap.PixelWidth, bitmap.PixelHeight, 0, 1);

            bitmap.Source.Lock();

            Vector4[] windowVertices = new Vector4[model.Vertices.Count];
            for (int i = 0; i < windowVertices.Length; i++)
            {
                windowVertices[i] = Vector4.Transform(model.Vertices[i], modelViewProjectionMatrix);
                windowVertices[i] /= windowVertices[i].W;
                windowVertices[i] = Vector4.Transform(windowVertices[i], viewportMatrix);
                int x = (int)windowVertices[i].X;
                int y = (int)windowVertices[i].Y;
                if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight)
                {
                    bitmap.SetPixel((int)windowVertices[i].X, (int)windowVertices[i].Y, Vector3.Zero);
                }
            }

            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.Source.Unlock();
        }
    }
}
