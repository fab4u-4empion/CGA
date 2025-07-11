﻿using lab1.Effects;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using static lab1.BloomConfig;

namespace lab1
{
    /// <summary>
    /// Логика взаимодействия для BloomConfigWindow.xaml
    /// </summary>
    public partial class BloomConfigWindow : Window
    {
        public BloomConfigWindow()
        {
            InitializeComponent();
        }

        private static readonly Pbgra32Bitmap bmp = new(new BitmapImage(new Uri("./BloomPreviewImg.png", UriKind.Relative)));
        private readonly Pbgra32Bitmap preview = new(bmp.PixelWidth, bmp.PixelHeight);

        static int NewKernelNumber = 0;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ImgPreview.Source = preview.Source;
            ImgKernel.Source = KernelImg?.Source;
            UpdateListBox();
            KernelListBox.SelectedIndex = 0;
        }

        private void UpdateListBox()
        {
            int selectedIndex = KernelListBox.SelectedIndex;
            KernelListBox.ItemsSource = Kernels.Select(x => x.Name);
            KernelListBox.SelectedIndex = int.Max(0, int.Min(selectedIndex, KernelListBox.Items.Count - 1));

            Buffer<Vector3> bmpBuf = new(bmp.PixelWidth, bmp.PixelHeight);

            for (int y = 0; y < bmp.PixelHeight; y++)
                for (int x = 0; x < bmp.PixelWidth; x++)
                    bmpBuf[x, y] = bmp.GetPixel(x, y) * 10;
            Buffer<Vector3> bloomBuf = KernelImg == null ?
                Bloom.GetGaussianClassicBlur(bmpBuf, bmp.PixelWidth, bmp.PixelHeight, 1)
                : Bloom.GetImageBasedBlur(bmpBuf, bmp.PixelWidth, bmp.PixelHeight);

            preview.Source.Lock();
            for (int y = 0; y < bmp.PixelHeight; y++)
                for (int x = 0; x < bmp.PixelWidth; x++)
                    preview.SetPixel(x, y, ToneMapping.LinearToSrgb(ToneMapping.AgX(bmpBuf[x, y] + bloomBuf[x, y])));
            preview.Source.AddDirtyRect(new(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            preview.Source.Unlock();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Kernel kernel = new() { Name = $"New kernel {NewKernelNumber}", Radius = 0, Intensity = 0 };
            Kernels.Add(kernel);
            NewKernelNumber++;
            UpdateListBox();
            KernelListBox.SelectedIndex = KernelListBox.Items.Count - 1;
        }

        private void KernelListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (KernelListBox.SelectedIndex > -1)
            {
                Kernel kernel = Kernels[KernelListBox.SelectedIndex];
                KernelName.Text = kernel.Name;
                KernelInt.Text = kernel.Intensity.ToString("0.###", CultureInfo.InvariantCulture);
                KernelR.Text = kernel.Radius.ToString("0.###");
            }
            else
            {
                KernelName.Text = "";
                KernelInt.Text = "";
                KernelR.Text = "";
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (KernelListBox.SelectedIndex > -1)
            {
                Kernel kernel = Kernels[KernelListBox.SelectedIndex];
                kernel.Name = KernelName.Text;
                kernel.Radius = int.Parse(KernelR.Text);
                kernel.Intensity = float.Parse(KernelInt.Text, CultureInfo.InvariantCulture);
                UpdateListBox();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (KernelListBox.SelectedIndex > -1)
            {
                Kernels.RemoveAt(KernelListBox.SelectedIndex);
                UpdateListBox();
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            ImgKernel.Source = null;
            KernelImg = null;
            UpdateListBox();
            GC.Collect();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Filter = "Pictures (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (ofd.ShowDialog() == true)
            {
                KernelImg = new(new BitmapImage(new Uri(ofd.FileName)));
                ImgKernel.Source = KernelImg.Source;
                UpdateListBox();
                GC.Collect();
            }
        }
    }
}