using Microsoft.Win32;
using System.IO;
using System.Windows;

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
                LightingConfig.IBLSpecularMap.Clear();

                using (StreamReader reader = new(ofd.FileName))
                {
                    while (!reader.EndOfStream)
                    {
                        string str = reader.ReadLine();

                        if (str.StartsWith("irradiance"))
                        {
                            LightingConfig.IBLDiffuseMap = new();
                            LightingConfig.IBLDiffuseMap.Open(Path.Combine(Path.GetDirectoryName(ofd.FileName), str.Remove(0, 10).Trim()));

                            IrradiancePreview.Source = LightingConfig.IBLDiffuseMap.ToLDR().Source;
                        }

                        if (str.StartsWith("specular"))
                        {
                            HDRTexture texture = new();
                            texture.Open(Path.Combine(Path.GetDirectoryName(ofd.FileName), str.Remove(0, 8).Trim()));

                            LightingConfig.IBLSpecularMap.Add(texture);
                        }

                        if (str.StartsWith("skybox"))
                        {
                            LightingConfig.SkyBox = new();
                            LightingConfig.SkyBox.Open(Path.Combine(Path.GetDirectoryName(ofd.FileName), str.Remove(0, 6).Trim()));
                        }
                    }
                }

                SpecularPreview.Source = LightingConfig.IBLSpecularMap[0].ToLDR().Source;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (LightingConfig.IBLDiffuseMap != null)
            {
                IrradiancePreview.Source = LightingConfig.IBLDiffuseMap.ToLDR().Source;
            }
            if (LightingConfig.IBLSpecularMap.Count > 0)
            {
                SpecularPreview.Source = LightingConfig.IBLSpecularMap[0].ToLDR().Source;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            LightingConfig.IBLDiffuseMap = null;
            LightingConfig.SkyBox = null;
            IrradiancePreview.Source = null;
            SpecularPreview.Source = null;
            LightingConfig.IBLSpecularMap.Clear();
        }
    }
}
