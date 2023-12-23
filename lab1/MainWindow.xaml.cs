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
using lab1.Effects;
using Microsoft.Win32;
using System.Reflection;

namespace lab1
{
    
    public struct Layer
    {
        public Vector3 Color { get; set; }
        public float Opacity { get; set; }
        public float Z { get; set; }
    }

    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;
        List<Layer>[,] layers;
        Vector3[,] bufferHDR;
        int[,] viewBuffer;

        Model model = new();
        Camera camera = new();
        ZBuffer ZBuffer;
        Vector3 light = new(0, 0, 0);
        Vector3 baseColor = new(0.5f, 0.5f, 0.5f);
        float smoothing = 0.25f;
        float BlurIntensity = 0.15f;
        float BlurRadius = 0;

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

                    if (mtlLine.StartsWith("map_Trasmission"))
                    {
                        material.AddTrasmission(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 15).Trim()}", UriKind.Relative))));
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
                        material.AddDiffuse(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 6).Trim()}", UriKind.Relative))));
                    }

                    if (mtlLine.StartsWith("map_Ke"))
                    {
                        material.AddEmission(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 6).Trim()}", UriKind.Relative))));
                    }

                    if (mtlLine.StartsWith("map_MRAO"))
                    {
                        material.AddMRAO(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 8).Trim()}", UriKind.Relative))));
                    }

                    if (mtlLine.StartsWith("map_ClearCoat"))
                    {
                        material.AddClearCoat(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 13).Trim()}", UriKind.Relative))));
                    }

                    if (mtlLine.StartsWith("map_CCRoughness"))
                    {
                        material.AddClearCoatRoughness(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 15).Trim()}", UriKind.Relative))));
                    }

                    if (mtlLine.StartsWith("CCNorm"))
                    {
                        material.AddClearCoatNormals(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 6).Trim()}", UriKind.Relative))));
                    }

                    if (mtlLine.StartsWith("norm"))
                    {
                        material.AddNormals(new(new BitmapImage(new Uri($"{fold}/{mtlLine.Remove(0, 4).Trim()}", UriKind.Relative))));
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
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(-0.5f, -0.5f, bitmap.PixelWidth, bitmap.PixelHeight, 0, 1);

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

        private float PerpDotProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
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

        private void FillViewBuffer(Vector4[] vertices, int X)
        {
            Vector4 a = vertices[0];
            Vector4 b = vertices[1];
            Vector4 c = vertices[2];

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

                for (int x = left; x < right; x++)
                {
                    Vector4 p = p1 + (x - p1.X) * k;
                    DrawPixelIntoViewBuffer(x, y, p.Z, X);
                }
            }
        }

        private void DrawPixelIntoHDRBuffer(int faceIndex, Vector4[] viewVertices, Vector2 p)
        {
            List<Vector3> face = model.Faces[faceIndex];
            int materialIndex = model.FacesMaterials[faceIndex];

            Vector4 a = viewVertices[(int)face[0].X - 1];
            Vector4 b = viewVertices[(int)face[1].X - 1];
            Vector4 c = viewVertices[(int)face[2].X - 1];

            Vector2 ap = new(a.X, a.Y);
            Vector2 bp = new(b.X, b.Y);
            Vector2 cp = new(c.X, c.Y);

            float u = PerpDotProduct(bp - p, cp - p) * a.W;
            float v = PerpDotProduct(cp - p, ap - p) * b.W;
            float w = PerpDotProduct(ap - p, bp - p) * c.W;
            float sum = u + v + w;

            Vector2 px = p + Vector2.UnitX;
            float ux = PerpDotProduct(bp - px, cp - px) * a.W;
            float vx = PerpDotProduct(cp - px, ap - px) * b.W;
            float wx = PerpDotProduct(ap - px, bp - px) * c.W;
            float sumx = ux + vx + wx;

            Vector2 py = p + Vector2.UnitY;
            float uy = PerpDotProduct(bp - py, cp - py) * a.W;
            float vy = PerpDotProduct(cp - py, ap - py) * b.W;
            float wy = PerpDotProduct(ap - py, bp - py) * c.W;
            float sumy = uy + vy + wy;

            Vector3 n1 = model.Normals[(int)face[0].Z - 1];
            Vector3 n2 = model.Normals[(int)face[1].Z - 1];
            Vector3 n3 = model.Normals[(int)face[2].Z - 1];

            Vector2 uv1 = model.UV[(int)face[0].Y - 1];
            Vector2 uv2 = model.UV[(int)face[1].Y - 1];
            Vector2 uv3 = model.UV[(int)face[2].Y - 1];

            Vector4 aw = model.Vertices[(int)face[0].X - 1];
            Vector4 bw = model.Vertices[(int)face[1].X - 1];
            Vector4 cw = model.Vertices[(int)face[2].X - 1];

            Vector2 uv = (u * uv1 + v * uv2 + w * uv3) / sum;
            Vector2 uvx = (ux * uv1 + vx * uv2 + wx * uv3) / sumx;
            Vector2 uvy = (uy * uv1 + vy * uv2 + wy * uv3) / sumy;

            Vector3 oN = (u * n1 + v * n2 + w * n3) / sum;
            Vector4 pw = (u * aw + v * bw + w * cw) / sum;

            Vector3 albedo = model.Materials[materialIndex].GetDiffuse(uv, uvx, uvy);
            Vector3 n = model.Materials[materialIndex].GetNormal(uv, oN, uvx, uvy);
            Vector3 MRAO = model.Materials[materialIndex].GetMRAO(uv, uvx, uvy);
            Vector3 emission = model.Materials[materialIndex].GetEmission(uv, uvx, uvy);
            float opascity = 1 - model.Materials[materialIndex].GetTrasmission(uv, uvx, uvy);
            (float clearCoatRougness, float clearCoat, Vector3 clearCoatNormal) = model.Materials[materialIndex].GetClearCoat(uv, oN, uvx, uvy);

            Vector3 hdrColor = PBR.GetPixelColor(albedo, MRAO.X, MRAO.Y, MRAO.Z, opascity, emission, n, clearCoatNormal, clearCoat, clearCoatRougness, camera.Position, new(pw.X, pw.Y, pw.Z), faceIndex);

            bufferHDR[(int)p.X, (int)p.Y] = hdrColor;
        }

        private void DrawPixelIntoViewBuffer(int x, int y, float z, int X)
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
                            viewBuffer[x, y] = X;
                            ZBuffer[x, y] = z;
                        }
                    }
                    finally
                    {
                        if (gotLock)
                        {
                            spins[x, y].Exit(false);
                            flag = false;
                        }
                    }
                }
            }
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
                            spins[x, y].Exit(false);
                            flag = false;
                        }
                    }
                }
            }
        }

        //private void DrawPixel(int x, int y, float z, Vector3 hdr, float opacity)
        //{
        //    if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight && z > 0 && z < 1)
        //    {
        //        bool gotLock = false;
        //        bool flag = true;
        //        while (flag)
        //        {
        //            try
        //            {
        //                spins[x, y].Enter(ref gotLock);
        //                if (gotLock)
        //                {
        //                    layers[x, y].Add(new() { Color = hdr, Opacity = opacity, Z = z });
        //                }
        //            }
        //            finally
        //            {
        //                if (gotLock)
        //                {
        //                    spins[x, y].Exit(false);
        //                    flag = false;
        //                }
        //            }
        //        }
        //    }
        //}

        public Vector3 GetResultColor(int x, int y)
        {
            List<Layer> layer = layers[x, y];
            layer.Sort((a, b) => b.Z.CompareTo(a.Z));
            Vector3 color = Vector3.Zero;
            for (int i = 0; i < layer.Count; i++)
            {
                color = layer[i].Color + color * (1 - layer[i].Opacity);
            }
            return color;
        }

        public void DrawViewBuffer(Vector4[] viewVertices)
        {
            Parallel.For(0, bitmap.PixelWidth, (x) =>
            {
                for (int y = 0; y < bitmap.PixelHeight; y++)
                {
                    if (viewBuffer[x, y] != -1)
                        DrawPixelIntoHDRBuffer(viewBuffer[x, y], viewVertices, new(x, y));
                }
            });
        }

        public void DrawBitmap()
        {
            //Parallel.For(0, bitmap.PixelWidth, (x) =>
            //{
            //    for (int y = 0; y < bitmap.PixelHeight; y++)
            //    {
            //        bufferHDR[x, y] = GetResultColor(x, y);
            //    }
            //});

            if (BlurRadius > 0)
            {
                Vector3[,] bloomBuffer = Bloom.GetBoolmBuffer((int)(BlurRadius * smoothing), bufferHDR, bitmap.PixelWidth, bitmap.PixelHeight);
                Parallel.For(0, bitmap.PixelWidth, (x) =>
                {
                    for (int y = 0; y < bitmap.PixelHeight; y++)
                    {
                        bitmap.SetPixel(x, y, ToneMapping.CompressColor(bufferHDR[x, y] + bloomBuffer[x, y] * BlurIntensity));
                        bufferHDR[x, y] = Vector3.Zero;
                    }
                });
            }
            else
            {
                Parallel.For(0, bitmap.PixelWidth, (x) =>
                {
                    for (int y = 0; y < bitmap.PixelHeight; y++)
                    {
                        bitmap.SetPixel(x, y, ToneMapping.CompressColor(bufferHDR[x, y]));
                        bufferHDR[x, y] = Vector3.Zero;
                    }
                });
            }
        }

        private void Draw()
        {
            
            DateTime t = DateTime.Now;
            bitmap.Clear();

            Vector4[] viewVertices = TransformCoordinates();

            Parallel.For(0, bitmap.PixelWidth, (x) =>
            {
                for (int y = 0; y < bitmap.PixelHeight; y++)
                {
                    //layers[x, y].Clear();
                    ZBuffer[x, y] = float.MaxValue;
                    viewBuffer[x, y] = -1;
                }
            });              

            bitmap.Source.Lock();

            var range = Partitioner.Create(0, model.Faces.Count);

            //Parallel.ForEach(range, (range) =>
            //{
            //    for (int i = range.Item1; i < range.Item2; i++)
            //    {
            //        Vector4[] faceVerts =
            //            [
            //                viewVertices[(int)model.Faces[i][0].X - 1],
            //                viewVertices[(int)model.Faces[i][1].X - 1],
            //                viewVertices[(int)model.Faces[i][2].X - 1],
            //            ];
            //        if (GetNormal(faceVerts) <= 0)
            //        {
            //            FillViewBuffer(faceVerts, i);
            //        }
            //    }
            //});

            Parallel.For(0, model.Faces.Count, (X) =>
            {
                List<Vector3> face = model.Faces[X];
                Vector4[] faceVerts =
                [
                    viewVertices[(int)face[0].X - 1],
                    viewVertices[(int)face[1].X - 1],
                    viewVertices[(int)face[2].X - 1],
                ];
                if (GetNormal(faceVerts) <= 0)
                {
                    FillViewBuffer(faceVerts, X);
                }
            });

            DrawViewBuffer(viewVertices);
            DrawBitmap();
       
            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            
            bitmap.Source.Unlock();
            Time.Content = (double.Round((DateTime.Now - t).TotalMilliseconds)).ToString() + " ms";
            Reso.Content = $"{bitmap.PixelWidth}×{bitmap.PixelHeight}";
            Ray_Count.Content = $"Ray count: {RTX.RayCount}";
            Light_size.Content = $"Light size: {RTX.LightSize}";
            ToneMode.Content = $"Tone mapping: {ToneMapping.Mode}";
            if (ToneMapping.Mode == ToneMappingMode.AgX)
                ToneMode.Content += $" {ToneMapping.LookMode}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bitmap = new((int)(Grid.ActualWidth * smoothing), (int)(Grid.ActualHeight * smoothing));
            bufferHDR = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];
            //layers = new List<Layer>[bitmap.PixelWidth, bitmap.PixelHeight];
            viewBuffer = new int[bitmap.PixelWidth, bitmap.PixelHeight];
            for (int i = 0; i < bitmap.PixelWidth; i++)
            {
                for (int j = 0; j < bitmap.PixelHeight; j++)
                {
                    spins[i, j] = new(false);
                    //layers[i, j] = new(16);
                }
            }

            DateTime t = DateTime.Now;
            //LoadModel("./model/Shovel Knight");
            //LoadModel("./model/Cyber Mancubus");
            //LoadModel("./model/Doom Slayer");
            //LoadModel("./model/Intergalactic Spaceship");
            //LoadModel("./model/Material Ball");
            //LoadModel("./model/Mimic Chest");
            //LoadModel("./model/Pink Soldier");
            //LoadModel("./model/Robot Steampunk");
            //LoadModel("./model/Tree Man");
            //LoadModel("./model/Box");
            //LoadModel("./model/Bottled car");
            //LoadModel("./model/Car");
            //LoadModel("./model/Egor");
            //LoadModel("./model/other/chess");
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
            viewBuffer = new int[bitmap.PixelWidth, bitmap.PixelHeight];
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];
            //layers = new List<Layer>[bitmap.PixelWidth, bitmap.PixelHeight];
            for (int i = 0; i < bitmap.PixelWidth; i++)
            {
                for (int j = 0; j < bitmap.PixelHeight; j++)
                {
                    spins[i, j] = new(false);
                    //layers[i, j] = new(16);
                }
            }
            ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            if (IsLoaded)
                Draw();
        }

        private WindowState LastState;

        private void ResizeHandler()
        {
            bitmap = new((int)(Grid.ActualWidth * smoothing), (int)(Grid.ActualHeight * smoothing));
            bufferHDR = new Vector3[bitmap.PixelWidth, bitmap.PixelHeight];
            viewBuffer = new int[bitmap.PixelWidth, bitmap.PixelHeight];
            Canvas.Source = bitmap.Source;
            spins = new SpinLock[bitmap.PixelWidth, bitmap.PixelHeight];
            //layers = new List<Layer>[bitmap.PixelWidth, bitmap.PixelHeight]; 
            for (int i = 0; i < bitmap.PixelWidth; i++)
            {
                for (int j = 0; j < bitmap.PixelHeight; j++)
                {
                    spins[i, j] = new(false);
                    //layers[i, j] = new(16);
                }
            }
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
                    PBR.LightIntensity += 10;
                    Draw();
                    break;

                case Key.Left:
                    PBR.LightIntensity -= 10;
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

                case Key.T:
                    if (ToneMapping.Mode == ToneMappingMode.ACES) 
                        ToneMapping.Mode = ToneMappingMode.AgX;
                    else 
                    if (ToneMapping.Mode == ToneMappingMode.AgX) 
                        ToneMapping.Mode = ToneMappingMode.ACES;
                    Draw();
                    break;

                case Key.Y:
                    if (ToneMapping.Mode == ToneMappingMode.AgX) {
                        if (ToneMapping.LookMode == AgXLookMode.DEFAULT)
                            ToneMapping.LookMode = AgXLookMode.PUNCHY;
                        else
                        if (ToneMapping.LookMode == AgXLookMode.PUNCHY)
                            ToneMapping.LookMode = AgXLookMode.GOLDEN;
                        else
                        if (ToneMapping.LookMode == AgXLookMode.GOLDEN)
                            ToneMapping.LookMode = AgXLookMode.DEFAULT;
                    }
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

                case Key.O:
                    OpenFileDialog dlg = new OpenFileDialog();
                    if (dlg.ShowDialog() == true)
                    {
                        Bloom.KernelImg = dlg.FileName;
                    }
                    Draw();
                    break;

                case Key.P:
                    Bloom.KernelImg = null;
                    Draw();
                    break;

                case Key.I:
                    Bloom.KernelCount = Bloom.KernelCount == 1 ? 3 : 1;
                    Draw();
                    break;

                case Key.M:
                    PBR.ClearCoatEnable = !PBR.ClearCoatEnable;
                    Draw();
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
