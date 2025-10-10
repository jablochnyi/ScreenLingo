using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenLingo
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();

            // Окно во весь экран (включая все мониторы)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.Topmost = true;
            this.IsHitTestVisible = false;
            this.ShowInTaskbar = false;
        }

        public void ShowTranslatedLines(List<OCRResult> ocrResults, List<string> translations, int fontSize)
        {
            // Оставляем рамку области
            var regionRects = new List<UIElement>();
            foreach (var child in OverlayCanvas.Children)
            {
                if (child is System.Windows.Shapes.Rectangle rect && rect.Tag != null && rect.Tag.ToString() == "region")
                    regionRects.Add(rect);
            }
            OverlayCanvas.Children.Clear();
            foreach (var el in regionRects)
                OverlayCanvas.Children.Add(el);

            if (ocrResults.Count == 0 || translations.Count == 0)
                return;

            // 1️⃣ Сортируем строки сверху вниз (по Y)
            var pairs = ocrResults.Zip(translations, (r, t) => new { r, t })
                                  .OrderBy(p => p.r.Bounds.Y)
                                  .ToList();

            double lineSpacing = 4; // минимальный отступ между строками
            double lastBottom = double.MinValue; // низ предыдущей строки

            for (int i = 0; i < pairs.Count; i++)
            {
                var bounds = pairs[i].r.Bounds;
                string text = pairs[i].t?.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                // немного расширяем область
                var expanded = new System.Drawing.Rectangle(
                    bounds.X - 5,
                    bounds.Y - 3,
                    bounds.Width + 10,
                    bounds.Height + 6
                );

                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                    TextWrapping = TextWrapping.Wrap,
                    Width = expanded.Width * 1.05, // чуть шире оригинала
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 4,
                        ShadowDepth = 0,
                        Opacity = 0.8
                    },
                    IsHitTestVisible = false
                };

                // Измеряем высоту перевода
                tb.Measure(new System.Windows.Size(expanded.Width * 1.05, double.PositiveInfinity));
                double textHeight = tb.DesiredSize.Height;

                // 2️⃣ Корректируем Y, если пересекается с предыдущей строкой
                double top = bounds.Y - SystemParameters.VirtualScreenTop;
                if (top < lastBottom + lineSpacing)
                    top = lastBottom + lineSpacing;

                double left = bounds.X - SystemParameters.VirtualScreenLeft;
                double bottom = top + textHeight;

                // Контейнер с фоном
                var panel = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 0, 0)), // полупрозрачный фон
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Child = tb,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(panel, left);
                Canvas.SetTop(panel, top);
                OverlayCanvas.Children.Add(panel);

                lastBottom = bottom; // сохраняем низ строки
            }

            if (!this.IsVisible)
                this.Show();
        }

        public void SetTranslationsVisibility(bool visible)
        {
            // прячем/показываем все элементы кроме рамки области (у которой Tag == "region")
            foreach (UIElement child in OverlayCanvas.Children)
            {
                if (child is System.Windows.Shapes.Rectangle rect && rect.Tag != null && rect.Tag.ToString() == "region")
                    continue;
                child.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void ShowTransparentRegion(System.Drawing.Rectangle region)
        {
            // Удаляем только старую рамку, если была
            foreach (var child in OverlayCanvas.Children)
            {
                if (child is System.Windows.Shapes.Rectangle rect && rect.Tag != null && rect.Tag.ToString() == "region")
                {
                    OverlayCanvas.Children.Remove(rect);
                    break;
                }
            }

            var border = new System.Windows.Shapes.Rectangle
            {
                Width = region.Width,
                Height = region.Height,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 255, 255)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 255, 255)),
                IsHitTestVisible = false,
                Tag = "region"
            };

            Canvas.SetLeft(border, region.X - SystemParameters.VirtualScreenLeft);
            Canvas.SetTop(border, region.Y - SystemParameters.VirtualScreenTop);
            OverlayCanvas.Children.Add(border);

            if (!this.IsVisible)
                this.Show();
        }
    }
}
