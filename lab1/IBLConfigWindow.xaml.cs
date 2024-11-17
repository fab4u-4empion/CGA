using lab1.Effects;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using static lab1.LightingConfig;
using static lab1.Utils;

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

            AmbientColorBtn.Color = Vector3ToColor(ToneMapping.LinearToSrgb(AmbientColor));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            IBLDiffuseMap = null;
            EnvironmentPreview.Source = null;
            IBLSpecularMap.Clear();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AmbientColor = ToneMapping.SrgbToLinear(ColorToVector3(AmbientColorBtn.Color));
        }
    }
}