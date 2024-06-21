using lab1.Shadow;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Int32;
using static System.Single;

namespace lab1
{
    using DPIScale = (double X, double Y);

    public partial class MainWindow : Window
    {
        Model? MainModel;

        readonly Renderer Renderer;

        DPIScale Scale = (1, 1);

        readonly Stopwatch Timer = new();

        Point MousePosition;

        WindowState LastState;

        public MainWindow()
        {
            Renderer = new();
            InitializeComponent();
        }

        private static void LoadMaterials(string fileName, Model model)
        {
            string? fold = Path.GetDirectoryName(fileName);

            using StreamReader mtlReader = new(fileName);

            Material? material = null;
            int mtlIndex = 0;

            while (!mtlReader.EndOfStream)
            {
                string mtlLine = mtlReader.ReadLine()!.Trim();

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
                    material!.Pm = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                }

                if (mtlLine.StartsWith("map_Tr"))
                {
                    material!.Transmission = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 6).Trim()));
                    material!.BlendMode = BlendModes.AlphaBlending;
                }

                if (mtlLine.StartsWith("Tr"))
                {
                    material!.Tr = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                    material!.BlendMode = BlendModes.AlphaBlending;
                }

                if (mtlLine.StartsWith('d'))
                {
                    material!.D = float.Parse(mtlLine.Remove(0, 1).Trim(), CultureInfo.InvariantCulture);
                    material!.BlendMode = BlendModes.AlphaBlending;
                }

                if (mtlLine.StartsWith("map_d"))
                {
                    material!.Dissolve = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 5).Trim()));
                    material!.BlendMode = BlendModes.AlphaBlending;
                }

                if (mtlLine.StartsWith("Kd"))
                {
                    float[] Kd = mtlLine
                        .Remove(0, 2)
                        .Trim()
                        .Split(' ')
                        .Select(c => float.Parse(c, CultureInfo.InvariantCulture))
                        .ToArray();
                    material!.Kd = ToneMapping.SrgbToLinear(new(Kd[0], Kd[1], Kd[2]));
                }

                if (mtlLine.StartsWith("Ks"))
                {
                    float[] Kd = mtlLine
                        .Remove(0, 2)
                        .Trim()
                        .Split(' ')
                        .Select(c => float.Parse(c, CultureInfo.InvariantCulture))
                        .ToArray();
                    material!.Ks = ToneMapping.SrgbToLinear(new(Kd[0], Kd[1], Kd[2]));
                }

                if (mtlLine.StartsWith("Pr"))
                {
                    material!.Pr = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                }

                if (mtlLine.StartsWith("map_Kd"))
                {
                    material!.Diffuse = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 6).Trim()), true);
                }

                if (mtlLine.StartsWith("map_Ks"))
                {
                    material!.Specular = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 6).Trim()), true);
                }

                if (mtlLine.StartsWith("map_Ke"))
                {
                    material!.Emission = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 6).Trim()), true);
                }

                if (mtlLine.StartsWith("map_MRAO"))
                {
                    material!.MRAO = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 8).Trim()));
                }

                if (mtlLine.StartsWith("map_Pcr"))
                {
                    material!.ClearCoatRoughness = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 7).Trim()));
                    continue;
                }

                if (mtlLine.StartsWith("Pcr"))
                {
                    material!.Pcr = float.Parse(mtlLine.Remove(0, 3).Trim(), CultureInfo.InvariantCulture);
                    continue;
                }

                if (mtlLine.StartsWith("map_Pc"))
                {
                    material!.ClearCoat = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 6).Trim()));
                }

                if (mtlLine.StartsWith("Pc"))
                {
                    material!.Pc = float.Parse(mtlLine.Remove(0, 2).Trim(), CultureInfo.InvariantCulture);
                }

                if (mtlLine.StartsWith("norm_pc"))
                {
                    material!.ClearCoatNormals = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 7).Trim()), false, true);
                    continue;
                }

                if (mtlLine.StartsWith("norm"))
                {
                    material!.Normals = model.AddTexture(Path.Combine(fold!, mtlLine.Remove(0, 4).Trim()), false, true);
                }
            }
            model.Materials.Add(material!);
        }

        private static void LoadModel(string fileName, Model model)
        {
            int materialIndex = 0;
            int faceIndex = 0;

            foreach (string l in File.ReadLines(fileName))
            {
                string[] line = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                if (line.Length == 0) continue;

                switch (line[0])
                {
                    case "mtllib":
                        LoadMaterials(Path.Combine(Path.GetDirectoryName(fileName)!, line[1]), model);
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
            }
            else
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
                //F* keys
                case Key.F2:
                    if (!e.IsRepeat)
                    {
                        LightingConfigWindow lightingConfigWindow = new();
                        lightingConfigWindow.ShowDialog();
                        Draw();
                    }
                    break;

                case Key.F3:
                    if (!e.IsRepeat)
                    {
                        BloomConfigWindow bloomConfigWindow = new();
                        bloomConfigWindow.ShowDialog();
                        Draw();
                    }
                    break;

                case Key.F4:
                    if (!e.IsRepeat)
                    {
                        IBLConfigWindow iBLConfigWindow = new();
                        iBLConfigWindow.ShowDialog();
                        Draw();
                    }
                    break;

                case Key.F5:
                    if (!e.IsRepeat)
                    {
                        Renderer.UseSkyBox = !Renderer.UseSkyBox;
                        Draw();
                    }
                    break;

                case Key.F6:
                    if (!e.IsRepeat)
                    {
                        Renderer.UseBloom = !Renderer.UseBloom;
                        Draw();
                    }
                    break;

                case Key.F7:
                    if (!e.IsRepeat)
                    {
                        LightingConfig.UseShadow = !LightingConfig.UseShadow;
                        Draw();
                    }
                    break;

                case Key.F8:
                    if (!e.IsRepeat)
                    {
                        LightingConfig.DrawLights = !LightingConfig.DrawLights;
                        Draw();
                    }
                    break;

                case Key.F9:
                    if (!e.IsRepeat)
                    {
                        Renderer.BackfaceCulling = !Renderer.BackfaceCulling;
                        Draw();
                    }
                    break;

                case Key.F11:
                    if (!e.IsRepeat)
                    {
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
                    }
                    break;

                case Key.F12:
                    if (!e.IsRepeat)
                    {
                        PngBitmapEncoder encoder = new();
                        encoder.Frames.Add(BitmapFrame.Create(Renderer.Bitmap.Source));
                        DirectoryInfo info = Directory.CreateDirectory("img");
                        using FileStream st = new($@"{info.Name}/{DateTime.Now:dd-MM-yyyy HH-mm-ss-fff}.png", FileMode.Create);
                        encoder.Save(st);
                    }
                    break;

                //Resolution
                case Key.D1:
                    if (!e.IsRepeat)
                    {
                        Renderer.Smoothing = 0.25f;
                        ResizeHandler();
                    }
                    break;

                case Key.D2:
                    if (!e.IsRepeat)
                    {
                        Renderer.Smoothing = 0.5f;
                        ResizeHandler();
                    }
                    break;

                case Key.D3:
                    if (!e.IsRepeat)
                    {
                        Renderer.Smoothing = 1;
                        ResizeHandler();
                    }
                    break;

                case Key.D4:
                    if (!e.IsRepeat)
                    {
                        Renderer.Smoothing = 2;
                        ResizeHandler();
                    }
                    break;

                case Key.D5:
                    if (!e.IsRepeat)
                    {
                        Renderer.Smoothing = 4;
                        ResizeHandler();
                    }
                    break;

                //Emission and Ambient
                case Key.OemMinus:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        LightingConfig.EmissionIntensity -= 0.2f;
                        LightingConfig.EmissionIntensity = Max(LightingConfig.EmissionIntensity, 0);
                    }
                    else
                    {
                        //LightingConfig.AmbientIntensity -= 0.1f;
                        //LightingConfig.AmbientIntensity = Max(LightingConfig.AmbientIntensity, 0);
                    }
                    Draw();
                    break;

                case Key.OemPlus:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.EmissionIntensity += 0.2f;
                    else
                        //LightingConfig.AmbientIntensity += 0.1f;
                        Draw();
                    break;

                //Arrows
                case Key.Right:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        RTX.LightSize += 0.001f;
                    else
                        LightingConfig.ChangeLampIntensity(10);
                    Draw();
                    break;

                case Key.Left:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        RTX.LightSize -= 0.001f;
                        RTX.LightSize = Max(RTX.LightSize, 0);
                    }
                    else
                        LightingConfig.ChangeLampIntensity(-10);
                    Draw();
                    break;

                case Key.Up:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        RTX.RayCount += 1;
                        Draw();
                    }
                    else if (!e.IsRepeat)
                    {
                        LightingConfig.ChangeLamp(1);
                        Draw();
                    }
                    break;

                case Key.Down:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        RTX.RayCount -= 1;
                        RTX.RayCount = int.Max(RTX.RayCount, 0);
                        Draw();
                    }
                    else if (!e.IsRepeat)
                    {
                        LightingConfig.ChangeLamp(-1);
                        Draw();
                    }
                    break;

                //Camera and Lights
                case Key.W:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampPosition(new(0, 0, -0.2f));
                    else
                        Renderer.Camera.Move(new(0, 0, -0.2f), true);
                    Draw();
                    break;

                case Key.S:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampPosition(new(0, 0, 0.2f));
                    else
                        Renderer.Camera.Move(new(0, 0, 0.2f), true);
                    Draw();
                    break;

                case Key.A:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampPosition(new(-0.2f, 0, 0));
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        HDRTexture.Angle -= 0.1f;
                    else
                        Renderer.Camera.Move(new(-0.2f, 0, 0), true);
                    Draw();
                    break;

                case Key.D:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampPosition(new(0.2f, 0, 0));
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        HDRTexture.Angle += 0.1f;
                    else
                        Renderer.Camera.Move(new(0.2f, 0, 0), true);
                    Draw();
                    break;

                case Key.Q:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampPosition(new(0, -0.2f, 0));
                    else
                        Renderer.Camera.Move(new(0, -0.2f, 0), false);
                    Draw();
                    break;

                case Key.E:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampPosition(new(0, 0.2f, 0));
                    else
                        Renderer.Camera.Move(new(0, 0.2f, 0), false);
                    Draw();
                    break;

                case Key.Back:
                    if (MainModel != null && !e.IsRepeat)
                    {
                        Renderer.Camera.Target = MainModel.GetCenter();
                        Renderer.Camera.Reset();
                        Draw();
                    }
                    break;

                case Key.Space:
                    if (!e.IsRepeat)
                        Renderer.Camera.Mode = (CameraMode)(((int)Renderer.Camera.Mode + 1) % 2);
                    break;

                //Other
                case Key.T:
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            if (ToneMapping.Mode == ToneMappingMode.AgX)
                                ToneMapping.LookMode = (AgXLookMode)(((int)ToneMapping.LookMode + 1) % 3);
                        }
                        else
                            ToneMapping.Mode = (ToneMappingMode)(((int)ToneMapping.Mode + 1) % 3);
                        Draw();
                    }
                    break;

                case Key.O:
                    if (!e.IsRepeat)
                    {
                        OpenFileDialog dlg = new()
                        {
                            Filter = "Wavefront (*.obj)|*.obj"
                        };

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
                    }
                    break;

                case Key.P:
                    if (!e.IsRepeat)
                    {
                        Renderer.CurrentShader = (ShaderTypes)(((int)Renderer.CurrentShader + 1) % 4);
                        Draw();
                    }
                    break;

                case Key.N:
                    if (!e.IsRepeat)
                    {
                        Renderer.UseTangentNormals = !Renderer.UseTangentNormals;
                        Draw();
                    }
                    break;

                case Key.M:
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            Material.MaxAnisotropy *= 2;
                            if (Material.MaxAnisotropy > 16)
                                Material.MaxAnisotropy = 1;
                        }
                        else
                            Material.UsingMIPMapping = !Material.UsingMIPMapping;
                        Draw();
                    }
                    break;
            }
        }
    }
}
