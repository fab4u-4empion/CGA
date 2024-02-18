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
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using lab1.Shaders;
using System.Windows.Media.Imaging;
using lab1.Shadow;
using lab1.Effects;
using Microsoft.Win32;
using System.Diagnostics;

namespace lab1
{
    using Layer = (int Index, float Z);
    using Color = (Vector3 Color, float Alpha, float Dissolve);
    using DPIScale = (double X, double Y);

    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;

        Buffer<Vector3> bufferHDR;
        Buffer<SpinLock> spins;
        Buffer<int> viewBuffer;
        Buffer<float> ZBuffer;

        Buffer<int> OffsetBuffer;
        Buffer<byte> CountBuffer;
        Layer[] LayersBuffer;

        Model mainModel;
        Model Sphere;
        Camera camera = new();

        Vector3 backColor = new(0.1f, 0.1f, 0.1f);

        float smoothing = 1f;
        float BlurIntensity = 0.15f;
        int BlurRadius = 0;
        DPIScale scale = (1, 1);

        Stopwatch timer = new();

        Point mouse_position;

        WindowState LastState;

        bool camera_control = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadMaterials(string fileName, Model model)
        {
            string fold = Path.GetDirectoryName(fileName);
            using (StreamReader mtlReader = new(fileName))
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
                        model.MaterialNames.Add(mtlLine.Remove(0, 6).Trim(), mtlIndex);
                    }

                    if (mtlLine.StartsWith("Pm"))
                    {
                        material.Pm = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    }

