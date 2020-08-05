using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
    /// Interaction logic for ZoomableImage.xaml
    /// </summary>

    public delegate void ColorClickedEventHandler(object sender, Color clicked);

    public class RoutedColorClickedEventArgs : RoutedEventArgs
    {
        public RoutedColorClickedEventArgs(Color clickedColor, RoutedEvent e) : base(e)
        {
            ClickedColor = clickedColor;
        }

        protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
        {
            ((ColorClickedEventHandler)genericHandler)(genericTarget, ClickedColor);
        }

        public Color ClickedColor { get; }
    }

    public partial class ZoomableImage : UserControl
    {
        public BitmapSource Source
        {
            get { return (BitmapSource)GetValue(SourceProperty); }
            set
            {
                SetValue(SourceProperty, value);
                //smoothZoom.From = Zoom;
                //smoothZoom.To = targetZoom;
                //smoothZoomStoryboard.Begin();
                //targetZoom = 1;
                //Offset = new Point(0, 0);
                shownImage.Source = value;
                RefreshView();
            }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(ZoomableImage), new PropertyMetadata(null));

        double targetZoom = 1;

        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(ZoomableImage), new PropertyMetadata(1.0, new PropertyChangedCallback(OnZoomChanged)));

        public Point Offset
        {
            get { return (Point)GetValue(OffsetProperty); }
            set { SetValue(OffsetProperty, value); }
        }

        public static readonly DependencyProperty OffsetProperty =
            DependencyProperty.Register("point", typeof(Point), typeof(ZoomableImage), new PropertyMetadata(new Point(0, 0)));


        public BitmapScalingMode ScalingMode
        {
            get { return (BitmapScalingMode)GetValue(ScalingModeProperty); }
            set { SetValue(ScalingModeProperty, value); }
        }

        public static readonly DependencyProperty ScalingModeProperty =
            DependencyProperty.Register("ScalingMode", typeof(BitmapScalingMode), typeof(ZoomableImage), new PropertyMetadata(BitmapScalingMode.Linear));




        public bool ClickableColors
        {
            get { return (bool)GetValue(ClickableColorsProperty); }
            set { SetValue(ClickableColorsProperty, value); }
        }

        public static readonly DependencyProperty ClickableColorsProperty =
            DependencyProperty.Register("ClickableColors", typeof(bool), typeof(ZoomableImage), new PropertyMetadata(false));



        public static readonly RoutedEvent ColorClickedEvent = EventManager.RegisterRoutedEvent(
            "Clicked", RoutingStrategy.Bubble,
            typeof(ColorClickedEventHandler), typeof(ZoomableImage));

        public event ColorClickedEventHandler ColorClicked
        {
            add { AddHandler(ColorClickedEvent, value); }
            remove { RemoveHandler(ColorClickedEvent, value); }
        }


        VelocityDrivenAnimation smoothZoom;
        Storyboard smoothZoomStoryboard;

        public ZoomableImage()
        {
            InitializeComponent();
            DataContext = this;

            smoothZoom = new VelocityDrivenAnimation();
            smoothZoom.From = 1.0;
            smoothZoom.To = 1.0;
            smoothZoom.Duration = new Duration(TimeSpan.FromSeconds(0.1));

            smoothZoomStoryboard = new Storyboard();
            smoothZoomStoryboard.Children.Add(smoothZoom);
            smoothZoomStoryboard.SlipBehavior = SlipBehavior.Grow;
            Storyboard.SetTarget(smoothZoom, this);
            Storyboard.SetTargetProperty(smoothZoom, new PropertyPath(ZoomableImage.ZoomProperty));
        }

        private static void OnZoomChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((ZoomableImage)sender).UpdateZoomOffset((double)e.OldValue, (double)e.NewValue);
            ((ZoomableImage)sender).RefreshView();
        }

        void RefreshView()
        {
            if (Source == null) return;
            double aspect = Source.Width / Source.Height;
            double containerAspect = container.ActualWidth / container.ActualHeight;
            double width, height;
            if (aspect > containerAspect)
            {
                width = container.ActualWidth;
                height = container.ActualWidth / aspect;
            }
            else
            {
                width = container.ActualHeight * aspect;
                height = container.ActualHeight;
            }
            var zoom = Zoom;
            if (zoom < 1) zoom = 1;
            double leftMargin = 0;
            double topMargin = 0;
            leftMargin += width * Offset.X;
            topMargin += height * Offset.Y;
            leftMargin *= zoom;
            topMargin *= zoom;
            shownImage.Width = width * zoom;
            shownImage.Height = height * zoom;
            leftMargin -= width * zoom / 2;
            topMargin -= height * zoom / 2;
            leftMargin += container.ActualWidth / 2;
            topMargin += container.ActualHeight / 2;
            shownImage.Margin = new Thickness(leftMargin, topMargin, 0, 0);
        }

        void UpdateZoomOffset(double oldval, double newval)
        {
            double scaleMult = newval / oldval;
            double aspect = Source.Width / Source.Height;
            double containerAspect = container.ActualWidth / container.ActualHeight;
            double width, height;
            if (aspect > containerAspect)
            {
                width = container.ActualWidth;
                height = container.ActualWidth / aspect;
            }
            else
            {
                width = container.ActualHeight * aspect;
                height = container.ActualHeight;
            }
            var pos = Mouse.GetPosition(container);
            pos = new Point(pos.X - (container.ActualWidth - width) / 2, pos.Y - (container.ActualHeight - height) / 2);
            pos = new Point(pos.X - width / 2, pos.Y - height / 2);
            pos = new Point(pos.X / width / Zoom + Offset.X, pos.Y / height / Zoom + Offset.Y);
            Offset = new Point((Offset.X - pos.X) * scaleMult + pos.X, (Offset.Y - pos.Y) * scaleMult + pos.Y);
            ClampOffset();
        }

        private void Container_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Source == null) return;
            double scaleMult = Math.Pow(2, e.Delta / 500.0);
            targetZoom *= scaleMult;

            if (targetZoom < 1)
            {
                targetZoom = 1;
                if (Zoom <= 1)
                {
                    scaleMult = scaleMult * scaleMult * scaleMult;
                    Offset = new Point(Offset.X * scaleMult, Offset.Y * scaleMult);
                    RefreshView();
                }
            }

            smoothZoom.From = Zoom;
            smoothZoom.To = targetZoom;
            smoothZoomStoryboard.Begin();

            ClampOffset();
        }

        private void Container_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshView();
        }

        bool mouseNotMoved = false;
        bool mouseIsDown = false;
        Point mouseMoveStart;
        Point offsetStart;
        private void Container_MouseDown(object sender, MouseButtonEventArgs e)
        {
            container.CaptureMouse();
            mouseIsDown = true;
            mouseNotMoved = true;
            mouseMoveStart = e.GetPosition(container);
            offsetStart = Offset;
        }

        void ClampOffset()
        {
            Offset = new Point(Offset.X > 0.5 ? 0.5 : Offset.X, Offset.Y > 0.5 ? 0.5 : Offset.Y);
            Offset = new Point(Offset.X < -0.5 ? -0.5 : Offset.X, Offset.Y < -0.5 ? -0.5 : Offset.Y);
        }

        private void Container_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseIsDown)
            {
                Point currentMousePos = e.GetPosition(container);
                Point mouseOffset = (Point)(currentMousePos - mouseMoveStart);
                if (mouseOffset.X != 0 && mouseOffset.Y != 0)
                {
                    container.Cursor = Cursors.ScrollAll;
                    mouseNotMoved = false;
                    Offset = new Point(Offset.X + mouseOffset.X / shownImage.ActualWidth, Offset.Y + mouseOffset.Y / shownImage.ActualHeight);
                    ClampOffset();

                    mouseMoveStart = currentMousePos;
                    offsetStart = Offset;

                    ClampOffset();
                    RefreshView();
                }
            }
        }

        private void Container_MouseUp(object sender, MouseButtonEventArgs e)
        {
            container.ReleaseMouseCapture();
            if (!mouseIsDown) return;
            if (mouseNotMoved)
            {
                if (Source != null && ClickableColors)
                {
                    double aspect = Source.Width / Source.Height;
                    double containerAspect = container.ActualWidth / container.ActualHeight;
                    double width, height;
                    if (aspect > containerAspect)
                    {
                        width = container.ActualWidth;
                        height = container.ActualWidth / aspect;
                    }
                    else
                    {
                        width = container.ActualHeight * aspect;
                        height = container.ActualHeight;
                    }
                    var pos = Mouse.GetPosition(container);
                    pos = new Point(pos.X - (container.ActualWidth - width) / 2, pos.Y - (container.ActualHeight - height) / 2);
                    pos = new Point(pos.X - width / 2, pos.Y - height / 2);
                    pos = new Point(pos.X / width / Zoom - Offset.X, pos.Y / height / Zoom - Offset.Y);

                    pos = new Point((pos.X + 0.5) * (Source.PixelWidth - 1), (pos.Y + 0.5) * (Source.PixelHeight - 1));

                    int x = (int)Math.Round(pos.X);
                    int y = (int)Math.Round(pos.Y);
                    if (x >= 0 && x < Source.PixelWidth &&
                        y >= 0 && y < Source.PixelHeight)
                    {
                        int stride = Source.PixelWidth * 4;
                        int size = Source.PixelHeight * stride;
                        var imagePixels = new byte[size];
                        Source.CopyPixels(imagePixels, stride, 0);
                        int pixel = y * stride + x * 4;
                        if (imagePixels[pixel + 3] != 0)
                        {
                            var c = Color.FromRgb(imagePixels[pixel + 2], imagePixels[pixel + 1], imagePixels[pixel + 0]);
                            RaiseEvent(new RoutedColorClickedEventArgs(c, ColorClickedEvent));
                        }
                    }
                }
            }
            else
            {
                container.Cursor = Cursor;
            }
            mouseIsDown = false;
        }
    }

    class VelocityDrivenAnimation : DoubleAnimationBase
    {
        public double From
        {
            get { return (double)GetValue(FromProperty); }
            set { SetValue(FromProperty, value); }
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(double), typeof(VelocityDrivenAnimation), new PropertyMetadata(0.0));


        public double To
        {
            get { return (double)GetValue(ToProperty); }
            set { SetValue(ToProperty, value); }
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(double), typeof(VelocityDrivenAnimation), new PropertyMetadata(0.0));


        VelocityDrivenAnimation parent = null;
        double velocity = 0;

        public VelocityDrivenAnimation() { }

        protected override Freezable CreateInstanceCore()
        {
            double v = velocity;
            double s = From;
            double f = To;
            var instance = new VelocityDrivenAnimation()
            {
                parent = this,
                From = From,
                To = To,
                velocity = velocity
            };
            return instance;
        }

        double easeFunc(double x, double v) => 
            (-2 + 4 * x + v * (1 + 2 * x * (1 + x * (-5 - 2 * (x - 3) * x)))) /
            (4 + 8 * (x - 1) * x);
        double easeVelFunc(double x, double v) => 
            -((x - 1) * (2 * x + v * (x - 1) * (-1 + 4 * x * (1 + (x - 1) * x)))) /
            Math.Pow(1 + 2 * (x - 1) * x, 2);

        protected override double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock animationClock)
        {
            double s = From;
            double f = To;
            double dist = f - s;
            if(dist ==0)
            {
                parent.velocity = 0;
                return s;
            }
            double v = velocity / dist;
            double x = (double)animationClock.CurrentProgress / 2 + 0.5;

            double ease = easeFunc(x, v) - easeFunc(0, v);
            double vel = easeVelFunc(x, v);

            parent.velocity = vel * dist;
            return ease * dist + s;
        }
    }
}
