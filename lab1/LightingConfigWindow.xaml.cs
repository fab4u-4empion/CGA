using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using static lab1.LightingConfig;

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
            LightsListBox.ItemsSource = Lights.Select(x => x.Name);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateListBox();
        }

        private void LightsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LightsListBox.SelectedIndex > -1)
            {
                Lamp lamp = Lights[LightsListBox.SelectedIndex];
                LampName.Text = lamp.Name;
                (LampPos_X.Text, LampPos_Y.Text, LampPos_Z.Text) = (lamp.Position.X.ToString(CultureInfo.InvariantCulture), lamp.Position.Y.ToString(CultureInfo.InvariantCulture), lamp.Position.Z.ToString(CultureInfo.InvariantCulture));
                (LampCol_R.Text, LampCol_G.Text, LampCol_B.Text) = (lamp.Color.X.ToString(CultureInfo.InvariantCulture), lamp.Color.Y.ToString(CultureInfo.InvariantCulture), lamp.Color.Z.ToString(CultureInfo.InvariantCulture));
                LampInt.Text = lamp.Intensity.ToString(CultureInfo.InvariantCulture);
                LampType.SelectedIndex = (int)lamp.Type;
            }
            else
            {
                (LampPos_X.Text, LampPos_Y.Text, LampPos_Z.Text) = ("", "", "");
                (LampCol_R.Text, LampCol_G.Text, LampCol_B.Text) = ("", "", "");
                LampInt.Text = "";
                LampName.Text = "";
                LampType.SelectedIndex = -1;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Lamp lamp = new() { Color = new(1, 1, 1), Intensity = 100, Position = new(0, 0, 0), Name = $"New lamp {NewLampNumber}" };
            Lights.Add(lamp);
            UpdateListBox();
            NewLampNumber++;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Lights.RemoveAt(LightsListBox.SelectedIndex);
            if (Lights.Count == 0)
                CurrentLamp = -1;
            else
                CurrentLamp = 0;
            UpdateListBox();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (LightsListBox.SelectedIndex > -1)
            {
                Lamp lamp = Lights[LightsListBox.SelectedIndex];
                lamp.Name = LampName.Text;
                lamp.Color = new(
                    float.Parse(LampCol_R.Text, CultureInfo.InvariantCulture),
                    float.Parse(LampCol_G.Text, CultureInfo.InvariantCulture),
                    float.Parse(LampCol_B.Text, CultureInfo.InvariantCulture)
                );
                lamp.Position = new(
                    float.Parse(LampPos_X.Text, CultureInfo.InvariantCulture),
                    float.Parse(LampPos_Y.Text, CultureInfo.InvariantCulture),
                    float.Parse(LampPos_Z.Text, CultureInfo.InvariantCulture)
                );
                lamp.Intensity = float.Parse(LampInt.Text, CultureInfo.InvariantCulture);
                lamp.Type = (LampTypes)LampType.SelectedIndex;
                UpdateListBox();
            }
        }
    }
}
