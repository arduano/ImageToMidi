using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageToMidi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty FadeInStoryboard =
            DependencyProperty.RegisterAttached("FadeInStoryboard", typeof(Storyboard), typeof(MainWindow), new PropertyMetadata(default(Storyboard)));
        public static readonly DependencyProperty FadeOutStoryboard =
            DependencyProperty.RegisterAttached("FadeOutStoryboard", typeof(Storyboard), typeof(MainWindow), new PropertyMetadata(default(Storyboard)));

        bool leftSelected = true;

        BitmapSource openedImageSrc = null;
        byte[] openedImagePixels = null;
        int openedImageWidth = 0;
        int openedImageHeight = 0;
        string openedImagePath = "";
        BitmapPalette chosenPalette = null;

        ConversionProcess convert = null;

        bool colorPick = false;

        void MakeFadeInOut(DependencyObject e)
        {
            DoubleAnimation fadeIn = new DoubleAnimation();
            fadeIn.From = 0.0;
            fadeIn.To = 1.0;
            fadeIn.Duration = new Duration(TimeSpan.FromSeconds(0.2));

            Storyboard fadeInBoard = new Storyboard();
            fadeInBoard.Children.Add(fadeIn);
            Storyboard.SetTarget(fadeIn, e);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Rectangle.OpacityProperty));

            e.SetValue(FadeInStoryboard, fadeInBoard);

            DoubleAnimation fadeOut = new DoubleAnimation();
            fadeOut.From = 1.0;
            fadeOut.To = 0.0;
            fadeOut.Duration = new Duration(TimeSpan.FromSeconds(0.2));

            Storyboard fadeOutBoard = new Storyboard();
            fadeOutBoard.Children.Add(fadeOut);
            Storyboard.SetTarget(fadeOut, e);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Rectangle.OpacityProperty));

            e.SetValue(FadeOutStoryboard, fadeOutBoard);
        }

        void TriggerMenuTransition(bool left)
        {
            if (left)
            {
                ((Storyboard)selectedHighlightRight.GetValue(FadeOutStoryboard)).Begin();
                ((Storyboard)selectedHighlightLeft.GetValue(FadeInStoryboard)).Begin();
                tabSelect.SelectedIndex = 0;
            }
            else
            {
                ((Storyboard)selectedHighlightRight.GetValue(FadeInStoryboard)).Begin();
                ((Storyboard)selectedHighlightLeft.GetValue(FadeOutStoryboard)).Begin();
                tabSelect.SelectedIndex = 1;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            MakeFadeInOut(selectedHighlightLeft);
            MakeFadeInOut(selectedHighlightRight);
            MakeFadeInOut(colPickerHint);
            MakeFadeInOut(openedImage);
            MakeFadeInOut(genImage);

            colPicker.PickStart += ColPicker_PickStart;
            colPicker.PickStop += ColPicker_PickStop;
            colPicker.PaletteChanged += ReloadPreview;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            double start = windowContent.ActualWidth;
            windowContent.Width = start;
            for (double i = 1; i > 0; i -= 0.05)
            {
                double smooth;
                double strength = 10;
                if (i < 0.5f)
                {
                    smooth = Math.Pow(i * 2, strength) / 2;
                }
                else
                {
                    smooth = 1 - Math.Pow((1 - i) * 2, strength) / 2;
                }
                Width = start * smooth;
                Thread.Sleep(1000 / 60);
            }
            Close();
        }

        private void RawMidiSelect_Click(object sender, RoutedEventArgs e)
        {
            if (!leftSelected)
                TriggerMenuTransition(true);
            leftSelected = true;
            ReloadPreview();
        }

        private void ColorEventsSelect_Click(object sender, RoutedEventArgs e)
        {
            if (leftSelected)
                TriggerMenuTransition(false);
            leftSelected = false;
            ReloadPreview();
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "Common image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
            if (!(bool)open.ShowDialog()) return;
            openedImagePath = open.FileName;
            BitmapImage src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(openedImagePath);
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();
            openedImageWidth = src.PixelWidth;
            openedImageHeight = src.PixelHeight;
            if (openedImageWidth != 128 && openedImageWidth != 256)
            {
                MessageBox.Show("The image must be either 128 or 256 pixels wide\n(For midi keyboard)", "Incorrect format");
                return;
            }
            int stride = src.PixelWidth * 4;
            int size = src.PixelHeight * stride;
            openedImagePixels = new byte[size];
            src.CopyPixels(openedImagePixels, stride, 0);
            openedImage.Source = src;
            openedImageSrc = src;
            ReloadAutoPalette();
            ((Storyboard)openedImage.GetValue(FadeInStoryboard)).Begin();
        }

        private void OpenedImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!colorPick)
            {
                if (openedImagePath == "") return;
                Process imageView = new Process();
                imageView.StartInfo.FileName = openedImagePath;
                imageView.Start();
            }
            else
            {
                var pos = e.GetPosition(openedImage);
                int x = (int)Math.Round(pos.X / openedImage.ActualWidth * openedImageWidth);
                int y = (int)Math.Round(pos.Y / openedImage.ActualHeight * openedImageHeight);
                if (x < 0) x = 0;
                if (x >= openedImageWidth) x = openedImageWidth - 1;
                if (y < 0) y = 0;
                if (y >= openedImageHeight) y = openedImageHeight - 1;

                int s = openedImageWidth * 4;

                Color c = Color.FromArgb(255, openedImagePixels[s * y + x * 4 + 2], openedImagePixels[s * y + x * 4 + 1], openedImagePixels[s * y + x * 4 + 0]);
                colPicker.SendColor(c);
            }
        }

        private void MinimiseButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ColPicker_PickStart()
        {
            Cursor = Cursors.Cross;
            ((Storyboard)colPickerHint.GetValue(FadeInStoryboard)).Begin();
            colorPick = true;
        }

        private void ColPicker_PickStop()
        {
            Cursor = Cursors.Arrow;
            ((Storyboard)colPickerHint.GetValue(FadeOutStoryboard)).Begin();
            colorPick = false;
        }

        private void TrackCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            ReloadAutoPalette();
        }

        void ReloadAutoPalette()
        {
            if (openedImageSrc == null) return;
            int tracks = (int)trackCount.Value;
            chosenPalette = new BitmapPalette(openedImageSrc, tracks * 16);
            ReloadPalettePreview();
            ReloadPreview();
        }

        void ReloadPalettePreview()
        {
            autoPaletteBox.Children.Clear();
            int tracks = (chosenPalette.Colors.Count + 15 - ((chosenPalette.Colors.Count + 15) % 16)) / 16;
            for (int i = 0; i < tracks; i++)
            {
                var dock = new Grid();
                for (int j = 0; j < 16; j++)
                    dock.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                autoPaletteBox.Children.Add(dock);
                DockPanel.SetDock(dock, Dock.Top);
                for (int j = 0; j < 16; j++)
                {
                    if (i * 16 + j < chosenPalette.Colors.Count)
                    {
                        var box = new Viewbox()
                        {
                            Stretch = Stretch.Uniform,
                            Child =
                                new Rectangle()
                                {
                                    Width = 40,
                                    Height = 40,
                                    Fill = new SolidColorBrush(chosenPalette.Colors[i * 16 + j])
                                }
                        };
                        Grid.SetColumn(box, j);
                        dock.Children.Add(box);
                    }
                }
            }
        }

        void ReloadPreview()
        {
            if (!IsInitialized) return;
            if (openedImagePixels == null) return;
            if (convert != null) convert.Cancel();
            var palette = chosenPalette;
            if (leftSelected) palette = colPicker.GetPalette();
            if ((bool)useNoteLength.IsChecked)
                convert = new ConversionProcess(palette, openedImagePixels, openedImageWidth == 256, (bool)startOfImage.IsChecked, (int)noteSplitLength.Value);
            else
                convert = new ConversionProcess(palette, openedImagePixels, openedImageWidth == 256);
            convert.RunProcessAsync(ShowPreview);
            genImage.Source = null;
            saveMidi.IsEnabled = false;
        }

        BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    BitmapImage bitmapimage = new BitmapImage();
                    bitmapimage.BeginInit();
                    bitmapimage.StreamSource = memory;
                    bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapimage.EndInit();

                    return bitmapimage;
                }
            }
            catch { return null; }
        }

        void ShowPreview()
        {
            try
            {
                var src = convert.Image;
                Dispatcher.Invoke(() =>
                {
                    var bmp = BitmapToImageSource(src);
                    if (bmp != null)
                    {
                        genImage.Source = bmp;
                        saveMidi.IsEnabled = true;
                        ((Storyboard)genImage.GetValue(FadeInStoryboard)).Begin();
                    }
                });
            }
            catch { }
        }

        private void SaveMidi_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog save = new SaveFileDialog();
            save.Filter = "Midi files (*.mid)|*.mid";
            if (!(bool)save.ShowDialog()) return;
            try
            {
                if (convert != null)
                {
                    bool colorEvents = (bool)genColorEventsCheck.IsChecked || !leftSelected;
                    convert.WriteMidi(save.FileName, (int)ticksPerPixel.Value, (int)midiPPQ.Value, (int)startOffset.Value, colorEvents);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "An error happened");
            }
        }

        private void NoteSplitLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            ReloadPreview();
        }

        private void StartOfImage_Checked(object sender, RoutedEventArgs e)
        {
            ReloadPreview();
        }

        private void StartOfNotes_Checked(object sender, RoutedEventArgs e)
        {
            ReloadPreview();
        }

        private void UseNoteLength_Checked(object sender, RoutedEventArgs e)
        {
            ReloadPreview();
        }

        private void ResetPalette_Click(object sender, RoutedEventArgs e)
        {
            ReloadAutoPalette();
        }

        private void ClusterisePalette_Click(object sender, RoutedEventArgs e)
        {
            chosenPalette = Clusterisation.Clusterise(chosenPalette, openedImagePixels, 10);
            ReloadPalettePreview();
            ReloadPreview();
        }
    }
}
