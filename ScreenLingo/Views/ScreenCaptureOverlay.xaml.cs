using System;
using System.Drawing; // для Bitmap
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace ScreenLingo
{
    public partial class ScreenCaptureOverlay : Window
    {
        private Point _startPoint;
        private System.Drawing.Rectangle _selectionRect;
        private System.Windows.Shapes.Rectangle _selectionVisual;

        public Bitmap CapturedBitmap { get; private set; }
        public System.Drawing.Rectangle SelectionRect { get; private set; }

        public ScreenCaptureOverlay()
        {
            InitializeComponent();

            _selectionVisual = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 255)),
                Visibility = Visibility.Collapsed
            };

            SelectionCanvas.Children.Add(_selectionVisual);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(this);
                Canvas.SetLeft(_selectionVisual, _startPoint.X);
                Canvas.SetTop(_selectionVisual, _startPoint.Y);
                _selectionVisual.Width = 0;
                _selectionVisual.Height = 0;
                _selectionVisual.Visibility = Visibility.Visible;
            }
        }
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(this);

                double x = Math.Min(pos.X, _startPoint.X);
                double y = Math.Min(pos.Y, _startPoint.Y);
                double w = Math.Abs(pos.X - _startPoint.X);
                double h = Math.Abs(pos.Y - _startPoint.Y);

                Canvas.SetLeft(_selectionVisual, x);
                Canvas.SetTop(_selectionVisual, y);
                _selectionVisual.Width = w;
                _selectionVisual.Height = h;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                Point endPoint = e.GetPosition(this);

                double x = Math.Min(_startPoint.X, endPoint.X);
                double y = Math.Min(_startPoint.Y, endPoint.Y);
                double width = Math.Abs(endPoint.X - _startPoint.X);
                double height = Math.Abs(endPoint.Y - _startPoint.Y);

                SelectionRect = new System.Drawing.Rectangle(
                    (int)(x * (SystemParameters.PrimaryScreenWidth / this.ActualWidth)),
                    (int)(y * (SystemParameters.PrimaryScreenHeight / this.ActualHeight)),
                    (int)(width * (SystemParameters.PrimaryScreenWidth / this.ActualWidth)),
                    (int)(height * (SystemParameters.PrimaryScreenHeight / this.ActualHeight))
                );

                if (SelectionRect.Width > 0 && SelectionRect.Height > 0)
                {
                    using (var bmp = new Bitmap(SelectionRect.Width, SelectionRect.Height))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(SelectionRect.Location, System.Drawing.Point.Empty, SelectionRect.Size);
                        CapturedBitmap = new Bitmap(bmp);
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
        }
    }
}
