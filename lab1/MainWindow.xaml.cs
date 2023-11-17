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
using lab1.Shadow;

namespace lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;
        Vector3[,] bufferHDR;
        Vector3[,] bufferEmission;

        Model model = new();
        Camera camera = new();
        ZBuffer ZBuffer;
        Vector3 light = new(0, 0, 0);
        Vector3 baseColor = new(0.5f, 0.5f, 0.5f);
        float smoothing = 1f;
        float BlurIntensity = 0.15f;
        float BlurRadius = 7;

        SpinLock[,] spins;

        Point mouse_position;
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadMaterials(string fold, string mtl)
        {
            using (StreamReader mtlReader = new($"{fold}/{mtl}"))
            {
                Material? material = null;
                int mtlIndex = 0;

                while (!mtlReader.EndOfStream)
                {
                    String mtlLine = mtlReader.ReadLine().Trim();

                    if (mtlLine.StartsWith("newmtl"))
                    {
                        if (material != null)
                        {
                            model.Materials.Add(material);
                            mtlIndex++;
                        }
                        material = new();
                        model.MaterialsIndexes.Add(mtlLine.Remove(0, 6).Trim(), mtlIndex);
                    }

                    if (mtlLine.StartsWith("Pm"))
                    {
                        material.Pm = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    }

                    if (mtlLine.StartsWith("Kd"))
                    {
                        float[] Kd = mtlLine
                            .Remove(0, 2)
                            .Trim()
                            .Split(' ')
                            .Select(c => float.Parse(c, CultureInfo.InvariantCulture))
                            .ToArray();
                        material.Kd = new(Kd[0], Kd[1], Kd[2]);
                    }

                    if (mtlLine.StartsWith("Pr"))
                    {
                        material.Pr = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    }

                    if (mtlLine.StartsWith("map_Kd"))
                    {
                        material.Diffuse = new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 6).Trim()}", UriKind.Relative)));
                    }

                    if (mtlLine.StartsWith("map_Ke"))
                    {
                        material.Emission = new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 6).Trim()}", UriKind.Relative)));
                    }

                    if (mtlLine.StartsWith("map_MRAO"))
                    {
                        material.MRAO = new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 8).Trim()}", UriKind.Relative)));
                    }

                    if (mtlLine.StartsWith("norm"))
                    {
                        material.Normals = new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 4).Trim()}", UriKind.Relative)));
                    }
                }
                model.Materials.Add(material);
            }
        }

        private void LoadModel(string fold)
        {
            int materialIndex = 0;
            using (StreamReader reader = new(fold + "/model.obj"))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine().Trim();

                    if (line.StartsWith("mtllib "))
                    {
                        String mtl = line.Remove(0, 7).Trim();
                        LoadMaterials(fold, mtl);
                    }

                    if (line.StartsWith("usemtl"))
                    {
                        model.MaterialsIndexes.TryGetValue(line.Remove(0, 6).Trim(), out materialIndex);
                    }

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
                        model.AddVertex(coordinates[0] * 2, coordinates[1] * 2, coordinates[2] * 2);
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
                            model.FacesMaterials.Add(materialIndex);
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
        }

        private Vector4[] TransformCoordinates()
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(model.Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(model.Yaw, model.Pitch, model.Roll);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(model.Translation);
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(camera.Position - model.GetTranslationParams(), camera.Target - model.GetTranslationParams(), camera.Up);
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(camera.FoV, (float)bitmap.PixelWidth / (float)bitmap.PixelHeight, 0.1f, 1000);
            Matrix4x4 modelViewProjectionMatrix = modelMatrix * viewMatrix * projectionMatrix;
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(0, 0, bitmap.PixelWidth, bitmap.PixelHeight, 0, 1);

            Vector4[] windowVertices = new Vector4[model.Vertices.Count];
            for (int i = 0; i < windowVertices.Length; i++)
            {
                windowVertices[i] = Vector4.Transform(model.Vertices[i], modelViewProjectionMatrix);
                float w = 1 / windowVertices[i].W;
                windowVertices[i] *= w;
                windowVertices[i] = Vector4.Transform(windowVertices[i], viewportMatrix);
                windowVertices[i].W = w;
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

        private void FillFace(
            Vector4 a, Vector4 b, Vector4 c, 
            Vector3 n1, Vector3 n2, Vector3 n3, 
            Vector2 uv1, Vector2 uv2, Vector2 uv3,
            Vector4 aw, Vector4 bw, Vector4 cw,
            Vector3 color,
            int materialIndex, int faceIndex)
        {
            uv1 *= a.W;
            uv2 *= b.W;
            uv3 *= c.W;

            n1 *= a.W;
            n2 *= b.W;
            n3 *= c.W;

            aw *= a.W;
            bw *= b.W;
            cw *= c.W;

            if (b.Y < a.Y)
            {
                (a, b) = (b, a);
                (n1, n2) = (n2, n1);
                (uv1, uv2) = (uv2, uv1);
                (aw, bw) = (bw, aw);
            }
                
            if (c.Y < a.Y)
            {
                (a, c) = (c, a);
                (n1, n3) = (n3, n1);
                (uv1, uv3) = (uv3, uv1);
                (aw, cw) = (cw, aw);
            }

            if (c.Y < b.Y)
            {
                (b, c) = (c, b);
                (n2, n3) = (n3, n2);
                (uv2, uv3) = (uv3, uv2);
                (bw, cw) = (cw, bw);
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

            Vector4 kw1 = (cw - aw) / (c.Y - a.Y);
            Vector4 kw2 = (bw - aw) / (b.Y - a.Y);
            Vector4 kw3 = (cw - bw) / (c.Y - b.Y);

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

                Vector4 pw1 = aw + (y - a.Y) * kw1;
                Vector4 pw2 = y < b.Y ? aw + (y - a.Y) * kw2 : bw + (y - b.Y) * kw3;

                if (p1.X > p2.X)
                {
                    (p1, p2) = (p2, p1);
                    (pn1, pn2) = (pn2, pn1);
                    (puv1, puv2) = (puv2, puv1);
                    (pw1, pw2) = (pw2, pw1);
                }

                Vector4 k = (p2 - p1) / (p2.X - p1.X);
                Vector3 kn = (pn2 - pn1) / (p2.X - p1.X);
                Vector2 kuv = (puv2 - puv1) / (p2.X - p1.X);
                Vector4 kw = (pw2 - pw1) / (p2.X - p1.X);

                int left = int.Max((int)float.Ceiling(p1.X), 0);
                int right = int.Min((int)float.Ceiling(p2.X), bitmap.PixelWidth);

                for (int x = left; x < right; x++) { 
                    Vector4 p = p1 + (x - p1.X) * k;
                    //Vector3 n = (pn1 + (x - p1.X) * kn) / p.W;
                    Vector2 uv = (puv1 + (x - p1.X) * kuv) / p.W;
                    Vector4 pw = (pw1 + (x - p1.X) * kw) / p.W;

                    //lab4
                    //Vector3 diffuse = model.GetDiffuse(uv.X, uv.Y);
                    //Vector3 n = 2 * model.GetNormal(uv.X, uv.Y) - Vector3.One;
                    //float spec = model.GetSpecular(uv.X, uv.Y).X;

                    //color = Phong.GetPixelColor(baseColor, n, 0.5f, light, camera.Position, new(pw.X, pw.Y, pw.Z));
                    //color = Phong.GetPixelColor(diffuse, n, 0.5f, light, camera.Position, new(pw.X, pw.Y, pw.Z));

                    //pbr
                    Vector3 albedo = model.Materials[materialIndex].GetDiffuse(uv.X, uv.Y);
                    Vector3 n = model.Materials[materialIndex].GetNormal(uv.X, uv.Y);
                    Vector3 MRAO = model.Materials[materialIndex].GetMRAO(uv.X, uv.Y);
                    Vector3 emission = model.Materials[materialIndex].GetEmission(uv.X, uv.Y);

                    Vector3 hdrColor;
                    Vector3 emissionColor;
                    (hdrColor, emissionColor) = PBR.GetPixelColor(albedo, MRAO.X, MRAO.Y, MRAO.Z, emission, n, camera.Position, new(pw.X, pw.Y, pw.Z), faceIndex);

                    DrawPixel(x, y, p.Z, hdrColor, emissionColor);
                    //DrawPixel(x, y, p.Z, color);
                }
            }
        }

        private void DrawFace(List<Vector3> face, Vector4[] vertices, int materialIndex, int faceIndex)
        {
            Vector3 n1 = model.Normals[(int)face[0].Z - 1];
            Vector3 n2 = model.Normals[(int)face[1].Z - 1]; 
            Vector3 n3 = model.Normals[(int)face[2].Z - 1];

            Vector2 uv1 = model.UV[(int)face[0].Y - 1];
            Vector2 uv2 = model.UV[(int)face[1].Y - 1];
            Vector2 uv3 = model.UV[(int)face[2].Y - 1];

            Vector4 aw = model.Vertices[(int)face[0].X - 1];
            Vector4 bw = model.Vertices[(int)face[1].X - 1];
            Vector4 cw = model.Vertices[(int)face[2].X - 1];

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
            //    light,
            //    new Vector3(aw.X, aw.Y, aw.Z),
            //    new Vector3(bw.X, bw.Y, bw.Z),
            //    new Vector3(cw.X, cw.Y, cw.Z)
            //);
            //FillFace(
            //    vertices[0], vertices[1], vertices[2],
            //    n1, n2, n3,
            //    uv1, uv2, uv3,
            //    aw, bw, cw,
            //    color);

            //lab3-4
            FillFace(
                vertices[0], vertices[1], vertices[2],
                n1, n2, n3,
                uv1, uv2, uv3,
                aw, bw, cw,
                Vector3.Zero,
                materialIndex,
                faceIndex);
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

        private void DrawPixel(int x, int y, float z, Vector3 hdr, Vector3 emission)
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
                            bufferHDR[x, y] = hdr;
                            bufferEmission[x, y] = emission;
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

        public void DrawBitmap(int r)
        {
            if (r > 0)
            {
                Vector3[,] resultTMP = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];

                float sigma = (r * 2f) / 6f;

                float[] g = new float[r * 2 + 1];

                for (int i = -r; i <= r; i++)
                {
                    g[i + r] = (float)Math.Exp(-1 * i * i / (2 * sigma * sigma)) / (float)Math.Sqrt(2 * float.Pi * sigma * sigma);
                }

                Parallel.For(0, bitmap.PixelWidth, (x) =>
                {
                    Parallel.For(0, bitmap.PixelHeight, (y) =>
                    {
                        Vector3 sum = Vector3.Zero;
                        for (int i = x - r; i <= x + r; i++)
                        {
                            sum += bufferEmission[
                                Math.Min(Math.Max(i, 0), bitmap.PixelWidth - 1),
                                y
                            ] * g[i + r - x];
                        }
                        resultTMP[x, y] = sum;
                    });
                });

                Parallel.For(0, bitmap.PixelWidth, (x) =>
                {
                    Parallel.For(0, bitmap.PixelHeight, (y) =>
                    {
                        Vector3 sum = Vector3.Zero;
                        for (int i = y - r; i <= y + r; i++)
                        {
                            sum += resultTMP[
                                x,
                                Math.Min(Math.Max(i, 0), bitmap.PixelHeight - 1)
                            ] * g[i + r - y];
                        }
                        bitmap.SetPixel(x, y, PBR.LinearToSrgb(PBR.AsecFilmic(bufferHDR[x, y] + sum * BlurIntensity)));
                        bufferHDR[x, y] = Vector3.Zero;
                        bufferEmission[x, y] = Vector3.Zero;
                    });
                });
            }
            else
            {
                Parallel.For(0, bitmap.PixelWidth, (x) =>
                {
                    Parallel.For(0, bitmap.PixelHeight, (y) =>
                    {
                        bitmap.SetPixel(x, y, PBR.LinearToSrgb(PBR.AsecFilmic(bufferHDR[x, y])));
                        bufferHDR[x, y] = Vector3.Zero;
                    });
                });
            }
        }

        private void Draw()
        {
            
            DateTime t = DateTime.Now;
            bitmap.Clear();
            Vector4[] vertices = TransformCoordinates();
            
            ZBuffer.Clear();
            
            bitmap.Source.Lock();

            Parallel.For(0, model.Faces.Count, (X) =>
            {
                List<Vector3> face = model.Faces[X];
                Vector4[] faceVerts = new Vector4[3];
                faceVerts[0] = vertices[(int)face[0].X - 1];
                faceVerts[1] = vertices[(int)face[1].X - 1];
                faceVerts[2] = vertices[(int)face[2].X - 1];

                if (GetNormal(faceVerts) <= 0)
                    DrawFace(face, faceVerts, model.FacesMaterials[X], X);
            });

            DrawBitmap((int)(BlurRadius * smoothing));
       
            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            
            bitmap.Source.Unlock();
            Time.Content = (double.Round((DateTime.Now - t).TotalMilliseconds)).ToString() + " ms";
            Reso.Content = $"{bitmap.PixelWidth}×{bitmap.PixelHeight}";
            Ray_Count.Content = $"Ray count: {RTX.RayCount}";
            Light_size.Content = $"Light size: {RTX.LightSize}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bitmap = new((int)(Grid.ActualWidth * smoothing), (int)(Grid.ActualHeight * smoothing));
            bufferHDR = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            bufferEmission = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];

            DateTime t = DateTime.Now;
            //LoadModel("./model/Shovel Knight");
            LoadModel("./model/Cyber Mancubus");
            //LoadModel("./model/Doom Slayer");
            //LoadModel("./model/Intergalactic Spaceship");
            //LoadModel("./model/Material Ball");
            //LoadModel("./model/Mimic Chest");
            //LoadModel("./model/Pink Soldier");
            //LoadModel("./model/Robot Steampunk");
            //LoadModel("./model/Tree Man");
            //LoadModel("./model/Box");
            Model_time.Content = "Model loaded in " + (double.Round((DateTime.Now - t).TotalMilliseconds)).ToString() + " ms";

            t = DateTime.Now;
            BVH.Build(model.Faces, model.Vertices);
            BVH_time.Content = "BVH builded in " + (double.Round((DateTime.Now - t).TotalMilliseconds)).ToString() + " ms";

            ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            camera.MinZoomR = model.GetMinZoomR();
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

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bitmap = new((int)(Grid.ActualWidth * smoothing), (int)(Grid.ActualHeight * smoothing));
            bufferHDR = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            bufferEmission = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];
            ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            if (IsLoaded)
                Draw();
        }

        private WindowState LastState;

        private void ResizeHandler()
        {
            bitmap = new((int)(Grid.ActualWidth * smoothing), (int)(Grid.ActualHeight * smoothing));
            bufferHDR = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            bufferEmission = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];
            ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            Draw();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.NumPad1:
                    light.X -= 0.5f;
                    PBR.X -= 0.1f;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.NumPad2:
                    light.X += 0.5f;
                    PBR.X += 0.1f;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.NumPad4:
                    light.Y -= 0.5f;
                    PBR.Y -= 0.1f;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.NumPad5:
                    light.Y += 0.5f;
                    PBR.Y += 0.1f;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.NumPad7:
                    light.Z -= 0.5f;
                    PBR.Z -= 0.1f;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.NumPad8:
                    light.Z += 0.5f;
                    PBR.Z += 0.1f;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.Right:
                    PBR.LightIntensity += 50;
                    Draw();
                    break;

                case Key.Left:
                    PBR.LightIntensity -= 50;
                    PBR.LightIntensity = float.Max(PBR.LightIntensity, 0);
                    Draw();
                    break;

                case Key.Up:
                    PBR.LP += 1;
                    PBR.ChangeLightsPos();
                    Draw(); 
                    break;

                case Key.Down:
                    PBR.LP -= 1;
                    PBR.ChangeLightsPos();
                    Draw();
                    break;

                case Key.Add:
                    PBR.AmbientIntensity += 0.01f;
                    Draw();
                    break;

                case Key.Subtract:
                    PBR.AmbientIntensity -= 0.01f;
                    PBR.AmbientIntensity = float.Max(PBR.AmbientIntensity, 0);
                    Draw();
                    break;

                case Key.Divide:
                    PBR.EmissionIntensity -= 0.2f;
                    PBR.EmissionIntensity = float.Max(PBR.EmissionIntensity, 0);
                    Draw();
                    break;

                case Key.Multiply:
                    PBR.EmissionIntensity += 0.2f;
                    Draw();
                    break;

                case Key.W:
                    BlurIntensity += 0.05f;
                    Draw();
                    break;

                case Key.S:
                    BlurIntensity -= 0.05f;
                    BlurIntensity = float.Max(BlurIntensity, 0);
                    Draw();
                    break;

                case Key.D:
                    BlurRadius += 1f;
                    Draw();
                    break;

                case Key.A:
                    BlurRadius -= 1f;
                    BlurRadius = float.Max(BlurRadius, 0);
                    Draw();
                    break;

                case Key.R:
                    PBR.UseShadow = !PBR.UseShadow;
                    Draw();
                    break;

                case Key.Z:
                    RTX.LightSize -= 0.001f;
                    RTX.LightSize = float.Max(RTX.LightSize, 0);
                    Draw();
                    break;

                case Key.X:
                    RTX.LightSize += 0.001f;
                    Draw();
                    break;

                case Key.C:
                    RTX.RayCount -= 1;
                    RTX.RayCount = int.Max(RTX.RayCount, 0);
                    Draw();
                    break;

                case Key.V:
                    RTX.RayCount += 1;
                    Draw();
                    break;

                case Key.Q:
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap.Source as BitmapSource));
                    DirectoryInfo info = Directory.CreateDirectory("img");
                    using (FileStream st = new FileStream($@"{info.Name}/{DateTime.Now:dd-MM-yyyy HH-mm-ss-fff}.png", FileMode.Create))
                    {
                        encoder.Save(st);
                    }
                    break;
            }

            if (!e.IsRepeat)
            {
                switch (e.Key)
                {
                    case Key.D1:
                        smoothing = 0.25f;
                        ResizeHandler();
                        break;

                    case Key.D2:
                        smoothing = 0.5f;
                        ResizeHandler();
                        break;

                    case Key.D3:
                        smoothing = 1;
                        ResizeHandler();
                        break;

                    case Key.D4:
                        smoothing = 2;
                        ResizeHandler();
                        break;

                    case Key.D5:
                        smoothing = 4;
                        ResizeHandler();
                        break;

                    case Key.F11:
                        if (WindowStyle != WindowStyle.None)
                        {
                            LastState = WindowState;
                            WindowStyle = WindowStyle.None;
                            ResizeMode = ResizeMode.NoResize;
                            WindowState = WindowState.Normal;
                            WindowState = WindowState.Maximized;
                        }
                        else
                        {
                            WindowStyle = WindowStyle.SingleBorderWindow;
                            ResizeMode = ResizeMode.CanResize;
                            WindowState = LastState;
                        }
                        break;
                }
            }
        }
    }
}
