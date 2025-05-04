using lab1.Effects;
using lab1.Shadow;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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

            foreach (string l in File.ReadLines(fileName))
            {
                string[] line = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                if (line.Length == 0) continue;

                switch (line[0])
                {
                    case "newmtl":
                        if (material != null)
                        {
                            model.Materials.Add(material);
                            mtlIndex++;
                        }
                        material = new();
                        model.MaterialNames.Add(line[1], mtlIndex);
                        break;

                    case "Kd":
                        material!.Kd = ToneMapping.SrgbToLinear(new(
                            float.Parse(line[1], CultureInfo.InvariantCulture),
                            float.Parse(line[2], CultureInfo.InvariantCulture),
                            float.Parse(line[3], CultureInfo.InvariantCulture)
                        ));
                        break;

                    case "map_Kd":
                        material!.Diffuse = model.AddTexture(Path.Combine(fold!, line[1]), useSrgbToLinearTransform: true);
                        break;

                    case "Ks":
                        material!.Ks = ToneMapping.SrgbToLinear(new(
                            float.Parse(line[1], CultureInfo.InvariantCulture),
                            float.Parse(line[2], CultureInfo.InvariantCulture),
                            float.Parse(line[3], CultureInfo.InvariantCulture)
                        ));
                        break;

                    case "map_Ks":
                        material!.Specular = model.AddTexture(Path.Combine(fold!, line[1]), useSrgbToLinearTransform: true);
                        break;

                    case "d":
                        material!.D = float.Parse(line[1], CultureInfo.InvariantCulture);
                        material!.BlendMode = BlendMode.AlphaBlending;
                        break;

                    case "map_d":
                        material!.Dissolve = model.AddTexture(Path.Combine(fold!, line[1]));
                        material!.BlendMode = BlendMode.AlphaBlending;
                        break;

                    case "Tr":
                        material!.Tr = float.Parse(line[1], CultureInfo.InvariantCulture);
                        material!.BlendMode = BlendMode.AlphaBlending;
                        break;

                    case "map_Tr":
                        material!.Transmission = model.AddTexture(Path.Combine(fold!, line[1]));
                        material!.BlendMode = BlendMode.AlphaBlending;
                        break;

                    case "Pm":
                        material!.Pm = float.Parse(line[1], CultureInfo.InvariantCulture);
                        break;

                    case "Pr":
                        material!.Pr = float.Parse(line[1], CultureInfo.InvariantCulture);
                        break;

                    case "map_MRAO":
                        material!.MRAO = model.AddTexture(Path.Combine(fold!, line[1]));
                        break;

                    case "map_ORM":
                        material!.MRAO = model.AddTexture(Path.Combine(fold!, line[1]));
                        material!.UseORM = true;
                        break;

                    case "map_Ke":
                        material!.Emission = model.AddTexture(Path.Combine(fold!, line[1]), useSrgbToLinearTransform: true);
                        break;

                    case "norm":
                        material!.Normals = model.AddTexture(Path.Combine(fold!, line[1]), isNormal: true);
                        break;

                    case "Pc":
                        material!.Pc = float.Parse(line[1], CultureInfo.InvariantCulture);
                        break;

                    case "Pcr":
                        material!.Pcr = float.Parse(line[1], CultureInfo.InvariantCulture);
                        break;

                    case "map_Pc":
                        material!.ClearCoat = model.AddTexture(Path.Combine(fold!, line[1]));
                        break;

                    case "map_Pcr":
                        material!.ClearCoatRoughness = model.AddTexture(Path.Combine(fold!, line[1]));
                        break;

                    case "norm_pc":
                        material!.ClearCoatNormals = model.AddTexture(Path.Combine(fold!, line[1]), isNormal: true);
                        break;
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
            ResolutionInfo.Text = $"{Renderer.Bitmap.PixelWidth} × {Renderer.Bitmap.PixelHeight}";

            SkyboxInfo.Text = $"{(Renderer.UseSkyBox ? "On" : "Off")}";
            BloomInfo.Text = $"{(Renderer.UseBloom ? "On" : "Off")}";
            RTAOInfo.Text = $"{(LightingConfig.UseRTAO ? "On" : "Off")}";
            ShadowsInfo.Text = $"{(LightingConfig.UseShadow ? "On" : "Off")}";
            GroundInfo.Text = $"{(LightingConfig.DrawGround ? "On" : "Off")}";
            LampsInfo.Text = $"{(LightingConfig.DrawLamps ? "On" : "Off")}";
            BackfaceInfo.Text = $"{(Renderer.BackfaceCulling ? "On" : "Off")}";

            RTAORayDistInfo.Text = RTX.RTAORayDistance.ToString("0.0", CultureInfo.InvariantCulture);
            RTAORayInfo.Text = $"{RTX.RTAORayCount}";
            ShadowRayInfo.Text = $"{RTX.ShadowRayCount}";
            CurrentLampInfo.Text = $"{(LightingConfig.CurrentLamp == -1 ? "*" : LightingConfig.Lights[LightingConfig.CurrentLamp].Name)}";

            CameraInfo.Text = $"{Renderer.Camera.Mode}";
            FovInfo.Text = $"{Round(RadiansToDegrees(Renderer.Camera.FoV), MidpointRounding.AwayFromZero)}°";

            ShaderInfo.Text = $"{Renderer.CurrentShader}";
            NormalsInfo.Text = $"{(Renderer.UseTangentNormals ? "Tangent" : "Object")}";
            TonemapInfo.Text = $"{ToneMapping.ToneMapper}";

            if (Material.UsingMIPMapping)
            {
                if (Material.MaxAnisotropy == 1)
                    FilteringInfo.Text = "Trilinear";
                else
                    FilteringInfo.Text = $"{Material.MaxAnisotropy}× Anisotropic";
            }
            else
                FilteringInfo.Text = "Bilinear";
        }

        private void Draw()
        {
            UpdateInfo();

            Timer.Restart();

            Renderer.Draw(MainModel);

            Timer.Stop();

            RenderTimeInfo.Text = (double.Round(Timer.ElapsedMilliseconds) + " ms");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Renderer.CreateBuffers(Grid.ActualWidth, Grid.ActualHeight, Scale);

            Canvas.Source = Renderer.Bitmap.Source;

            Renderer.Sphere = new Model();
            LoadModel(Path.Combine(Directory.GetCurrentDirectory(), "model.obj"), Renderer.Sphere);

            LightingConfig.BRDFLLUT.Open("BRDFIntegrationMap.hdr");

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
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                Renderer.Camera.FoV -= Pi / 60 * Sign(e.Delta);
                Renderer.Camera.FoV = Clamp(Renderer.Camera.FoV, Pi / 6, Pi / 2);
            }
            else
            {
                Renderer.Camera.UpdatePosition(-0.3f * Sign(e.Delta), 0, 0);
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
                        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                            LightingConfig.DrawGround = !LightingConfig.DrawGround;
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                            LightingConfig.UseRTAO = !LightingConfig.UseRTAO;
                        else
                            LightingConfig.UseShadow = !LightingConfig.UseShadow;
                        Draw();
                    }
                    break;

                case Key.F8:
                    if (!e.IsRepeat)
                    {
                        LightingConfig.DrawLamps = !LightingConfig.DrawLamps;
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
                        Renderer.Scaling = 0.25f;
                        ResizeHandler();
                    }
                    break;

                case Key.D2:
                    if (!e.IsRepeat)
                    {
                        Renderer.Scaling = 0.5f;
                        ResizeHandler();
                    }
                    break;

                case Key.D3:
                    if (!e.IsRepeat)
                    {
                        Renderer.Scaling = 1;
                        ResizeHandler();
                    }
                    break;

                case Key.D4:
                    if (!e.IsRepeat)
                    {
                        Renderer.Scaling = 2;
                        ResizeHandler();
                    }
                    break;

                case Key.D5:
                    if (!e.IsRepeat)
                    {
                        Renderer.Scaling = 4;
                        ResizeHandler();
                    }
                    break;

                //Emission and Ambient
                case Key.OemMinus:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        LightingConfig.EmissionIntensity -= 10f;
                        LightingConfig.EmissionIntensity = Max(LightingConfig.EmissionIntensity, 0);
                    }
                    else
                    {
                        ToneMapping.Exposure *= 0.84089642f;
                        ToneMapping.Exposure = Max(ToneMapping.Exposure, 0.03125f);
                    }
                    Draw();
                    break;

                case Key.OemPlus:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.EmissionIntensity += 10f;
                    else
                    {
                        ToneMapping.Exposure *= 1.18920712f;
                        ToneMapping.Exposure = Min(ToneMapping.Exposure, 32);
                    }
                    Draw();
                    break;

                //Arrows
                case Key.Right:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampSize(0.01f, 0.5f);
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        RTX.RTAORayDistance += 0.1f;
                    else
                        LightingConfig.ChangeLampIntensity(10);
                    Draw();
                    break;

                case Key.Left:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        LightingConfig.ChangeLampSize(-0.01f, -0.5f);
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        RTX.RTAORayDistance = Max(0, RTX.RTAORayDistance - 0.1f);
                    else
                        LightingConfig.ChangeLampIntensity(-10);
                    Draw();
                    break;

                case Key.Up:
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            RTX.ShadowRayCount = Min(4096, RTX.ShadowRayCount * 2);
                            Draw();
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            RTX.RTAORayCount = Min(4096, RTX.RTAORayCount * 2);
                            Draw();
                        }
                        else
                        {
                            LightingConfig.ChangeLamp(1);
                            UpdateInfo();
                        }
                    }
                    break;

                case Key.Down:
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            RTX.ShadowRayCount = Max(1, RTX.ShadowRayCount / 2);
                            Draw();
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            RTX.RTAORayCount = Max(1, RTX.RTAORayCount / 2);
                            Draw();
                        }
                        else
                        {
                            LightingConfig.ChangeLamp(-1);
                            UpdateInfo();
                        }
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
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                            HDRTexture.Angle = 0;
                        else
                            Renderer.Camera.Reset(MainModel == null ? Vector3.Zero : MainModel.GetCenter());
                        Draw();
                    }
                    break;

                case Key.Space:
                    if (!e.IsRepeat)
                    {
                        Renderer.Camera.Mode = (CameraMode)(((int)Renderer.Camera.Mode + 1) % Enum.GetNames<CameraMode>().Length);
                        UpdateInfo();
                    }
                    break;

                //Other
                case Key.T:
                    if (!e.IsRepeat)
                    {
                        ToneMapping.ToneMapper = (ToneMapper)(((int)ToneMapping.ToneMapper + 1) % Enum.GetNames<ToneMapper>().Length);
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

                            LoadModel(dlg.FileName, MainModel);
                            MainModel.CalculateTangents();

                            if (MainModel.OpaqueFacesIndices.Count > 0)
                                BVH.Build(MainModel.Positions, MainModel.OpaqueFacesIndices, MainModel.PositionIndices);

                            Renderer.Camera.Reset(MainModel.GetCenter());

                            GC.Collect();

                            Draw();
                        }
                    }
                    break;

                case Key.P:
                    if (!e.IsRepeat)
                    {
                        Renderer.CurrentShader = (ShaderType)(((int)Renderer.CurrentShader + 1) % Enum.GetNames<ShaderType>().Length);
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

                case Key.I:
                    if (!e.IsRepeat)
                    {
                        UI.Visibility = UI.Visibility == Visibility.Visible ?
                            Visibility.Collapsed : Visibility.Visible;
                    }
                    break;
            }
        }
    }
}