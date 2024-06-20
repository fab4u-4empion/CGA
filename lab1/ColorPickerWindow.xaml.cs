using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace lab1
{
    /// <summary>
    /// Логика взаимодействия для ColorPickerWIndow.xaml
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        public Vector3 Color { 
            get
            {
                return new Vector3(color.R, color.G, color.B) / 255f;
            }

            set
            {
                value *= 255;
                color = System.Windows.Media.Color.FromRgb((byte)value.X, (byte)value.Y, (byte)value.Z);
            }
        }

        private Color color = Colors.Black;

        public ColorPickerWindow()
        {
            InitializeComponent();
        }

        private void ColorPicker_MouseMove(object sender, MouseEventArgs e)
        {
            color = ColorPicker.Color;
            ColorPreview.Background = new SolidColorBrush(color);

            SliderR.Value = color.R;
            SliderG.Value = color.G;
            SliderB.Value = color.B;

            LabelR.Content = color.R;
            LabelG.Content = color.G;
            LabelB.Content = color.B;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ColorPicker.Color = color;

            SliderR.Value = color.R;
            SliderG.Value = color.G;
            SliderB.Value = color.B;

            LabelR.Content = color.R;
            LabelG.Content = color.G;
            LabelB.Content = color.B;
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            color.B = (byte)SliderB.Value;
            LabelB.Content = color.B;

            color.G = (byte)SliderG.Value;
            LabelG.Content = color.G;

            color.R = (byte)SliderR.Value;
            LabelR.Content = color.R;

            ColorPicker.Color = color;
        }

        private void ColorPicker_ColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            ColorPreview.Background = new SolidColorBrush(color);
        }
    }
}
