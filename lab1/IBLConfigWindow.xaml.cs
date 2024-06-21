using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using static lab1.Utils;
using static lab1.LightingConfig;

namespace lab1
{
    /// <summary>
    /// Логика взаимодействия для IBLConfigWindow.xaml
    /// </summary>
    public partial class IBLConfigWindow : Window
    {
        public IBLConfigWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Filter = "IBL Configs (*.ibl)|*.ibl"
            };

            if (ofd.ShowDialog() == true)
            {
                IBLSpecularMap.Clear();

                using (StreamReader reader = new(ofd.FileName))
                {
                    while (!reader.EndOfStream)
                    {
                        string? str = reader.ReadLine();

                        if (str!.StartsWith("irradiance"))
                        {
                            IBLDiffuseMap = new();
                            IBLDiffuseMap.Open(Path.Combine(Path.GetDirectoryName(ofd.FileName)!, str.Remove(0, 10).Trim()));
                        }

                        if (str.StartsWith("specular"))
                        {
                            HDRTexture texture = new();
                            texture.Open(Path.Combine(Path.GetDirectoryName(ofd.FileName)!, str.Remove(0, 8).Trim()));

                            IBLSpecularMap.Add(texture);
                        }
                    }
                }

                EnvironmentPreview.Source = IBLSpecularMap[0].ToLDR().Source;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IBLSpecularMap.Count > 0)
            {
                EnvironmentPreview.Source = IBLSpecularMap[0].ToLDR().Source;
            }

            AmbientColorBtn.Background = new SolidColorBrush(Vector3ToColor(ToneMapping.LinearToSrgb(AmbientColor)));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            IBLDiffuseMap = null;
            EnvironmentPreview.Source = null;
            IBLSpecularMap.Clear();
        }

        private void AmbientColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerWindow colorPicker = new();
            colorPicker.ColorPicker.Color = ((SolidColorBrush)AmbientColorBtn.Background).Color;
            colorPicker.ShowDialog();
            AmbientColorBtn.Background = new SolidColorBrush(colorPicker.ColorPicker.Color);
            AmbientColor = ToneMapping.SrgbToLinear(ColorToVector3(colorPicker.ColorPicker.Color));
        }
    }
}
