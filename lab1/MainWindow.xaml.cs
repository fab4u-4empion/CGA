using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using lab1.Shadow;
using Microsoft.Win32;
using System.Diagnostics;
using System.Numerics;

namespace lab1
{
    using DPIScale = (double X, double Y);    

    public partial class MainWindow : Window
    {
        Model MainModel;

        Renderer Renderer;

        DPIScale Scale = (1, 1);

        Stopwatch Timer = new();

        Point MousePosition;

        WindowState LastState;

        bool CameraControl = false;

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

                    if (mtlLine.StartsWith("Ks"))
                    {
                        float[] Kd = mtlLine
                            .Remove(0, 2)
                            .Trim()
                            .Split(' ')
                            .Select(c => float.Parse(c, CultureInfo.InvariantCulture))
                            .ToArray();
                        material.Ks = ToneMapping.SrgbToLinear(new(Kd[0], Kd[1], Kd[2]));
                    }

                    if (mtlLine.StartsWith("Pr"))
                    {
                        material.Pr = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    }

                    if (mtlLine.StartsWith("map_Kd"))
                    {                        
                        material.Diffuse = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 6).Trim()), true);
                    }

                    if (mtlLine.StartsWith("map_Ks"))
                    {
                        material.Specular = model.AddTexture(Path.Combine(fold, mtlLine.Remove(0, 6).Trim()), true);
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

            foreach (string l in File.ReadLines(fileName))
            {
                if (l == "") continue;

                string[] line = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                switch (line[0])
                {
                    case "mtllib":
                        LoadMaterials(Path.Combine(Path.GetDirectoryName(fileName), line[1]), model);
                        break;

                    case "usemtl":
                        model.MaterialNames.TryGetValue(line[1], out materialIndex);
                        break;

                    case "v":
                        model.AddVertex(
                            float.Parse(line[1], CultureInfo.InvariantCulture),
                            float.Parse(line[2], CultureInfo.InvariantCulture),
                            float.Parse(line[3], CultureInfo.InvariantCulture)
                        );
                        break;

                    case "f":
                        for (int i = 1; i < line.Length - 2; i++)
                        {
                            model.AddFace(line[1], line[i + 1], line[i + 2], materialIndex, faceIndex);
                            faceIndex++;
                        }
                        break;

                    case "vn":
                        model.AddNormal(
                            float.Parse(line[1], CultureInfo.InvariantCulture),
                            float.Parse(line[2], CultureInfo.InvariantCulture),
                            float.Parse(line[3], CultureInfo.InvariantCulture)
                        );
                        break;

                    case "vt":
                        model.AddUV(float.Parse(line[1], CultureInfo.InvariantCulture), 1 - float.Parse(line[2], CultureInfo.InvariantCulture));
                        break;
                }
            }

            model.ProjectionVertices = new Vector4[model.Positions.Count];
        }

        private void UpdateInfo()
        {
            Reso.Content = $"{Renderer.Bitmap.PixelWidth}×{Renderer.Bitmap.PixelHeight}";

            Ray_Count.Content = $"Ray count: {RTX.RayCount}";
            Light_size.Content = $"Light size: {RTX.LightSize}";

            ToneMode.Content = $"Tone mapping: {ToneMapping.Mode}";
            if (ToneMapping.Mode == ToneMappingMode.AgX)
                ToneMode.Content += $" {ToneMapping.LookMode}";

            MIPMapping.Content = $"MIP mapping: {Material.UsingMIPMapping}";
            if (Material.UsingMIPMapping)
                MIPMapping.Content += $" ×{Material.MaxAnisotropy}";

            CurrLamp.Content = $"Current lamp: {(LightingConfig.CurrentLamp > -1 ? LightingConfig.Lights[LightingConfig.CurrentLamp].Name : "*")}";

            Contrl.Content = $"Camera control mode: {Renderer.Camera.Mode}";
            Shader.Content = $"Shader: {Renderer.CurrentShader}";
        }

        private void Draw()
        {
            UpdateInfo();

            Timer.Restart();

            Renderer.Draw(MainModel);

            Timer.Stop();

            Time.Content = (double.Round(Timer.ElapsedMilliseconds) + " ms");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Renderer = new();
            Renderer.CreateBuffers(Grid.ActualWidth, Grid.ActualHeight, Scale);

            Canvas.Source = Renderer.Bitmap.Source;

            Renderer.Sphere = new Model();
            LoadModel(Path.Combine(Directory.GetCurrentDirectory(), "model.obj"), Renderer.Sphere);

            LightingConfig.BRDFLLUT.Open("BRDFIntegrationMap.pfm");

            Draw();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point current_position = e.GetPosition(this);
                float sensitivity = Renderer.Camera.Mode == CameraMode.Arcball ? -0.5f : -0.2f;
                Renderer.Camera.UpdatePosition(0, 0, (float)(current_position.X - MousePosition.X) * sensitivity);
                Renderer.Camera.UpdatePosition(0, (float)(current_position.Y - MousePosition.Y) * sensitivity, 0);
                Draw();
            }
            MousePosition = e.GetPosition(this);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MousePosition = e.GetPosition(this);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                Renderer.Camera.UpdatePosition(-0.3f, 0, 0);
            } else
            {
                Renderer.Camera.UpdatePosition(0.3f, 0, 0);
            }
            Draw();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            Scale = (dpi.DpiScaleX, dpi.DpiScaleY);

            if (IsLoaded)
            {
                Renderer.CreateBuffers(Grid.ActualWidth, Grid.ActualHeight, Scale);
                Canvas.Source = Renderer.Bitmap.Source;
                Draw();
            }
        }

        private void ResizeHandler()
        {
            Renderer.CreateBuffers(Grid.ActualWidth, Grid.ActualHeight, Scale);
            Canvas.Source = Renderer.Bitmap.Source;
            Draw();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.NumPad1:
                    LightingConfig.ChangeLampPosition(new(-0.2f, 0, 0));
                    Draw();
                    break;

                case Key.NumPad2:
                    LightingConfig.ChangeLampPosition(new(0.2f, 0, 0));
                    Draw();
                    break;

                case Key.NumPad4:
                    LightingConfig.ChangeLampPosition(new(0, -0.2f, 0));
                    Draw();
                    break;

                case Key.NumPad5:
                    LightingConfig.ChangeLampPosition(new(0, 0.2f, 0));
                    Draw();
                    break;

                case Key.NumPad7:
                    LightingConfig.ChangeLampPosition(new(0, 0, -0.2f));
                    Draw();
                    break;

                case Key.NumPad8:
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

                case Key.Add:
                    LightingConfig.AmbientIntensity += 0.1f;
                    Draw();
                    break;

                case Key.Subtract:
                    LightingConfig.AmbientIntensity -= 0.1f;
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

                case Key.J:
                    HDRTexture.Angle += 0.1f;
                    Draw();
                    break;

                case Key.K:
                    HDRTexture.Angle -= 0.1f;
                    Draw();
                    break;

                case Key.W:
                    Renderer.Camera.Move(new(0, 0, -0.2f), true);
                    Draw(); 
                    break;

                case Key.S:
                    Renderer.Camera.Move(new(0, 0, 0.2f), true);
                    Draw();
                    break;

                case Key.A:
                    Renderer.Camera.Move(new(-0.2f, 0, 0), true);
                    Draw();
                    break;

                case Key.D:
                    Renderer.Camera.Move(new(0.2f, 0, 0), true);
                    Draw();
                    break;

                case Key.Q:
                    Renderer.Camera.Move(new(0, -0.2f, 0), false);
                    Draw();
                    break;

                case Key.E:
                    Renderer.Camera.Move(new(0, 0.2f, 0), false);
                    Draw();
                    break;
            }

            if (!e.IsRepeat)
            {
                switch (e.Key)
                {
                    case Key.O:
                        OpenFileDialog dlg = new();
                        dlg.Filter = "Wavefront (*.obj)|*.obj";
                        if (dlg.ShowDialog() == true)
                        {
                            MainModel = new();

                            Timer.Restart();
                            LoadModel(dlg.FileName, MainModel);
                            MainModel.CalculateTangents();
                            Timer.Stop();
                            Model_time.Content = $"Model loaded in {double.Round(Timer.ElapsedMilliseconds)} ms";

                            Timer.Restart();

                            if (MainModel.OpaqueFacesIndices.Count > 0)
                                BVH.Build(MainModel.Positions, MainModel.OpaqueFacesIndices, MainModel.PositionIndices);

                            BVH_time.Content = $"BVH builded in {double.Round(Timer.ElapsedMilliseconds)} ms";
                            Timer.Stop();

                            Renderer.Camera.Target = MainModel.GetCenter();
                            Renderer.Camera.Reset();

                            Draw();
                        }
                        break;

                    case Key.R:
                        LightingConfig.UseShadow = !LightingConfig.UseShadow;
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

                    case Key.F6:
                        Renderer.UseBloom = !Renderer.UseBloom;
                        Draw();
                        break;

                    case Key.I:
                        Renderer.UseTangentNormals = !Renderer.UseTangentNormals;
                        Draw();
                        break;

                    case Key.T:
                        ToneMapping.Mode = (ToneMappingMode)(((int)ToneMapping.Mode + 1) % 3);
                        Draw();
                        break;

                    case Key.Y:
                        if (ToneMapping.Mode == ToneMappingMode.AgX)
                        {
                            ToneMapping.LookMode = (AgXLookMode)(((int)ToneMapping.LookMode + 1) % 3);
                        }
                        Draw();
                        break;

                    case Key.F12:
                        PngBitmapEncoder encoder = new();
                        encoder.Frames.Add(BitmapFrame.Create(Renderer.Bitmap.Source));
                        DirectoryInfo info = Directory.CreateDirectory("img");
                        using (FileStream st = new($@"{info.Name}/{DateTime.Now:dd-MM-yyyy HH-mm-ss-fff}.png", FileMode.Create))
                        {
                            encoder.Save(st);
                        }
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

                    case Key.P:
                        Renderer.CurrentShader = (ShaderTypes)(((int)Renderer.CurrentShader + 1) % 4);
                        Draw();
                        break;

                    case Key.F5:
                        Renderer.UseSkyBox = !Renderer.UseSkyBox;
                        Draw();
                        break;

                    case Key.F7:
                        Renderer.Camera.Mode = (CameraMode)(((int)Renderer.Camera.Mode + 1) % 2);
                        Draw();
                        break;

                    case Key.NumPad0:
                        Renderer.Camera.Target = MainModel.GetCenter();
                        Renderer.Camera.Reset();
                        Draw();
                        break;

                    case Key.D1:
                        Renderer.Smoothing = 0.25f;
                        ResizeHandler();
                        break;

                    case Key.D2:
                        Renderer.Smoothing = 0.5f;
                        ResizeHandler();
                        break;

                    case Key.D3:
                        Renderer.Smoothing = 1;
                        ResizeHandler();
                        break;

                    case Key.D4:
                        Renderer.Smoothing = 2;
                        ResizeHandler();
                        break;

                    case Key.D5:
                        Renderer.Smoothing = 4;
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
                        LightingConfigWindow lightingConfigWindow = new();
                        lightingConfigWindow.ShowDialog();
                        Draw();
                        break;

                    case Key.F3:
                        BloomConfigWindow bloomConfigWindow = new();
                        bloomConfigWindow.ShowDialog();
                        Draw();
                        break;

                    case Key.F4:
                        IBLConfigWindow iBLConfigWindow = new();
                        iBLConfigWindow.ShowDialog();
                        Draw();
                        break;
                }
            }

        }
    }
}
