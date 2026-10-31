using System;
using System.Collections.Generic;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;

namespace CodeByatta.Core.Services
{
    public enum HotkeyAction
    {
        ToggleTopmost = 1001,
        ToggleOpacity = 1002,
        ToggleClickThrough = 1003,
        TogglePip = 1004,
        ToggleDarkMode = 1005,
        ToggleCinemaFocus = 1006
    }

    public class HotkeyService : IDisposable
    {
        private IntPtr _hWnd;
        private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = new();

        public event EventHandler<HotkeyAction>? HotkeyTriggered;

        public void Initialize(IntPtr hWnd, HotkeyConfig config)
        {
            _hWnd = hWnd;
            UnregisterAll();

            Register(HotkeyAction.ToggleTopmost, config.Modifier, config.TopmostKey);
            Register(HotkeyAction.ToggleOpacity, config.Modifier, config.OpacityKey);
            Register(HotkeyAction.ToggleClickThrough, config.Modifier, config.ClickThroughKey);
            Register(HotkeyAction.TogglePip, config.Modifier, config.PipKey);
            Register(HotkeyAction.ToggleDarkMode, config.Modifier, config.DarkModeKey);
            Register(HotkeyAction.ToggleCinemaFocus, config.Modifier, config.CinemaFocusKey);
        }

        public void Initialize(IntPtr hWnd, AppConfig config)
        {
            _hWnd = hWnd;
            UnregisterAll();

            if (config.ShowSetTopButton)
                Register(HotkeyAction.ToggleTopmost, config.Hotkeys.Modifier, config.Hotkeys.TopmostKey);

            if (config.ShowOpacityButton)
                Register(HotkeyAction.ToggleOpacity, config.Hotkeys.Modifier, config.Hotkeys.OpacityKey);

            if (config.ShowClickThroughButton)
                Register(HotkeyAction.ToggleClickThrough, config.Hotkeys.Modifier, config.Hotkeys.ClickThroughKey);

            if (config.ShowPipButton)
                Register(HotkeyAction.TogglePip, config.Hotkeys.Modifier, config.Hotkeys.PipKey);

            if (config.ShowDarkModeButton)
                Register(HotkeyAction.ToggleDarkMode, config.Hotkeys.Modifier, config.Hotkeys.DarkModeKey);

            if (config.ShowCinemaFocusButton)
                Register(HotkeyAction.ToggleCinemaFocus, config.Hotkeys.Modifier, config.Hotkeys.CinemaFocusKey);
        }

        private void Register(HotkeyAction action, uint modifier, uint key)
        {
            if (_hWnd == IntPtr.Zero || key == 0) return;

            int id = (int)action;
            uint flags = modifier | User32.MOD_NOREPEAT;

            if (User32.RegisterHotKey(_hWnd, id, flags, key))
            {
                _registeredHotkeys[id] = action;
            }
        }

        public bool ProcessMessage(int msg, IntPtr wParam)
        {
            if (msg == User32.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_registeredHotkeys.TryGetValue(id, out var action))
                {
                    HotkeyTriggered?.Invoke(this, action);
                    return true;
                }
            }
            return false;
        }

        public void UnregisterAll()
        {
            if (_hWnd == IntPtr.Zero) return;

            foreach (var id in _registeredHotkeys.Keys)
            {
                User32.UnregisterHotKey(_hWnd, id);
            }
            _registeredHotkeys.Clear();
        }

        public void Dispose()
        {
            UnregisterAll();
        }
    }
}
