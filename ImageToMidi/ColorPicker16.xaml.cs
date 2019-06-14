using MaterialDesignThemes.Wpf;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageToMidi
{
    /// <summary>
    /// Interaction logic for ColorPicker16.xaml
    /// </summary>

    public partial class ColorPicker16 : UserControl
    {
        Color[] colors = new Color[16];
        bool[] set = new bool[16];

        public event Action PickStart;
        public event Action PickStop;

        public ColorPicker16()
        {
            InitializeComponent();
            for(int i = 0; i < 16; i++)
            {
                getButton(i).Background = Brushes.Transparent;
                colors[i] = Colors.Transparent;
            }
        }

        int colorPickerButton = -1;
        int lastPicker = -1;

        Button getButton(int i)
        {
            return (Button)((Viewbox)buttonDock.Children[i]).Child;
        }

        int getButtonID(Button b)
        {
            return buttonDock.Children.IndexOf((Viewbox)b.Parent);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var b = (Button)sender;
            int id = getButtonID(b);
            if (set[id])
            {
                b.Background = Brushes.Transparent;
                ((PackIcon)b.Content).Kind = PackIconKind.Eyedropper;
                set[id] = false;
            }
            else
            {
                ((PackIcon)b.Content).Kind = PackIconKind.Vanish;
                colorPickerButton = id;
                lastPicker = id;
                PickStart();
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Height = ActualWidth / 16;
        }

        private void Button_LostFocus(object sender, RoutedEventArgs e)
        {
            if(colorPickerButton != -1)
            {
                ((PackIcon)getButton(colorPickerButton).Content).Kind = PackIconKind.Eyedropper;
                colorPickerButton = -1;
                PickStop(); 
            }
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (colorPickerButton != -1)
            {
                ((PackIcon)getButton(colorPickerButton).Content).Kind = PackIconKind.Eyedropper;
                colorPickerButton = -1;
                PickStop();
            }
        }

        public void SendColor(Color c)
        {
            getButton(lastPicker).Background = new SolidColorBrush(c);
            colors[lastPicker] = c;
            set[lastPicker] = true;

            ((PackIcon)getButton(colorPickerButton).Content).Kind = PackIconKind.Tick;
            colorPickerButton = -1;
            PickStop();
        }
    }
}
