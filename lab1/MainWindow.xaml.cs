using Rasterization;
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

        private void ClearBitmap()
        {


            bitmap.Clear();

            //for (int i = 0; i < bitmap.PixelWidth; i++)
            //{
            //    for (int j = 0; j < bitmap.PixelHeight; j++)
            //    {
            //        bitmap.ClearPixel(i, j);
            //    }
            //}
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

        private Vector3 GetNormal(List<Vector4> vertices)
        {
            Vector4 v1 = vertices[0];
            Vector4 v2 = vertices[1];
            Vector4 v3 = vertices[2];

            Vector3 s1 = new(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
            Vector3 s2 = new(v3.X - v2.X, v3.Y - v2.Y, v3.Z - v2.Z);
            return Vector3.Normalize(Vector3.Cross(s1, s2));
        }

        private List<float> Interpolate(float i0, float d0, float i1, float d1)
        {
            List<float> result = new();
            if (i0 == i1)
            {
                result.Add(d0);
                return result;
            }

            float a = (d1 - d0) / (i1 - i0);
            float d = d0;
            for (int i = (int)i0; i <= (int)i1; i++)
            {
                result.Add(d);
                d += a;
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
                float dz = (b.Z - a.Z) / (b.X - a.X);
                float z = a.Z;
                for (int x = (int)a.X; x <= (int)b.X; x++)
                {
                    DrawPixel(x, (int)ys[x - (int)a.X], z, color);
                    z += dz;
                }
            } 
            else
            {
                if (a.Y > b.Y)
                    (b, a) = (a, b);
                List<float> xs = Interpolate(a.Y, a.X, b.Y, b.X);
                float dz = (b.Z - a.Z) / (b.Y - a.Y);
                float z = a.Z;
                for (int y = (int)a.Y; y <= (int)b.Y; y++)
                {
                    DrawPixel((int)xs[y - (int)a.Y], y, z, color);
                    z += dz;
                }
            }
        }

        private void FillFace(Vector4 a, Vector4 b, Vector4 c, Vector3 color) // список всех точек ребер грани
        {
            if (b.Y < a.Y)
                (a, b) = (b, a);
            if (c.Y < a.Y)
                (a, c) = (c, a);
            if (c.Y < b.Y)
                (b, c) = (c, b);

            List<float> x01 = Interpolate(a.Y, a.X, b.Y, b.X);
            List<float> x12 = Interpolate(b.Y, b.X, c.Y, c.X);
            List<float> x02 = Interpolate(a.Y, a.X, c.Y, c.X);

            x01.RemoveAt(x01.Count - 1);
            List<float> x012 = new();
            x012.AddRange(x01);
            x012.AddRange(x12);

            List<float> z01 = Interpolate(a.Y, a.Z, b.Y, b.Z);
            List<float> z12 = Interpolate(b.Y, b.Z, c.Y, c.Z);
            List<float> z02 = Interpolate(a.Y, a.Z, c.Y, c.Z);

            z01.RemoveAt(z01.Count - 1);
            List<float> z012 = new();
            z012.AddRange(z01);
            z012.AddRange(z12);

            int m = x012.Count / 2;

            List<float> x_left;
            List<float> x_right;

            List<float> z_left;
            List<float> z_right;

            if (x02[m] < x012[m])
            {
                x_left = x02;
                x_right = x012;

                z_left = z02;
                z_right = z012;
            }
            else
            {
                x_left = x012;
                x_right = x02;

                z_left = z012;
                z_right = z02;
            }            

            for (int y = (int)a.Y; y < (int)c.Y; y++)
            {
                float z = z_left[y - (int)a.Y];
                float dz = (z_right[y - (int)a.Y] - z_left[y - (int)a.Y]) / (x_right[y - (int)a.Y] - x_left[y - (int)a.Y]);
                for (int x = (int)x_left[y - (int)a.Y]; x < (int)x_right[y - (int)a.Y]; x++)
                {
                    DrawPixel(x, y, z, color);
                    z += dz;
                }
            }
        }

        private void DrawFace(List<Vector3> face, List<Vector4> vertices)
        {
            Vector3 color = GetFaceColor(face, new(0.5f, 0.5f, 0.5f));
            //Vector3 color = Vector3.Zero;

                for (int i = 0; i < face.Count - 1; i++)
                {
                    DrawLine(
                        vertices[i],
                        vertices[i + 1],
                        color
                    );
                }


                DrawLine(
                    vertices[0],                   
                    vertices[2],
                    color
                );
            FillFace(vertices[0], vertices[1], vertices[2], color);
        }

        private void DrawPixel(int x, int y, float z, Vector3 color)
        {
            if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight && z > 0 && z < 1 && z <= ZBuffer[x, y])
            {
                bitmap.SetPixel(x, y, color);
                ZBuffer[x, y] = z;
            }

        }

        private void Draw()
        {

            DateTime t = DateTime.Now;
            bitmap.Clear();

            
            Vector4[] vertices = TransformCoordinates();
            
            ZBuffer.Clear();
            
            bitmap.Source.Lock();

            //List<Task> tasks = new List<Task>();
            
            Parallel.ForEach(model.Faces, face =>
            {
                List<Vector4> faceVertices = new List<Vector4>()
                            {
                                vertices[(int)face[0].X - 1],
                                vertices[(int)face[1].X - 1],
                                vertices[(int)face[2].X - 1]
                            };
                if (GetNormal(faceVertices).Z < 0)
                    DrawFace(face, faceVertices);
            });
       
            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            
            bitmap.Source.Unlock();
            Time.Content = (1000 / (DateTime.Now - t).Milliseconds).ToString() + " fps";

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bitmap = new((int)Grid.ActualWidth, (int)Grid.ActualHeight);
            Canvas.Source = bitmap.Source;
            /*model.Translation = new(0, -10, 0);
            ParseModelFromFile("./model/tree.obj");*/
            ParseModelFromFile("./model/shovel_low.obj");
            model.Translation = new(0, -6, -3);
            /*model.Translation = new(-54, -54, 0);
            ParseModelFromFile("./model/cube.obj");*/
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
