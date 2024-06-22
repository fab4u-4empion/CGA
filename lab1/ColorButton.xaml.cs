using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace lab1
{
    public class RgbToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return Color.FromRgb(System.Convert.ToByte(values[0]), System.Convert.ToByte(values[1]), System.Convert.ToByte(values[2]));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return [(double)((Color)value).R, (double)((Color)value).G, (double)((Color)value).B];
        }
    }

    /// <summary>
    /// Логика взаимодействия для ColorButton.xaml
    /// </summary>
    public partial class ColorButton : UserControl
    {
        public Color Color
        {
            get => ColorPicker.Color;
            set => ColorPicker.Color = value;
        }

        public ColorButton()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Popup.IsOpen = !Popup.IsOpen;
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!Border.IsMouseOver && !Popup.IsMouseOver)
            {
                Popup.IsOpen = false;
            }
        }
    }
}