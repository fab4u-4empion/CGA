using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static lab1.LightingConfig;
using static lab1.Utils;
using static System.Int32;
using static System.Single;

namespace lab1
{
    public partial class LightingConfigWindow : Window
    {
        static int NewLampNumber = 0;

        public LightingConfigWindow()
        {
            InitializeComponent();
        }

        private void UpdateListBox()
        {
            int selectedIndex = LightsListBox.SelectedIndex;
            LightsListBox.ItemsSource = Lights.Select(x => x.Name);
            LightsListBox.SelectedIndex = Max(0, Min(selectedIndex, LightsListBox.Items.Count - 1));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LampColorBtn.Color = Colors.Black;
            UpdateListBox();
            LightsListBox.SelectedIndex = 0;
        }

        private void LightsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LightsListBox.SelectedIndex > -1)
            {
                Lamp lamp = Lights[LightsListBox.SelectedIndex];
                LampName.Text = lamp.Name;
                (LampPos_X.Text, LampPos_Y.Text, LampPos_Z.Text) = (lamp.Position.X.ToString(CultureInfo.InvariantCulture), lamp.Position.Y.ToString(CultureInfo.InvariantCulture), lamp.Position.Z.ToString(CultureInfo.InvariantCulture));
                (LampDir_T.Text, LampDir_Ph.Text) = (lamp.Theta.ToString(CultureInfo.InvariantCulture), lamp.Phi.ToString(CultureInfo.InvariantCulture));
                LampInt.Text = lamp.Intensity.ToString(CultureInfo.InvariantCulture);
                LampType.SelectedIndex = (int)lamp.Type;
                LampColorBtn.Color = Vector3ToColor(ToneMapping.LinearToSrgb(lamp.Color));
                LampAngle.Text = lamp.Angle.ToString(CultureInfo.InvariantCulture);
                LampRadius.Text = lamp.Radius.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                (LampPos_X.Text, LampPos_Y.Text, LampPos_Z.Text) = ("", "", "");
                (LampDir_T.Text, LampDir_Ph.Text) = ("", "");
                LampInt.Text = "";
                LampName.Text = "";
                LampType.SelectedIndex = -1;
                LampColorBtn.Color = Colors.Black;
                LampAngle.Text = "";
                LampRadius.Text = "";
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Lamp lamp = new() { Color = new(1, 1, 1), Intensity = 100, Position = new(0, 0, 0), Name = $"New lamp {NewLampNumber}" };
            Lights.Add(lamp);
            UpdateListBox();
            LightsListBox.SelectedIndex = LightsListBox.Items.Count - 1;
            NewLampNumber++;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (LightsListBox.SelectedIndex > -1)
            {
                Lights.RemoveAt(LightsListBox.SelectedIndex);
                if (Lights.Count == 0)
                    CurrentLamp = -1;
                else
                    CurrentLamp = 0;
                UpdateListBox();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (LightsListBox.SelectedIndex > -1)
            {
                Lamp lamp = Lights[LightsListBox.SelectedIndex];
                lamp.Name = LampName.Text;
                lamp.Position = new(
                    float.Parse(LampPos_X.Text, CultureInfo.InvariantCulture),
                    float.Parse(LampPos_Y.Text, CultureInfo.InvariantCulture),
                    float.Parse(LampPos_Z.Text, CultureInfo.InvariantCulture)
                );
                lamp.Color = ToneMapping.SrgbToLinear(ColorToVector3(LampColorBtn.Color));
                lamp.Intensity = float.Parse(LampInt.Text, CultureInfo.InvariantCulture);
                lamp.Theta = Clamp(float.Parse(LampDir_T.Text, CultureInfo.InvariantCulture), 0, 180);
                lamp.Phi = Clamp(float.Parse(LampDir_Ph.Text, CultureInfo.InvariantCulture), 0, 360);
                lamp.Type = (LampTypes)LampType.SelectedIndex;
                lamp.Angle = Clamp(float.Parse(LampAngle.Text, CultureInfo.InvariantCulture), 0, 90);
                lamp.Radius = Max(float.Parse(LampRadius.Text, CultureInfo.InvariantCulture), 0);
                UpdateListBox();
            }
        }

        private void LampType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LampType.SelectedIndex == 0)
            {
                PositionGrid.Visibility = Visibility.Visible;
                RadiusGrid.Visibility = Visibility.Visible;

                DirectionGrid.Visibility = Visibility.Collapsed;
                AngleGrid.Visibility = Visibility.Collapsed;
            }

            if (LampType.SelectedIndex == 1)
            {
                PositionGrid.Visibility = Visibility.Collapsed;
                RadiusGrid.Visibility = Visibility.Collapsed;

                DirectionGrid.Visibility = Visibility.Visible;
                AngleGrid.Visibility = Visibility.Visible;
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Filter = "Light Configs (*.lght)|*.lght"
            };

            if (ofd.ShowDialog() == true)
            {
                Lamp? lamp = null;

                foreach (string l in File.ReadLines(ofd.FileName))
                {
                    string[] line = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    if (line.Length == 0) continue;

                    switch (line[0])
                    {
                        case "newlmp":
                            if (lamp != null)
                                Lights.Add(lamp);
                            lamp = new() { Name = line[1] };
                            break;

                        case "color":
                            lamp!.Color = ToneMapping.SrgbToLinear(new(
                                float.Parse(line[1], CultureInfo.InvariantCulture),
                                float.Parse(line[2], CultureInfo.InvariantCulture),
                                float.Parse(line[3], CultureInfo.InvariantCulture)
                            ));
                            break;

                        case "position":
                            lamp!.Position = new(
                                float.Parse(line[1], CultureInfo.InvariantCulture),
                                float.Parse(line[2], CultureInfo.InvariantCulture),
                                float.Parse(line[3], CultureInfo.InvariantCulture)
                            );
                            break;

                        case "theta":
                            lamp!.Theta = float.Parse(line[1], CultureInfo.InvariantCulture);
                            break;

                        case "phi":
                            lamp!.Phi = float.Parse(line[1], CultureInfo.InvariantCulture);
                            break;

                        case "intensity":
                            lamp!.Intensity = float.Parse(line[1], CultureInfo.InvariantCulture);
                            break;

                        case "radius":
                            lamp!.Radius = float.Parse(line[1], CultureInfo.InvariantCulture);
                            break;

                        case "angle":
                            lamp!.Angle = float.Parse(line[1], CultureInfo.InvariantCulture);
                            break;

                        case "type":
                            switch (line[1])
                            {
                                case "point":
                                    lamp!.Type = LampTypes.Point;
                                    break;

                                case "directional":
                                    lamp!.Type = LampTypes.Directional;
                                    break;
                            }

                            break;
                    }
                }

                if (lamp != null)
                    Lights.Add(lamp);

                UpdateListBox();
                LightsListBox.SelectedIndex = 0;
            }
        }
    }
}