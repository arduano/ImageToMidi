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
    public partial class ZoomableImage : UserControl
    {
        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set
            {
                SetValue(SourceProperty, value);
                Zoom = 1;
                Offset = new Point(0, 0);
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

        VelocityDrivenAnimation smoothZoom;
        Storyboard smoothZoomStoryboard;

        public ZoomableImage()
        {
            InitializeComponent();
            
            smoothZoom = new VelocityDrivenAnimation();
            smoothZoom.From = 1.0;
            smoothZoom.To = 1.0;
            smoothZoom.Duration = new Duration(TimeSpan.FromSeconds(0.15));

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
                width = container.ActualHeight / aspect;
                height = container.ActualHeight;
            }
            double leftMargin = 0;
            double topMargin = 0;
            leftMargin += width * Offset.X;
            topMargin += height * Offset.Y;
            leftMargin *= Zoom;
            topMargin *= Zoom;
            shownImage.Width = width * Zoom;
            shownImage.Height = height * Zoom;
            leftMargin -= width * Zoom / 2;
            topMargin -= height * Zoom / 2;
            leftMargin += container.ActualWidth / 2;
            topMargin += container.ActualHeight / 2;
            shownImage.Margin = new Thickness(leftMargin, topMargin, 0, 0);
        }

        void UpdateZoomOffset(double oldval, double newval){
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
                width = container.ActualHeight / aspect;
                height = container.ActualHeight;
            }
            double left = 0;
            double top = 0;
            left += width * Offset.X;
            top += height * Offset.Y;
            left *= Zoom;
            top *= Zoom;
            var pos = Mouse.GetPosition(container);
            pos = new Point(pos.X - (container.ActualWidth - width) / 2, pos.Y - (container.ActualHeight - height) / 2);
            pos = new Point(pos.X - width / 2, pos.Y - height / 2);
            pos = new Point(pos.X / width / Zoom + Offset.X, pos.Y / height / Zoom + Offset.Y);
            Offset = new Point((Offset.X - pos.X) * scaleMult + pos.X, (Offset.Y - pos.Y) * scaleMult + pos.Y);
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
                container.Cursor = Cursors.ScrollAll;

                mouseNotMoved = false;

                Point currentMousePos = e.GetPosition(container);

                Point mouseOffset = (Point)(currentMousePos - mouseMoveStart);
                Offset = new Point(Offset.X + mouseOffset.X / shownImage.ActualWidth, Offset.Y + mouseOffset.Y / shownImage.ActualHeight);
                ClampOffset();

                mouseMoveStart = currentMousePos;
                offsetStart = Offset;

                ClampOffset();
                RefreshView();
            }
        }

        private void Container_MouseUp(object sender, MouseButtonEventArgs e)
        {
            container.ReleaseMouseCapture();
            if (!mouseIsDown) return;
            if (mouseNotMoved)
            {

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

        double timeOffset = 0;

        protected override Freezable CreateInstanceCore()
        {
            if (velocity > 0 ^ To > From) velocity = 0;
            double v = velocity;
            double s = From;
            double f = To;
            if(v < 0)
            {

            }
            if(f < s)
            {
                f = From;
                s = To;
                v = -v;
            }
            double startPos = 0.5 - Math.Sqrt((Math.Sqrt((f - s) * (f - s + 4 * v)) - v + s - f) / v) / 2;
            if (double.IsNaN(startPos) || double.IsInfinity(startPos))
                startPos = 0;
            var instance = new VelocityDrivenAnimation()
            {
                parent = this,
                From = From,
                To = To,
                timeOffset = startPos
            };
            return instance;
        }

        protected override double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock animationClock)
        {
            double t = timeOffset + (double)animationClock.CurrentProgress * (1 - timeOffset);
            double s = From;
            double f = To;
            double value = Math.Pow(t, 2) / (Math.Pow(t, 2) + Math.Pow(1 - t, 2)) * (f - s) + s;
            parent.velocity = -(2 * (f - s) * (t - 1) * t) / Math.Pow(1 + 2 * (t - 1) * t, 2);
            return value;
            return 1;
        }
    }
}
