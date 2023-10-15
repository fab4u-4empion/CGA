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
using System.Threading;
using lab1.Shaders;
using System.Windows.Media.Imaging;

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
        Vector3 light = Vector3.Normalize(new(1, 2, 3));
        Vector3 baseColor = new(0.5f, 0.5f, 0.5f);
        int smoothing = 1;

        SpinLock[,] spins;

        Point mouse_position;
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadModel(string fold)
        {
            model.DiffuseMap = new Pbgra32Bitmap(new BitmapImage(new Uri($"{fold}/diffuse.png", UriKind.Relative)));
            model.NormalMap = new Pbgra32Bitmap(new BitmapImage(new Uri($"{fold}/nm.png", UriKind.Relative)));
            model.SpecularMap = new Pbgra32Bitmap(new BitmapImage(new Uri($"{fold}/spec.png", UriKind.Relative)));
            using (StreamReader reader = new(fold + "/model.obj"))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine().Trim();

                    if (line.StartsWith("v "))
                    {
                        List<float> coordinates = line
                            .Remove(0, 2)
                            .Trim()
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
                                .Trim()
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
                            .Trim()
                            .Split(" ")
                            .Select(c =>
                                float.Parse(c, CultureInfo.InvariantCulture)
                             )
                            .ToList();
                        model.AddNormal(coordinates[0], coordinates[1], coordinates[2]);
                    }

                    if (line.StartsWith("vt "))
                    {
                        List<float> coordinates = line
                            .Remove(0, 3)
                            .Trim()
                            .Split(" ")
                            .Select(c =>  
                                float.Parse(c, CultureInfo.InvariantCulture)
                            )
                            .ToList();
                        model.AddUV(coordinates[0], 1 - coordinates[1]);
                    }
                }
            }
            model.TransformModelParams();
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

        private float GetNormal(Vector4[] vertices)
        {
            Vector4 v1 = vertices[0];
            Vector4 v2 = vertices[1];
            Vector4 v3 = vertices[2];

            Vector2 s1 = new(v2.X - v1.X, v2.Y - v1.Y);
            Vector2 s2 = new(v3.X - v2.X, v3.Y - v2.Y);
            return s1.X * s2.Y - s1.Y * s2.X;
        }

        private void DrawLine(Vector4 a, Vector4 b, Vector3 color)
        {
            
            if (Math.Abs(b.X - a.X) > Math.Abs(b.Y - a.Y))
            {
                if (a.X > b.X)
                    (b, a) = (a, b);

                Vector4 k = (b - a) / (b.X - a.X);

                int left = int.Max((int)float.Ceiling(a.X), 0);
                int right = int.Min((int)float.Ceiling(b.X), bitmap.PixelWidth);

                for (int x = left; x < right; x++)
                {
                    Vector4 p = a + (x - a.X) * k;
                    DrawPixel(x, (int)float.Ceiling(p.Y), p.Z, color);
                }
            }
            else
            {
                if (a.Y > b.Y)
                    (b, a) = (a, b);

                Vector4 k = (b - a) / (b.Y - a.Y);

                int top = int.Max((int)float.Ceiling(a.Y), 0);
                int bottom = int.Min((int)float.Ceiling(b.Y), bitmap.PixelWidth);

                for (int y = top; y < bottom; y++)
                {
                    Vector4 p = a + (y - a.Y) * k;
                    DrawPixel((int)float.Ceiling(p.X), y, p.Z, color);
                }
            }
        }

        private void FillFace(Vector4 a, Vector4 b, Vector4 c, Vector3 n1, Vector3 n2, Vector3 n3, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector3 color)
        {
            uv1 /= a.Z;
            uv2 /= b.Z;
            uv3 /= c.Z;

            if (b.Y < a.Y)
            {
                (a, b) = (b, a);
                (n1, n2) = (n2, n1);
                (uv1, uv2) = (uv2, uv1);
            }
                
            if (c.Y < a.Y)
            {
                (a, c) = (c, a);
                (n1, n3) = (n3, n1);
                (uv1, uv3) = (uv3, uv1);
            }

            if (c.Y < b.Y)
            {
                (b, c) = (c, b);
                (n2, n3) = (n3, n2);
                (uv2, uv3) = (uv3, uv2);
            }

            Vector4 k1 = (c - a) / (c.Y - a.Y);
            Vector4 k2 = (b - a) / (b.Y - a.Y);
            Vector4 k3 = (c - b) / (c.Y - b.Y);

            Vector3 kn1 = (n3 - n1) / (c.Y - a.Y);
            Vector3 kn2 = (n2 - n1) / (b.Y - a.Y);
            Vector3 kn3 = (n3 - n2) / (c.Y - b.Y);

            Vector2 kuv1 = (uv3 - uv1) / (c.Y - a.Y);
            Vector2 kuv2 = (uv2 - uv1) / (b.Y - a.Y);
            Vector2 kuv3 = (uv3 - uv2) / (c.Y - b.Y);

            int top = int.Max((int)float.Ceiling(a.Y), 0);
            int bottom = int.Min((int)float.Ceiling(c.Y), bitmap.PixelHeight);

            for (int y = top; y < bottom; y++)
            {
                Vector4 p1 = a + (y - a.Y) * k1;
                Vector4 p2 = y < b.Y ? a + (y - a.Y) * k2 : b + (y - b.Y) * k3;

                Vector3 pn1 = n1 + (y - a.Y) * kn1;
                Vector3 pn2 = y < b.Y ? n1 + (y - a.Y) * kn2 : n2 + (y - b.Y) * kn3;

                Vector2 puv1 = uv1 + (y - a.Y) * kuv1;
                Vector2 puv2 = y < b.Y ? uv1 + (y - a.Y) * kuv2 : uv2 + (y - b.Y) * kuv3;

                if (p1.X > p2.X)
                {
                    (p1, p2) = (p2, p1);
                    (pn1, pn2) = (pn2, pn1);
                    (puv1, puv2) = (puv2, puv1);
                }

                Vector4 k = (p2 - p1) / (p2.X - p1.X);
                Vector3 kn = (pn2 - pn1) / (p2.X - p1.X);
                Vector2 kuv = (puv2 - puv1) / (p2.X - p1.X);

                int left = int.Max((int)float.Ceiling(p1.X), 0);
                int right = int.Min((int)float.Ceiling(p2.X), bitmap.PixelWidth);

                for (int x = left; x < right; x++) { 
                    Vector4 p = p1 + (x - p1.X) * k;
                    Vector3 n = pn1 + (x - p1.X) * kn;
                    Vector2 uv = (puv1 + (x - p1.X) * kuv) / (1 / p.Z);

                    Vector3 diffuse = model.GetDiffuse(uv.X, uv.Y);
                    //Vector3 n = 2 * model.GetNormal(uv.X, uv.Y) - Vector3.One;
                    Vector3 spec = model.GetSpecular(uv.X, uv.Y);

                    color = Phong.GetPixelColor(diffuse, n, spec, light, camera.LookVector);

                    DrawPixel(x, y, p.Z, color);
                }
            }
        }

        private void DrawFace(List<Vector3> face, Vector4[] vertices)
        {
            Vector3 n1 = model.Normals[(int)face[0].Z - 1];
            Vector3 n2 = model.Normals[(int)face[1].Z - 1]; 
            Vector3 n3 = model.Normals[(int)face[2].Z - 1];

            Vector2 uv1 = model.UV[(int)face[0].Y - 1];
            Vector2 uv2 = model.UV[(int)face[1].Y - 1];
            Vector2 uv3 = model.UV[(int)face[2].Y - 1];

            //lab 1
            //DrawLine(vertices[0], vertices[1], new(0, 0, 0));
            //DrawLine(vertices[0], vertices[2], new(0, 0, 0));
            //DrawLine(vertices[1], vertices[2], new(0, 0, 0));

            //lab2
            //Vector3 color = Lambert.GetFaceColor(
            //    n1,
            //    n2,
            //    n3,
            //    baseColor,
            //    light
            //);
            //FillFace(vertices[0], vertices[1], vertices[2], n1, n2, n3, uv1, uv2, uv3, color);

            //lab3-4
            FillFace(vertices[0], vertices[1], vertices[2], n1, n2, n3, uv1, uv2, uv3, Vector3.Zero);
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

            //ParseModelFromFile("./model/tree.obj");
            //ParseModelFromFile("./model/shovel_low.obj");
            //ParseModelFromFile("./model/doom.obj");
            //ParseModelFromFile("./model/cube.obj");
            LoadModel("./model/man");
            //LoadModel("./model/diablo");
            //LoadModel("./model/shovel");
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
