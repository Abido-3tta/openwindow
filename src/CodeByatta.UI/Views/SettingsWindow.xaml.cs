using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using Microsoft.Win32;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfButton = System.Windows.Controls.Button;
using Forms = System.Windows.Forms;

namespace CodeByatta.UI.Views
{
    public class WindowProfileItem
    {
        public string ProcessName { get; set; } = string.Empty;
        public bool IsTopmost { get; set; }
        public byte OpacityLevel { get; set; }
        public bool IsClickThrough { get; set; }
        public double ArrowCenterRatio { get; set; }

        public Visibility TopmostBadgeVisibility => IsTopmost ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ClickThroughBadgeVisibility => IsClickThrough ? Visibility.Visible : Visibility.Collapsed;
        public string OpacitySummary => $"🪟 {(int)Math.Round((OpacityLevel / 255.0) * 100)}%";
        public string PositionSummary => $"Anchor: {(int)Math.Round(ArrowCenterRatio * 100)}%";
    }

    public partial class SettingsWindow : Window
    {
        private readonly AppConfig _config;
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "OpenWindow";

        public SettingsWindow(AppConfig config)
        {
            InitializeComponent();
            _config = config;

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch { }

            LoadSettings();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0, User32.SWP_NOMOVE | User32.SWP_NOSIZE);
                User32.BringWindowToTop(hwnd);
                User32.SetForegroundWindow(hwnd);
            }
            catch { }
        }

        private void LoadSettings()
        {
            // 0. General Startup Setting
            try
            {
                ToggleStartWithWindows.IsOn = _config.StartWithWindows || IsStartWithWindowsEnabled();
            }
            catch
            {
                ToggleStartWithWindows.IsOn = _config.StartWithWindows;
            }

            // 1. Advanced Settings
            ToggleHighlightBorder.IsOn = _config.HighlightTopmostWithBorder;
            ToggleDisableMaximized.IsOn = _config.DisableTopmostOnMaximized;
            SliderBorderWidth.Value = _config.TopmostBorderWidth;
            TxtBorderWidth.Text = $"{_config.TopmostBorderWidth:0.00} px";
            ToggleHotkeyNotification.IsOn = _config.ShowNotificationOnHotkey;
            ToggleHideOriginalCrop.IsOn = _config.HideOriginalWindowOnCrop;
            ToggleDisableOpacityHover.IsOn = _config.DisableOpacityOnMouseOver;

            UpdateBorderColorDisplay(_config.TopmostBorderColor);

            // 2. Toolbar Settings
            ToggleToolSetTop.IsOn = _config.ShowSetTopButton;
            ToggleToolAnchor.IsOn = _config.ShowAnchorButton;
            ToggleToolPip.IsOn = _config.ShowPipButton;
            ToggleToolCrop.IsOn = _config.ShowCropButton;
            ToggleToolOpacity.IsOn = _config.ShowOpacityButton;
            ToggleToolHoverOpacity.IsOn = _config.ShowHoverOpacityButton;
            ToggleToolClickThrough.IsOn = _config.ShowClickThroughButton;
            ToggleToolCinemaFocus.IsOn = _config.ShowCinemaFocusButton;
            ToggleToolDark.IsOn = _config.ShowDarkModeButton;
            ToggleToolGlass.IsOn = _config.ShowGlassButton;
            TogglePipSnapping.IsOn = _config.EnablePipCornerSnapping;
            TogglePipWheelZoom.IsOn = _config.EnablePipWheelResize;
            SliderDimLevel.Value = (int)Math.Round(_config.CinemaFocusDimLevel * 100);
            TxtDimLevel.Text = $"{(int)Math.Round(_config.CinemaFocusDimLevel * 100)}%";
            SliderHoverTime.Value = _config.HoverTimeMs;
            TxtHoverTime.Text = $"{_config.HoverTimeMs}ms";
            ToggleAutoHide.IsOn = _config.AutoHideToolbar;
            ToggleRememberPosition.IsOn = _config.AutoRememberWindowPreferences;
            ToggleClickToExpandToolbar.IsOn = _config.RequireClickToExpandToolbar;
            ToggleIntegrateMinimize.IsOn = _config.IntegrateWithMinimizeButton;

            UpdateArrowColorDisplay(_config.ArrowColor);

            // 3. Hotkeys Settings
            for (int i = 0; i < ComboWheelModifier.Items.Count; i++)
            {
                if ((ComboWheelModifier.Items[i] as ComboBoxItem)?.Content.ToString() == _config.MouseWheelOpacityModifier)
                {
                    ComboWheelModifier.SelectedIndex = i;
                    break;
                }
            }

            string sensString = $"{_config.MouseWheelSensitivity} Units";
            for (int i = 0; i < ComboWheelSensitivity.Items.Count; i++)
            {
                if ((ComboWheelSensitivity.Items[i] as ComboBoxItem)?.Content.ToString() == sensString)
                {
                    ComboWheelSensitivity.SelectedIndex = i;
                    break;
                }
            }

            // 4. Exclusions Settings
            ListExclusions.Items.Clear();
            foreach (var proc in _config.ExcludedProcesses)
            {
                ListExclusions.Items.Add(proc);
            }
            RadioBlacklist.IsChecked = !_config.IsWhitelistMode;
            RadioWhitelist.IsChecked = _config.IsWhitelistMode;
            ToggleGameMode.IsOn = _config.DisableOnGameMode;

            // 5. Window Profiles & Memory Settings
            ToggleRememberProfiles.IsOn = _config.AutoRememberWindowPreferences;
            RefreshWindowProfilesList();

            // 6. About & Updates
            ToggleAutoUpdates.IsOn = _config.AutoCheckForUpdates;
        }

        private void RefreshWindowProfilesList()
        {
            var items = new List<WindowProfileItem>();
            foreach (var kvp in _config.WindowSettings)
            {
                items.Add(new WindowProfileItem
                {
                    ProcessName = kvp.Key,
                    IsTopmost = kvp.Value.IsTopmost,
                    OpacityLevel = kvp.Value.OpacityLevel,
                    IsClickThrough = kvp.Value.IsClickThrough,
                    ArrowCenterRatio = kvp.Value.HaveArrowCenterRatio ? kvp.Value.ArrowCenterRatio : 0.5
                });
            }
            ListWindowProfiles.ItemsSource = items;
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelAdvanced == null || PanelToolbar == null || PanelHotkeys == null || 
                PanelExclusions == null || PanelWindowProfiles == null || PanelAbout == null) return;

            PanelAdvanced.Visibility = Visibility.Collapsed;
            PanelToolbar.Visibility = Visibility.Collapsed;
            PanelHotkeys.Visibility = Visibility.Collapsed;
            PanelExclusions.Visibility = Visibility.Collapsed;
            PanelWindowProfiles.Visibility = Visibility.Collapsed;
            PanelAbout.Visibility = Visibility.Collapsed;

            var selectedItem = NavList.SelectedItem as ListBoxItem;
            string tag = selectedItem?.Tag?.ToString() ?? "Advanced";

            switch (tag)
            {
                case "Advanced":
                    PanelAdvanced.Visibility = Visibility.Visible;
                    break;
                case "Toolbar":
                    PanelToolbar.Visibility = Visibility.Visible;
                    break;
                case "Hotkeys":
                    PanelHotkeys.Visibility = Visibility.Visible;
                    break;
                case "Exclusions":
                    PanelExclusions.Visibility = Visibility.Visible;
                    break;
                case "WindowProfiles":
                    PanelWindowProfiles.Visibility = Visibility.Visible;
                    break;
                case "About":
                    PanelAbout.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void RadioPreviewState_Changed(object sender, RoutedEventArgs e)
        {
            if (PreviewExpandedPill == null || PreviewCollapsedAnchor == null) return;
            if (RadioPreviewExpanded?.IsChecked == true)
            {
                PreviewExpandedPill.Visibility = Visibility.Visible;
                PreviewCollapsedAnchor.Visibility = Visibility.Collapsed;
            }
            else
            {
                PreviewExpandedPill.Visibility = Visibility.Collapsed;
                PreviewCollapsedAnchor.Visibility = Visibility.Visible;
            }
        }

        #region Color Picking and Styling

        private void BorderColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PickBorderColor();
        }

        private void BtnPickBorderColor_Click(object sender, RoutedEventArgs e)
        {
            PickBorderColor();
        }

        private void BtnPresetBorderColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string hex)
            {
                _config.TopmostBorderColor = hex;
                UpdateBorderColorDisplay(hex);
            }
        }

        private void BtnResetBorderColor_Click(object sender, RoutedEventArgs e)
        {
            _config.TopmostBorderColor = "#FF0000";
            UpdateBorderColorDisplay("#FF0000");
        }

        private void PickBorderColor()
        {
            using var dlg = new Forms.ColorDialog
            {
                FullOpen = true,
                AnyColor = true
            };

            try
            {
                var cur = (MediaColor)MediaColorConverter.ConvertFromString(_config.TopmostBorderColor);
                dlg.Color = System.Drawing.Color.FromArgb(cur.A, cur.R, cur.G, cur.B);
            }
            catch { }

            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                _config.TopmostBorderColor = hex;
                UpdateBorderColorDisplay(hex);
            }
        }

        private void UpdateBorderColorDisplay(string hex)
        {
            try
            {
                BorderColorPreview.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            }
            catch
            {
                BorderColorPreview.Background = MediaBrushes.Red;
            }
        }

        private void ArrowColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PickArrowColor();
        }

        private void BtnPickArrowColor_Click(object sender, RoutedEventArgs e)
        {
            PickArrowColor();
        }

        private void BtnPresetArrowColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string hex)
            {
                _config.ArrowColor = hex;
                UpdateArrowColorDisplay(hex);
            }
        }

        private void BtnResetArrowColor_Click(object sender, RoutedEventArgs e)
        {
            _config.ArrowColor = "#30D158";
            UpdateArrowColorDisplay("#30D158");
        }

        private void PickArrowColor()
        {
            using var dlg = new Forms.ColorDialog
            {
                FullOpen = true,
                AnyColor = true
            };

            try
            {
                var cur = (MediaColor)MediaColorConverter.ConvertFromString(_config.ArrowColor);
                dlg.Color = System.Drawing.Color.FromArgb(cur.A, cur.R, cur.G, cur.B);
            }
            catch { }

            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                _config.ArrowColor = hex;
                UpdateArrowColorDisplay(hex);
            }
        }

        private void UpdateArrowColorDisplay(string hex)
        {
            try
            {
                ArrowColorPreview.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            }
            catch
            {
                ArrowColorPreview.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#30D158")!;
            }
        }

        #endregion

        #region Sliders & Numeric Controls

        private void SliderBorderWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBorderWidth != null)
            {
                TxtBorderWidth.Text = $"{e.NewValue:0.00} px";
            }
        }

        private void SliderHoverTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtHoverTime != null)
            {
                TxtHoverTime.Text = $"{(int)e.NewValue}ms";
            }
        }

        private void SliderDimLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDimLevel != null)
            {
                TxtDimLevel.Text = $"{(int)Math.Round(e.NewValue)}%";
            }
        }

        private void BtnResetBorderWidth_Click(object sender, RoutedEventArgs e)
        {
            SliderBorderWidth.Value = 3.07;
        }

        #endregion

        #region Exclusions & Updates

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdates.IsEnabled = false;
            TxtUpdateStatus.Text = "Checking for updates...";

            try
            {
                var updateService = new UpdateService();
                var info = await updateService.CheckForUpdatesAsync(new Version(1, 1, 0));

                if (info != null && info.IsNewer)
                {
                    TxtUpdateStatus.Text = $"Update {info.TagName} is available!";
                    var dlg = new UpdateDialog(info, updateService, _config);
                    dlg.Owner = this;
                    dlg.ShowDialog();
                }
                else if (info != null)
                {
                    TxtUpdateStatus.Text = "✓ Running latest version.";
                    Forms.MessageBox.Show(
                        $"OpenWindow is completely up-to-date!\nVersion: v{info.CurrentVersion} (Production Release)\nAll Pro features unlocked.",
                        "OpenWindow Update",
                        Forms.MessageBoxButtons.OK,
                        Forms.MessageBoxIcon.Information);
                }
                else
                {
                    TxtUpdateStatus.Text = "Unable to connect to GitHub.";
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Text = "Update check failed.";
                Forms.MessageBox.Show(
                    $"Failed to check for updates: {ex.Message}",
                    "Update Check",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Warning);
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }

        private void BtnAddExclusion_Click(object sender, RoutedEventArgs e)
        {
            string newProc = TxtNewExclusion.Text.Trim();
            if (!string.IsNullOrEmpty(newProc) && !ListExclusions.Items.Contains(newProc))
            {
                ListExclusions.Items.Add(newProc);
                TxtNewExclusion.Clear();
            }
        }

        private void BtnRemoveExclusion_Click(object sender, RoutedEventArgs e)
        {
            if (ListExclusions.SelectedIndex >= 0)
            {
                ListExclusions.Items.RemoveAt(ListExclusions.SelectedIndex);
            }
        }

        #endregion

        #region Window Profiles Management

        private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            string proc = TxtNewProfileProcess.Text.Trim();
            if (!string.IsNullOrEmpty(proc))
            {
                if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    proc = proc.Substring(0, proc.Length - 4);
                }
                var pref = _config.GetOrCreateWindowPreference(proc);
                pref.LastModified = DateTime.UtcNow;
                _config.Save();
                TxtNewProfileProcess.Clear();
                RefreshWindowProfilesList();
            }
        }

        private void BtnRemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ListWindowProfiles.SelectedItem is WindowProfileItem item)
            {
                _config.WindowSettings.Remove(item.ProcessName);
                _config.Save();
                RefreshWindowProfilesList();
            }
        }

        private void BtnClearProfiles_Click(object sender, RoutedEventArgs e)
        {
            var res = Forms.MessageBox.Show(
                "Are you sure you want to remove all saved window customizations?",
                "Clear Saved Windows",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Warning);

            if (res == Forms.DialogResult.Yes)
            {
                _config.WindowSettings.Clear();
                _config.Save();
                RefreshWindowProfilesList();
            }
        }

        #endregion

        #region Defaults & Registry Startup

        private void BtnRestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var res = Forms.MessageBox.Show(
                "Are you sure you want to restore all settings to default values?",
                "Restore Defaults",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Question);

            if (res != Forms.DialogResult.Yes) return;

            var defaults = new AppConfig();
            
            _config.HighlightTopmostWithBorder = defaults.HighlightTopmostWithBorder;
            _config.DisableTopmostOnMaximized = defaults.DisableTopmostOnMaximized;
            _config.TopmostBorderColor = defaults.TopmostBorderColor;
            _config.TopmostBorderWidth = defaults.TopmostBorderWidth;
            _config.ShowNotificationOnHotkey = defaults.ShowNotificationOnHotkey;
            _config.HideOriginalWindowOnCrop = defaults.HideOriginalWindowOnCrop;
            _config.DisableOpacityOnMouseOver = defaults.DisableOpacityOnMouseOver;

            _config.ShowSetTopButton = defaults.ShowSetTopButton;
            _config.ShowAnchorButton = defaults.ShowAnchorButton;
            _config.ShowPipButton = defaults.ShowPipButton;
            _config.ShowCropButton = defaults.ShowCropButton;
            _config.ShowDarkModeButton = defaults.ShowDarkModeButton;
            _config.ShowOpacityButton = defaults.ShowOpacityButton;
            _config.ShowGlassButton = defaults.ShowGlassButton;
            _config.HoverTimeMs = defaults.HoverTimeMs;
            _config.AutoHideToolbar = defaults.AutoHideToolbar;
            _config.AutoRememberWindowPreferences = defaults.AutoRememberWindowPreferences;
            _config.RequireClickToExpandToolbar = defaults.RequireClickToExpandToolbar;
            _config.IntegrateWithMinimizeButton = defaults.IntegrateWithMinimizeButton;
            _config.ArrowColor = defaults.ArrowColor;

            _config.MouseWheelOpacityModifier = defaults.MouseWheelOpacityModifier;
            _config.MouseWheelSensitivity = defaults.MouseWheelSensitivity;

            _config.ExcludedProcesses.Clear();
            _config.ExcludedProcesses.AddRange(defaults.ExcludedProcesses);
            _config.IsWhitelistMode = defaults.IsWhitelistMode;
            _config.DisableOnGameMode = defaults.DisableOnGameMode;

            LoadSettings();
        }

        private void SetStartWithWindows(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key != null)
                {
                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName
                            ?? Path.Combine(AppContext.BaseDirectory, "CodeByatta.UI.exe");
                        key.SetValue(AppRegistryName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
            catch { }
        }

        private bool IsStartWithWindowsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppRegistryName) != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // 0. Startup
            _config.StartWithWindows = ToggleStartWithWindows.IsOn;
            SetStartWithWindows(ToggleStartWithWindows.IsOn);

            // 1. Save Advanced
            _config.HighlightTopmostWithBorder = ToggleHighlightBorder.IsOn;
            _config.DisableTopmostOnMaximized = ToggleDisableMaximized.IsOn;
            _config.TopmostBorderWidth = SliderBorderWidth.Value;
            if (BorderColorPreview.Background is SolidColorBrush scb)
            {
                _config.TopmostBorderColor = $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
            }
            if (ArrowColorPreview.Background is SolidColorBrush acb)
            {
                _config.ArrowColor = $"#{acb.Color.R:X2}{acb.Color.G:X2}{acb.Color.B:X2}";
            }
            _config.ShowNotificationOnHotkey = ToggleHotkeyNotification.IsOn;
            _config.HideOriginalWindowOnCrop = ToggleHideOriginalCrop.IsOn;
            _config.DisableOpacityOnMouseOver = ToggleDisableOpacityHover.IsOn;

            // 2. Save Toolbar
            _config.ShowSetTopButton = ToggleToolSetTop.IsOn;
            _config.ShowAnchorButton = ToggleToolAnchor.IsOn;
            _config.ShowPipButton = ToggleToolPip.IsOn;
            _config.ShowCropButton = ToggleToolCrop.IsOn;
            _config.ShowOpacityButton = ToggleToolOpacity.IsOn;
            _config.ShowHoverOpacityButton = ToggleToolHoverOpacity.IsOn;
            _config.ShowClickThroughButton = ToggleToolClickThrough.IsOn;
            _config.ShowCinemaFocusButton = ToggleToolCinemaFocus.IsOn;
            _config.ShowDarkModeButton = ToggleToolDark.IsOn;
            _config.ShowGlassButton = ToggleToolGlass.IsOn;
            _config.EnablePipCornerSnapping = TogglePipSnapping.IsOn;
            _config.EnablePipWheelResize = TogglePipWheelZoom.IsOn;
            _config.CinemaFocusDimLevel = SliderDimLevel.Value / 100.0;
            _config.HoverTimeMs = (int)SliderHoverTime.Value;
            _config.AutoHideToolbar = ToggleAutoHide.IsOn;
            _config.AutoRememberWindowPreferences = ToggleRememberPosition.IsOn || ToggleRememberProfiles.IsOn;
            _config.RequireClickToExpandToolbar = ToggleClickToExpandToolbar.IsOn;
            _config.IntegrateWithMinimizeButton = ToggleIntegrateMinimize.IsOn;

            // 3. Save Hotkeys
            _config.MouseWheelOpacityModifier = (ComboWheelModifier.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Alt";
            string sens = (ComboWheelSensitivity.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "10 Units";
            _config.MouseWheelSensitivity = int.TryParse(sens.Replace(" Units", ""), out int val) ? val : 10;

            // 4. Save Exclusions
            _config.ExcludedProcesses.Clear();
            foreach (var item in ListExclusions.Items)
            {
                _config.ExcludedProcesses.Add(item.ToString()!);
            }
            _config.IsWhitelistMode = RadioWhitelist.IsChecked ?? false;
            _config.DisableOnGameMode = ToggleGameMode.IsOn;

            // 5. Save About & Updates
            _config.AutoCheckForUpdates = ToggleAutoUpdates.IsOn;

            _config.Save();
            Close();
        }
    }
}
