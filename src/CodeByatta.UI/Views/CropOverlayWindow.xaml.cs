using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;
using WpfPoint = System.Windows.Point;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Key = System.Windows.Input.Key;
using MouseButtonState = System.Windows.Input.MouseButtonState;

namespace CodeByatta.UI.Views
{
    public partial class CropOverlayWindow : Window
    {
        private readonly ManagedWindow _targetWindow;
        private readonly Action<RECT> _onCropped;
        private WpfPoint _startPoint;
        private bool _isSelecting = false;
        private RECT _targetBounds;

        public CropOverlayWindow(ManagedWindow targetWindow, Action<RECT> onCropped)
        {
            InitializeComponent();
            _targetWindow = targetWindow;
            _onCropped = onCropped;

            SourceInitialized += CropOverlayWindow_SourceInitialized;
        }

        private void CropOverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (_targetWindow.GetBounds(out _targetBounds))
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                Left = _targetBounds.Left / dpi.DpiScaleX;
                Top = _targetBounds.Top / dpi.DpiScaleY;
                Width = Math.Max(200, _targetBounds.Width / dpi.DpiScaleX);
                Height = Math.Max(150, _targetBounds.Height / dpi.DpiScaleY);
            }
        }

        private void Window_MouseDown(object sender, WpfMouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isSelecting = true;
                _startPoint = e.GetPosition(OverlayCanvas);

                SelectionBox.Width = 0;
                SelectionBox.Height = 0;
                Canvas.SetLeft(SelectionBox, _startPoint.X);
                Canvas.SetTop(SelectionBox, _startPoint.Y);
                SelectionBox.Visibility = Visibility.Visible;
                DimensionBadge.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (!_isSelecting) return;

            WpfPoint currentPoint = e.GetPosition(OverlayCanvas);

            double left = Math.Min(_startPoint.X, currentPoint.X);
            double top = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _startPoint.X);
            double height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionBox, left);
            Canvas.SetTop(SelectionBox, top);
            SelectionBox.Width = width;
            SelectionBox.Height = height;

            Canvas.SetLeft(DimensionBadge, left + 4);
            Canvas.SetTop(DimensionBadge, Math.Max(0, top - 24));
            TxtDimensions.Text = $"{(int)width} × {(int)height} px";
        }

        private void Window_MouseUp(object sender, WpfMouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            double width = SelectionBox.Width;
            double height = SelectionBox.Height;
            double left = Canvas.GetLeft(SelectionBox);
            double top = Canvas.GetTop(SelectionBox);

            Close();

            if (width > 20 && height > 20)
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                RECT cropRect = new RECT(
                    (int)Math.Round(left * dpi.DpiScaleX),
                    (int)Math.Round(top * dpi.DpiScaleY),
                    (int)Math.Round((left + width) * dpi.DpiScaleX),
                    (int)Math.Round((top + height) * dpi.DpiScaleY));

                _onCropped(cropRect);
            }
        }

        private void Window_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