                    if (mtlLine.StartsWith("map_Tr"))
                    {
                        material.Transmission = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 6).Trim()));
                        material.BlendMode = BlendModes.AlphaBlending;
                    }

                    if (mtlLine.StartsWith("Tr"))
                    {
                        material.Tr = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                        material.BlendMode = BlendModes.AlphaBlending;
                    }

                    if (mtlLine.StartsWith('d'))
                    {
                        material.D = float.Parse(mtlLine.Remove(0, 1).Trim(), CultureInfo.InvariantCulture);
                        material.BlendMode = BlendModes.AlphaBlending;
                    }

                    if (mtlLine.StartsWith("map_d"))
                    {
                        material.Dissolve = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 5).Trim()));
                        material.BlendMode = BlendModes.AlphaBlending;
                    }

                    if (mtlLine.StartsWith("Kd"))
                    {
                        float[] Kd = mtlLine
                            .Remove(0, 2)
                            .Trim()
                            .Split(' ')
                            .Select(c => float.Parse(c, CultureInfo.InvariantCulture))
                            .ToArray();
                        material.Kd = ToneMapping.SrgbToLinear(new(Kd[0], Kd[1], Kd[2]));
                    }

                    if (mtlLine.StartsWith("Pr"))
                    {
                        material.Pr = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    }

                    if (mtlLine.StartsWith("map_Kd"))
                    {                        
                        material.Diffuse = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 6).Trim()), true);
                    }

                    if (mtlLine.StartsWith("map_Ke"))
                    {
                        material.Emission = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 6).Trim()), true);
                    }

                    if (mtlLine.StartsWith("map_MRAO"))
                    {
                        material.MRAO = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 8).Trim()));
                    }

                    if (mtlLine.StartsWith("map_Pcr"))
                    {
                        material.ClearCoatRoughness = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 7).Trim()));
                        continue;
                    }

                    if (mtlLine.StartsWith("Pcr"))
                    {
                        material.Pcr = float.Parse(mtlLine.Remove(0, 3).Trim(), CultureInfo.InvariantCulture);
                        continue;
                    }

                    if (mtlLine.StartsWith("map_Pc"))
                    {
                        material.ClearCoat = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 6).Trim()));
                    }

                    if (mtlLine.StartsWith("Pc"))
                    {
                        material.Pc = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    }

                    if (mtlLine.StartsWith("norm_pc"))
                    {
                        material.ClearCoatNormals = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 7).Trim()), false, true);
                        continue;
                    }

                    if (mtlLine.StartsWith("norm"))
                    {
                        material.Normals = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 4).Trim()), false, true);
                    }
                }
                model.Materials.Add(material);
            }
        }

        private void LoadModel(string fileName, Model model)
        {
            int materialIndex = 0;
            int faceIndex = 0;
            using (StreamReader reader = new(fileName))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine().Trim();

                    if (line.StartsWith("mtllib "))
                    {
                        String mtl = line.Remove(0, 7).Trim();
                        LoadMaterials(Path.Combine(Path.GetDirectoryName(fileName), mtl), model);
                    }

                    if (line.StartsWith("usemtl"))
                    {
                        model.MaterialNames.TryGetValue(line.Remove(0, 6).Trim(), out materialIndex);
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
                        List<(int, int, int)> vertices = line
                                .Remove(0, 2)
                                .Trim()
                                .Split(" ")
                                .Select(v =>
                                {
                                    List<int> indexes = v
                                        .Split("/")
                                        .Select(i => int.Parse(i, CultureInfo.InvariantCulture))
                                        .ToList();
                                    return (indexes[0], indexes[1], indexes[2]);
                                }
                                )
                                .ToList();
                        for (int i = 0; i < vertices.Count - 2;  i++)
                        {
                            model.AddFace(vertices[0], vertices[i + 1], vertices[i + 2], materialIndex, faceIndex);
                            faceIndex++;
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

        private void TransformCoordinates(Model model)
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(model.Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(model.Yaw, model.Pitch, model.Roll);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(model.Translation);
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(camera.FoV, (float)bitmap.PixelWidth / (float)bitmap.PixelHeight, 0.1f, 500f);
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(-0.5f, -0.5f, bitmap.PixelWidth, bitmap.PixelHeight, 0, 1);

            Matrix4x4 matrix = modelMatrix * viewMatrix * projectionMatrix * viewportMatrix;

            model.ViewVertices = new Vector4[model.Positions.Count];
            for (int i = 0; i < model.ViewVertices.Length; i++)
            {
                Vector4 vertex = Vector4.Transform(model.Positions[i], matrix);
                vertex /= new Vector4(new(vertex.W), vertex.W * vertex.W);
                model.ViewVertices[i] = vertex;
            }
        }

        private float PerpDotProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private void Rasterize(List<int> facesIndices, Model model, Action<int, int, float, int> action, BlendModes blendMode)
        {
            Parallel.ForEach(Partitioner.Create(0, facesIndices.Count), (range) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    int index = facesIndices[i] * 3;
                    Vector4 a = model.ViewVertices[model.PositionIndices[index]];
                    Vector4 b = model.ViewVertices[model.PositionIndices[index + 1]];
                    Vector4 c = model.ViewVertices[model.PositionIndices[index + 2]];
                    if ((PerpDotProduct(new(b.X - a.X, b.Y - a.Y), new(c.X - b.X, c.Y - b.Y)) <= 0 || blendMode == BlendModes.AlphaBlending) && 1 / a.W > 0 && 1 / b.W > 0 && 1 / c.W > 0)
                    {
                        if (b.X < a.X)
                            (a, b) = (b, a);

                        if (c.X < a.X)
                            (a, c) = (c, a);

                        if (c.X < b.X)
                            (b, c) = (c, b);

                        Vector4 k1 = (c - a) / (c.X - a.X);
                        Vector4 k2 = (b - a) / (b.X - a.X);
                        Vector4 k3 = (c - b) / (c.X - b.X);

                        int left = int.Max((int)float.Ceiling(a.X), 0);
                        int right = int.Min((int)float.Ceiling(c.X), bitmap.PixelWidth);

                        for (int x = left; x < right; x++)
                        {
                            Vector4 p1 = a + (x - a.X) * k1;
                            Vector4 p2 = x < b.X ? a + (x - a.X) * k2 : b + (x - b.X) * k3;

                            if (p1.Y > p2.Y)
                                (p1, p2) = (p2, p1);

                            Vector4 k = (p2 - p1) / (p2.Y - p1.Y);

                            int top = int.Max((int)float.Ceiling(p1.Y), 0);
                            int bottom = int.Min((int)float.Ceiling(p2.Y), bitmap.PixelHeight);

                            for (int y = top; y < bottom; y++)
                            {
                                Vector4 p = p1 + (y - p1.Y) * k;
                                if (p.Z >= 0 && p.Z <= 1)
                                    action(x, y, p.Z, facesIndices[i]);
                            }
                        }
                    }
                }
            });
        }

        private Color GetPixelColor(int faceIndex, Vector2 p, Model model)
        {
            int materialIndex = model.MaterialIndices[faceIndex];
            int index = faceIndex * 3;

            Vector4 a = model.ViewVertices[model.PositionIndices[index]];
            Vector4 b = model.ViewVertices[model.PositionIndices[index + 1]];
            Vector4 c = model.ViewVertices[model.PositionIndices[index + 2]];

            Vector2 pa = new Vector2(a.X, a.Y) - p;
            Vector2 pb = new Vector2(b.X, b.Y) - p;
            Vector2 pc = new Vector2(c.X, c.Y) - p;

            float u = PerpDotProduct(pc, pb) * a.W;
            float v = PerpDotProduct(pa, pc) * b.W;
            float w = PerpDotProduct(pb, pa) * c.W;
            float sum = u + v + w;

            float dudx = (pc.Y - pb.Y) * a.W * 0.5f;
            float dvdx = (pa.Y - pc.Y) * b.W * 0.5f;
            float dwdx = (pb.Y - pa.Y) * c.W * 0.5f;

            float dudy = (pb.X - pc.X) * a.W * 0.5f;
            float dvdy = (pc.X - pa.X) * b.W * 0.5f;
            float dwdy = (pa.X - pb.X) * c.W * 0.5f;

            (float u1, float v1, float w1) = (u - dudx, v - dvdx, w - dwdx);
            (float u2, float v2, float w2) = (u + dudy, v + dvdy, w + dwdy);
            (float u3, float v3, float w3) = (u + dudx, v + dvdx, w + dwdx);
            (float u4, float v4, float w4) = (u - dudy, v - dvdy, w - dwdy);

            Vector3 n1 = model.Normals[model.NormalIndices[index]];
            Vector3 n2 = model.Normals[model.NormalIndices[index + 1]];
            Vector3 n3 = model.Normals[model.NormalIndices[index + 2]];

            Vector2 uv_1 = model.UV[model.UVIndices[index]];
            Vector2 uv_2 = model.UV[model.UVIndices[index + 1]];
            Vector2 uv_3 = model.UV[model.UVIndices[index + 2]];

            Vector3 aw = model.Positions[model.PositionIndices[index]];
            Vector3 bw = model.Positions[model.PositionIndices[index + 1]];
            Vector3 cw = model.Positions[model.PositionIndices[index + 2]];

            Vector3 t1 = model.Tangents[model.TangentIndices[index]];
            Vector3 t2 = model.Tangents[model.TangentIndices[index + 1]];
            Vector3 t3 = model.Tangents[model.TangentIndices[index + 2]];

            Vector2 uv = (u * uv_1 + v * uv_2 + w * uv_3) / sum;
            Vector2 uv1 = (u1 * uv_1 + v1 * uv_2 + w1 * uv_3) / (u1 + v1 + w1);
            Vector2 uv2 = (u2 * uv_1 + v2 * uv_2 + w2 * uv_3) / (u2 + v2 + w2);
            Vector2 uv3 = (u3 * uv_1 + v3 * uv_2 + w3 * uv_3) / (u3 + v3 + w3);
            Vector2 uv4 = (u4 * uv_1 + v4 * uv_2 + w4 * uv_3) / (u4 + v4 + w4);

            Vector3 oN = (u * n1 + v * n2 + w * n3) / sum;
            Vector3 pw = (u * aw + v * bw + w * cw) / sum;

            Vector3 T = (u * t1 + v * t2 + w * t3) / sum;
            Vector3 B = Vector3.Cross(oN, T) * model.Signs[faceIndex];

            Vector3 albedo = model.Materials[materialIndex].GetDiffuse(uv, uv1, uv2, uv3, uv4);

            Vector3 n = model.Materials[materialIndex].GetNormal(uv, Vector3.UnitZ, uv1, uv2, uv3, uv4);
            n = T * n.X + B * n.Y + oN * n.Z;

            Vector3 MRAO = model.Materials[materialIndex].GetMRAO(uv, uv1, uv2, uv3, uv4);
            Vector3 emission = model.Materials[materialIndex].GetEmission(uv, uv1, uv2, uv3, uv4);
            float opacity = 1 - model.Materials[materialIndex].GetTransmission(uv, uv1, uv2, uv3, uv4);
            float dissolve = model.Materials[materialIndex].GetDissolve(uv, uv1, uv2, uv3, uv4);
            (float clearCoatRougness, float clearCoat, Vector3 nc) = model.Materials[materialIndex].GetClearCoat(uv, Vector3.UnitZ, uv1, uv2, uv3, uv4);
            nc = T * nc.X + B * nc.Y + oN * nc.Z;

            Vector3 color = PBR.GetPixelColor(albedo, MRAO.X, MRAO.Y, MRAO.Z, opacity, emission, n, nc, clearCoat, clearCoatRougness, camera.Position, pw, faceIndex, dissolve);

            return (color, opacity, dissolve);
        }

        private void DrawPixelIntoViewBuffer(int x, int y, float z, int index)
        {
            bool gotLock = false;
            spins[x, y].Enter(ref gotLock);

            if (z < ZBuffer[x, y])
            {
                viewBuffer[x, y] = index;
                ZBuffer[x, y] = z;
            }

            spins[x, y].Exit(false);
        }

        private void IncDepth(int x, int y, float z, int index)
        {
            if (z < ZBuffer[x, y])
                Interlocked.Increment(ref OffsetBuffer[x, y]);
        }

        private void DrawPixelIntoLayers(int x, int y, float z, int index)
        {
            if (z < ZBuffer[x, y])
            {
                bool gotLock = false;
                spins[x, y].Enter(ref gotLock);

                LayersBuffer[OffsetBuffer[x, y] + CountBuffer[x, y]] = (index, z);
                CountBuffer[x, y] += 1;

                spins[x, y].Exit(false);
            }
        }

        public Vector3 GetResultColor(int start, int length, int x, int y)
        {
            float key;
            Layer layer;
            int j;
            for (int i = 1 + start; i < length + start; i++)
            {
                key = LayersBuffer[i].Z;
                layer = LayersBuffer[i];
                j = i - 1;

                while (j >= start && LayersBuffer[j].Z > key)
                {
                    LayersBuffer[j + 1] = LayersBuffer[j];
                    j--;
                }
                LayersBuffer[j + 1] = layer;
            }

            Vector3 color = Vector3.Zero;
            float alpha = 0;

            for (int i = start; i < length + start; i++)
            {
                Color pixel = GetPixelColor(LayersBuffer[i].Index, new(x, y), mainModel);

                color += (1 - alpha) * pixel.Color;
                alpha += (1 - alpha) * pixel.Alpha * pixel.Dissolve;

                if (pixel.Alpha == 1 && pixel.Dissolve == 1)
                    break;
            }

            color += (1 - alpha) * bufferHDR[x, y];

            return color;
        }

        public void DrawViewBuffer()
        {
            Parallel.For(0, bitmap.PixelWidth, (x) =>
            {
                for (int y = 0; y < bitmap.PixelHeight; y++)
                {
                    if (viewBuffer[x, y] != -1)
                    {
                        Color color = GetPixelColor(viewBuffer[x, y], new(x, y), mainModel);
                        bufferHDR[x, y] = color.Color;
                    }
                }
            });
        }

        public void DrawLayers()
        {
            Parallel.For(0, bitmap.PixelWidth, (x) =>
            {
                for (int y = 0; y < bitmap.PixelHeight; y++)
                {
                    if (CountBuffer[x, y] > 0)
                        bufferHDR[x, y] = GetResultColor(OffsetBuffer[x, y], CountBuffer[x, y], x, y);
                }
            });
        }

        public void DrawHDRBuffer()
        {
            if (BlurRadius > 0)
            {
                Bloom.Smoothing = smoothing;
                Buffer<Vector3> bloomBuffer = Bloom.GetBoolmBuffer(bufferHDR, bitmap.PixelWidth, bitmap.PixelHeight);
                Parallel.For(0, bitmap.PixelWidth, (x) =>
                {
                    for (int y = 0; y < bitmap.PixelHeight; y++)
                    {
                        bitmap.SetPixel(x, y, ToneMapping.CompressColor(bufferHDR[x, y] + bloomBuffer[x, y] * BlurIntensity));
                        bufferHDR[x, y] = backColor;
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
                        bufferHDR[x, y] = backColor;
                    }
                });
            }
        }

        private void UpdateInfo()
        {
            Reso.Content = $"{bitmap.PixelWidth}×{bitmap.PixelHeight}";

            Ray_Count.Content = $"Ray count: {RTX.RayCount}";
            Light_size.Content = $"Light size: {RTX.LightSize}";

            ToneMode.Content = $"Tone mapping: {ToneMapping.Mode}";
            if (ToneMapping.Mode == ToneMappingMode.AgX)
                ToneMode.Content += $" {ToneMapping.LookMode}";

            MIPMapping.Content = $"MIP mapping: {Material.UsingMIPMapping}";
            if (Material.UsingMIPMapping)
                MIPMapping.Content += $" ×{Material.MaxAnisotropy}";

            CurrLamp.Content = $"Current lamp: {(LightingConfig.CurrentLamp > -1 ? LightingConfig.CurrentLamp + 1 : "*")}";

            Contrl.Content = $"NumPad control mode: {(camera_control ? "camera" : "light")}";
        }

        private void DrawLights()
        {
            if (LightingConfig.DrawLights)
            {
                for (int i = 0; i < LightingConfig.Lights.Count; i++)
                {
                    Lamp lamp = LightingConfig.Lights[i];

                    Sphere.Translation = lamp.Position;
                    Sphere.Scale = float.Max(0.05f, RTX.LightSize);

                    TransformCoordinates(Sphere);

                    Rasterize(Sphere.OpaqueFacesIndices, Sphere, (int x, int y, float z, int unused) =>
                    {
                        bool gotLock = false;
                        spins[x, y].Enter(ref gotLock);

                        if (z < ZBuffer[x, y])
                        {
                            bufferHDR[x, y] = lamp.Color * (lamp.Intensity + 1f);
                            ZBuffer[x, y] = z;
                        }

                        spins[x, y].Exit(false);
                    }, BlendModes.Opaque);
                }
            }
        }

        private void DrawScene()
        {
            TransformCoordinates(mainModel);

            Rasterize(mainModel.OpaqueFacesIndices, mainModel, DrawPixelIntoViewBuffer, BlendModes.Opaque);

            DrawViewBuffer();

            DrawLights();

            if (mainModel.TransparentFacesIndices.Count > 0)
            {
                Rasterize(mainModel.TransparentFacesIndices, mainModel, IncDepth, BlendModes.AlphaBlending);

                int prefixSum = 0;
                int depth = 0;

                for (int x = 0; x < bitmap.PixelWidth; x++)
                {
                    for (int y = 0; y < bitmap.PixelHeight; y++)
                    {
                        depth = OffsetBuffer[x, y];
                        OffsetBuffer[x, y] = prefixSum;
                        prefixSum += depth;
                    }
                }

                LayersBuffer = new Layer[prefixSum];

                Rasterize(mainModel.TransparentFacesIndices, mainModel, DrawPixelIntoLayers, BlendModes.AlphaBlending);

                DrawLayers();
            }
        }

        private void Draw()
        {
            UpdateInfo();

            timer.Restart();

            for (int x = 0; x < bitmap.PixelWidth; x++)
            {
                for (int y = 0; y < bitmap.PixelHeight; y++)
                {
                    ZBuffer[x, y] = float.MaxValue;
                    viewBuffer[x, y] = -1;
                    CountBuffer[x, y] = 0;
                    OffsetBuffer[x, y] = 0;
                }
            }

            if (mainModel !=  null)
                DrawScene();
            else
                DrawLights();

            bitmap.Source.Lock();
            bitmap.Clear();

            DrawHDRBuffer();
       
            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.Source.Unlock();

            timer.Stop();

            Time.Content = (double.Round(timer.ElapsedMilliseconds) + " ms");
        }

        private void CreateBuffers()
        {
            bitmap = new((int)(Grid.ActualWidth * smoothing * scale.X), (int)(Grid.ActualHeight * smoothing * scale.Y));
            bufferHDR = new(bitmap.PixelWidth, bitmap.PixelHeight);
            Canvas.Source = bitmap.Source;
            spins = new(bitmap.PixelWidth, bitmap.PixelHeight);
            viewBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            CountBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            OffsetBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
            for (int i = 0; i < bitmap.PixelWidth; i++)
            {
                for (int j = 0; j < bitmap.PixelHeight; j++)
                {
                    spins[i, j] = new(false);
                    bufferHDR[i, j] = backColor;
                }
            }
            ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CreateBuffers();
            Sphere = new Model();
            LoadModel(Path.Combine(Directory.GetCurrentDirectory(), "model.obj"), Sphere);

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
                camera.UpdatePosition(-0.3f, 0, 0);
            } else
            {
                camera.UpdatePosition(0.3f, 0, 0);
            }
            Draw();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            scale = (dpi.DpiScaleX, dpi.DpiScaleY);

            CreateBuffers();
            
            if (IsLoaded)
                Draw();
        }

        private void ResizeHandler()
        {
            CreateBuffers();
            Draw();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.NumPad1:
                    if (camera_control)
                        camera.Move(new(-0.2f, 0, 0));
                    else
                        LightingConfig.ChangeLampPosition(new(-0.2f, 0, 0));
                    Draw();
                    break;

                case Key.NumPad2:
                    if (camera_control)
                        camera.Move(new(0.2f, 0, 0));
                    else
                        LightingConfig.ChangeLampPosition(new(0.2f, 0, 0));
                    Draw();
                    break;

                case Key.NumPad4:
                    if (camera_control)
                        camera.Move(new(0, -0.2f, 0));
                    else
                        LightingConfig.ChangeLampPosition(new(0, -0.2f, 0));
                    Draw();
                    break;

                case Key.NumPad5:
                    if (camera_control)
                        camera.Move(new(0, 0.2f, 0));
                    else
                        LightingConfig.ChangeLampPosition(new(0, 0.2f, 0));
                    Draw();
                    break;

                case Key.NumPad7:
                    if (camera_control)
                        camera.Move(new(0, 0, -0.2f));
                    else
                        LightingConfig.ChangeLampPosition(new(0, 0, -0.2f));
                    Draw();
                    break;

                case Key.NumPad8:
                    if (camera_control)
                        camera.Move(new(0, 0, 0.2f));
                    else
                        LightingConfig.ChangeLampPosition(new(0, 0, 0.2f));
                    Draw();
                    break;

                case Key.Right:
                    LightingConfig.ChangeLampIntensity(10);
                    Draw();
                    break;

                case Key.Left:
                    LightingConfig.ChangeLampIntensity(-10);
                    Draw();
                    break;

                case Key.Up:
                    LightingConfig.ChangeLamp(1);
                    Draw(); 
                    break;

                case Key.Down:
                    LightingConfig.ChangeLamp(-1);
                    Draw();
                    break;

                case Key.Add:
                    LightingConfig.AmbientIntensity += 0.01f;
                    Draw();
                    break;

                case Key.Subtract:
                    LightingConfig.AmbientIntensity -= 0.01f;
                    LightingConfig.AmbientIntensity = float.Max(LightingConfig.AmbientIntensity, 0);
                    Draw();
                    break;

                case Key.Divide:
                    LightingConfig.EmissionIntensity -= 0.2f;
                    LightingConfig.EmissionIntensity = float.Max(LightingConfig.EmissionIntensity, 0);
                    Draw();
                    break;

                case Key.Multiply:
                    LightingConfig.EmissionIntensity += 0.2f;
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
                    BlurRadius += 1;
                    if (Bloom.Kernels.Length == 1)
                        Bloom.Kernels = [new() { R = BlurRadius, Intensity = 1 }];
                    else
                        Bloom.Kernels = [
                            new() { R = BlurRadius, Intensity = 1 },
                            new() { R = BlurRadius * 3, Intensity = 1.25f },
                            new() { R = BlurRadius * 3, Intensity = 1.5625f },
                        ];
                    Draw();
                    break;

                case Key.A:
                    BlurRadius -= 1;
                    BlurRadius = int.Max(BlurRadius, 0);
                    if (Bloom.Kernels.Length == 1)
                        Bloom.Kernels = [new() { R = BlurRadius, Intensity = 1 }];
                    else
                        Bloom.Kernels = [
                            new() { R = BlurRadius, Intensity = 1 },
                            new() { R = BlurRadius * 3, Intensity = 1.25f },
                            new() { R = BlurRadius * 3, Intensity = 1.5625f },
                        ];
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
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap.Source as BitmapSource));
                    DirectoryInfo info = Directory.CreateDirectory("img");
                    using (FileStream st = new($@"{info.Name}/{DateTime.Now:dd-MM-yyyy HH-mm-ss-fff}.png", FileMode.Create))
                    {
                        encoder.Save(st);
                    }
                    break;

                case Key.U:
                    OpenFileDialog ofd = new();
                    if (ofd.ShowDialog() == true)
                    {
                        Bloom.KernelImg = ofd.FileName;
                    }
                    Draw();
                    break;

                case Key.P:
                    Bloom.KernelImg = null;
                    Draw();
                    break;

                case Key.I:
                    if (Bloom.Kernels.Length > 1)
                        Bloom.Kernels = [new() { R = BlurRadius, Intensity = 1 }];
                    else
                        Bloom.Kernels = [
                            new() { R = BlurRadius, Intensity = 1 },
                            new() { R = BlurRadius * 3, Intensity = 1.25f },
                            new() { R = BlurRadius * 3, Intensity = 1.5625f },
                        ];
                    Draw();
                    break;

                case Key.M:
                    Material.UsingMIPMapping = !Material.UsingMIPMapping;
                    Draw();
                    break;

                case Key.N:
                    Material.MaxAnisotropy *= 2;
                    if (Material.MaxAnisotropy > 16) 
                        Material.MaxAnisotropy = 1;
                    Draw();
                    break;

                case Key.B:
                    LightingConfig.DrawLights = !LightingConfig.DrawLights;
                    Draw();
                    break;

                case Key.L:
                    camera_control = !camera_control;
                    Draw();
                    break;

                case Key.NumPad9:
                    camera.Target = mainModel.GetCenter();
                    camera.UpdatePosition(0, 0, 0);
                    Draw();
                    break;

                case Key.O:
                    OpenFileDialog dlg = new();
                    dlg.Filter = "Wavefront (*.obj)|*.obj";
                    if (dlg.ShowDialog() == true)
                    {
                        mainModel = new();

                        timer.Restart();
                        LoadModel(dlg.FileName, mainModel);
                        mainModel.CalculateTangents();
                        timer.Stop();
                        Model_time.Content = $"Model loaded in {double.Round(timer.ElapsedMilliseconds)} ms";

                        timer.Restart();
                        BVH.Build(mainModel.Positions, mainModel.OpaqueFacesIndices, mainModel.PositionIndices);
                        BVH_time.Content = $"BVH builded in {double.Round(timer.ElapsedMilliseconds)} ms";
                        timer.Stop();

                        camera.Target = mainModel.GetCenter();
                        camera.UpdatePosition(0, 0, 0);

                        Draw();
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

                    case Key.F2:
                        LightingConfigWindow window = new();
                        window.ShowDialog();
                        Draw();
                        break;
                }
            }

        }
    }
}
