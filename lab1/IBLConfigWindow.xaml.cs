using Microsoft.Win32;
using Rasterization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            OpenFileDialog ofd = new();
            if (ofd.ShowDialog() == true )
            {
                LightingConfig.IBLDiffuseMap = new();
                LightingConfig.IBLDiffuseMap.Open(ofd.FileName);

                DiffusePreview.Source = LightingConfig.IBLDiffuseMap.ToLDR().Source;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (LightingConfig.IBLDiffuseMap != null)
            {
                DiffusePreview.Source = LightingConfig.IBLDiffuseMap.ToLDR().Source;
            }
        }
    }
}
