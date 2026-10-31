using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CodeByatta.Core.Interop;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace CodeByatta.UI.Views
{
    public partial class HotkeyNotificationHudWindow : Window
    {
        private static HotkeyNotificationHudWindow? _instance;
        private readonly DispatcherTimer _hideTimer;
        private Storyboard? _fadeInAnim;
        private Storyboard? _fadeOutAnim;

        public static void ShowHud(string title, string status, string iconKind = "pin", bool? isPositive = true)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null || !User32.IsWindow(new WindowInteropHelper(_instance).Handle))
                {
                    _instance = new HotkeyNotificationHudWindow();
                }
                _instance.Display(title, status, iconKind, isPositive);
            });
        }

        public HotkeyNotificationHudWindow()
        {
            InitializeComponent();

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1300)
            };
            _hideTimer.Tick += HideTimer_Tick;

            SourceInitialized += HotkeyNotificationHudWindow_SourceInitialized;
            Loaded += (s, e) =>
            {
                _fadeInAnim = (Storyboard)Resources["FadeInAnim"];
                _fadeOutAnim = (Storyboard)Resources["FadeOutAnim"];
                if (_fadeOutAnim != null)
                {
                    _fadeOutAnim.Completed += (s2, e2) => Hide();
                }
            };
        }

        private void HotkeyNotificationHudWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = (long)exStyle | User32.WS_EX_TRANSPARENT | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
        }

        public void Display(string title, string status, string iconKind, bool? isPositive)
        {
            _hideTimer.Stop();

            TxtActionName.Text = title;
            TxtStatus.Text = status;

            // Set badge colors according to state
            if (isPositive == true)
            {
                StatusBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0xEE, 0x30, 0xD1, 0x58)); // Green
                TxtStatus.Foreground = MediaBrushes.White;
            }
            else if (isPositive == false)
            {
                StatusBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0xEE, 0xFF, 0x45, 0x3A)); // Red
                TxtStatus.Foreground = MediaBrushes.White;
            }
            else
            {
                StatusBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0xEE, 0x0A, 0x84, 0xFF)); // Blue/Accent
                TxtStatus.Foreground = MediaBrushes.White;
            }

            // Set dynamic icon vector
            switch (iconKind.ToLowerInvariant())
            {
                case "dark":
                    PathIcon.Data = Geometry.Parse("M10,0 A8,8 0 1,1 0,10 A6,6 0 0,0 10,0 Z");
                    IconBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0x33, 0xFF, 0xD6, 0x0A));
                    break;
                case "opacity":
                    PathIcon.Data = Geometry.Parse("M0,4 A2,2 0 0,1 2,2 L10,2 A2,2 0 0,1 12,4 L12,12 A2,2 0 0,1 10,14 L2,14 A2,2 0 0,1 0,12 Z M4,0 A2,2 0 0,1 6,0 L14,0 A2,2 0 0,1 16,2 L16,10 A2,2 0 0,1 14,12");
                    IconBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0x33, 0x0A, 0x84, 0xFF));
                    break;
                case "clickthrough":
                    PathIcon.Data = Geometry.Parse("M0,0 L0,14 L4,10 L7,16 L9,15 L6,9 L11,9 Z");
                    IconBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0x33, 0x30, 0xD1, 0x58));
                    break;
                case "pip":
                case "crop":
                    PathIcon.Data = Geometry.Parse("M2,0 L14,0 A2,2 0 0,1 16,2 L16,10 A2,2 0 0,1 14,12 L2,12 A2,2 0 0,1 0,10 L0,2 A2,2 0 0,1 2,0 Z M7,5 L14,5 L14,10 L7,10 Z");
                    IconBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0x33, 0xBF, 0x5A, 0xF2));
                    break;
                default: // pin
                    PathIcon.Data = Geometry.Parse("M9,2 L7,4 L7,8 L4,11 L4,12 L8,12 L8,16 L9,17 L10,16 L10,12 L14,12 L14,11 L11,8 L11,4 L9,2 Z");
                    IconBadge.Background = new SolidColorBrush(MediaColor.FromArgb(0x33, 0xFF, 0x45, 0x3A));
                    break;
            }

            // Center horizontally at top of primary screen (or active screen)
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            UpdateLayout();
            Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

            Left = (screenWidth - ActualWidth) / 2.0;
            if (double.IsNaN(Left) || Left <= 0)
            {
                Left = (screenWidth - 260) / 2.0;
            }
            Top = 45; // 45px below top screen bezel

            Show();
            _fadeInAnim?.Begin(this);
            _hideTimer.Start();
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            _fadeOutAnim?.Begin(this);
        }
    }
}
