<div align="center">

# ⚡ OpenWindow

**Modern, High-Performance Native Multitasking Suite for Windows 10 & 11**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64%20%7C%20x86)-0078D6.svg)](https://www.microsoft.com/windows)
[![Release](https://img.shields.io/badge/Release-v1.1.0-brightgreen.svg)](https://github.com/codebyatta/openwindow/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF%20(Self--Contained)-512BD4.svg)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/codebyatta/openwindow)
[![Author](https://img.shields.io/badge/Author-CodeByatta-FF79C6.svg)](https://github.com/codebyatta)

*A clean-room, 100% free, unlocked native Windows multitasking utility with zero licensing fees, zero telemetry, and ultra-low CPU overhead.*

---

</div>

## 📖 Overview

**OpenWindow** is an advanced window management tool designed for power users, developers, financial traders, and multitaskers. Built on top of **Microsoft .NET 8.0** and native Windows APIs (Win32, Desktop Window Manager, and the Windows Magnification Engine), it delivers smooth, hardware-accelerated window manipulations without compromising system performance.

Available both as a **standalone zero-dependency native desktop application** (no .NET runtime installation required) and as an open, extensible C# architecture.

---

## 🌟 Key Features

### 📌 1. Always-on-Top (Pin Window) — `Alt + Z`
- Pins any foreground window on top of all other windows.
- Features a **3-second active watchdog** that prevents rogue applications or full-screen overlays from stealing topmost z-order.
- Outlines the pinned window with an interactive, configurable accent border (color and width customizable in Settings).
- **Double-Click Anchor Pin:** Direct double-clicking on the collapsed toolbar anchor (`⚡ OpenWindow ▼`) instantly toggles Always-on-Top in 0.1s without expanding the full toolbar.

### 🔲 2. Live 60 FPS Picture-in-Picture (PiP) — `Alt + Q`
- Hardware-accelerated live streaming preview powered directly by the **Desktop Window Manager (DWM)** thumbnail engine.
- **Ghost Mode Technology:** When shrinking an app to PiP, OpenWindow makes the original window invisible to human eyes (`WS_EX_LAYERED | WS_EX_TRANSPARENT`, Alpha = 1) without using `SW_HIDE`.
  - **Zero Tab Throttling:** Chrome, Edge, and Firefox never pause timers, WebSockets, or audio.
  - **Zero Automation Conflicts:** External automation systems (Selenium, Playwright, RPA) continue interacting with the window unimpeded.
- **Interactive Mouse Control:** Click, scroll, and drag inside the PiP viewport; clicks are translated into target client coordinates in real-time.

### ✂️ 3. Sub-Region Cropping
- Crop any sub-rectangle of a window (video player, dashboard chart, terminal pane) to stream in a dedicated PiP view.

### 🪟 4. Smart Opacity & Hover Auto-Disable — `Alt + A`
- Cycle transparency instantly (`100% -> 80% -> 60% -> 40% -> 100%`) or fine-tune using mouse wheel scrolling while holding `Alt`.
- **Global Inactive & Background Hover Reveal:** Automatically restores 100% opacity whenever the cursor enters the body of ANY transparent window—even if that window is inactive, unfocused, or in the background (**zero click required!**). As soon as the cursor leaves the window, its configured transparency level is smoothly restored.
- **Smart Toolbar Exemption:** If the cursor is positioned directly over the floating titlebar toolbar, the window remains transparent, allowing you to freely adjust opacity sliders or buttons without visual jump.
- **Toolbar Quick-Toggle:** Dedicated eye icon button directly on the toolbar to toggle hover reveal on/off in 1 click.

### 🖱️ 5. Smart Click-Through Mode with Titlebar Waking — `Alt + C`
- Makes any transparent window completely click-through (`WS_EX_TRANSPARENT`). Clicks pass straight through to the applications underneath.
- **Header Proximity & Interactivity Waking:** When you move your cursor or click on the upper titlebar header of a click-through window, OpenWindow automatically restores header interactivity, surfaces the toolbar, and activates the window instantly—**no need to minimize or use the Windows Taskbar!**

### 🌙 6. GPU-Accelerated Inverted Dark Mode — `Alt + D`
- Instantly inverts window colors using the **Windows Magnification API** GPU matrix.
- Transforms bright, legacy white software into eye-friendly dark themes with 0% CPU overhead.

### 🛸 7. Non-Activating Floating Titlebar Toolbar
- Automatically docks a compact, translucent pill inside the active window's title bar.
- Uses `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` so clicking toolbar buttons never steals keyboard focus from your active work.
- **Horizontal-Only Constrained Dragging:** Drag smoothly left and right along the title bar in both expanded and collapsed modes (`Cursor="SizeWE"`), locked vertically (`Top = dipTop + 3.0`) with zero snapping or drifting.
- **Symmetrical Center Preservation:** Pre-calculates layout targets so expanding and collapsing remains symmetrically anchored at the exact same location.
- **Adaptive Compact Mode:** Automatically collapses secondary buttons on narrow windows (< 580 DIPs) to fit neatly without overflowing or covering window caption controls.
- **Browser & IDE Tab Avoidance:** Dynamically calculates left offsets on Google Chrome, Microsoft Edge, Firefox, Brave, and VS Code so the toolbar never obscures open tabs.
- **Per-Window Position Memory:** Remembers custom toolbar drag locations (`ArrowCenterRatio` and `SideOffset`) across minimize, maximize, and restore cycles.

### ⚙️ 8. Saved Windows Profile Manager
- Easily view, add, or remove custom preferences saved for specific applications in the Settings dashboard.

### 🔄 9. 1-Click In-App Auto-Updater & Release Notifications
- Automatically checks for new versions on startup and every 24 hours via the GitHub Releases API.
- Shows release changelogs, version comparison badges, and real-time streaming download progress.
- 1-Click background installation smoothly replaces binaries and restarts the application without losing user preferences.
- Configurable toggle and manual "Check for Updates Now" button inside the Settings dashboard.

### 🛡️ 10. Windows Taskbar & Shell Immunity (`ShellSanitizer`)
- **Complete Shell Isolation:** Windows Taskbar (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`), Notification Area Overflow flyouts (`NotifyIconOverflowWindow`), and Windows Desktop (`Progman`, `WorkerW`) are strictly immune from accidental opacity, pinning, click-through, or toolbar docking.
- **Always-on-Top Auto-Reveal:** Ensures the Windows Taskbar always stays above pinned user windows in the Topmost Z-order band, allowing auto-hide and regular taskbars to surface smoothly when moving the cursor to the bottom screen edge.
- **Active Shell Repair:** Automatically repairs and restores full solid 100% opacity on system tray flyouts and Windows shell components.

---

## ⌨️ Default Hotkeys

| Action | Hotkey | Description |
| :--- | :---: | :--- |
| **Always-on-Top** | `Alt + Z` | Pin / unpin active window on top |
| **Picture-in-Picture** | `Alt + Q` | Toggle live 60 FPS PiP mode |
| **Smart Opacity** | `Alt + A` | Cycle window transparency (100% -> 80% -> 60% -> 40%) |
| **Click-Through** | `Alt + C` | Toggle click-through mode for transparent windows |
| **Dark Mode** | `Alt + D` | Toggle GPU-accelerated inverted dark mode |
| **Fine Opacity** | `Alt + Scroll` | Adjust opacity up or down in real-time |

---

## 💻 System Compatibility

| Windows OS | 64-bit (x64) | 32-bit (x86) | Standalone (Zero .NET Required) |
| :--- | :---: | :---: | :---: |
| **Windows 11** (all versions) | ✅ Supported | ✅ Supported | ✅ Yes (Self-Contained) |
| **Windows 10** (1809+) | ✅ Supported | ✅ Supported | ✅ Yes (Self-Contained) |
| **Windows Server 2019 / 2022** | ✅ Supported | ✅ Supported | ✅ Yes (Self-Contained) |

---

## 🚀 Download & Installation for End Users

### Option 1: Standalone Native Desktop Release (Recommended)
1. Download the latest release ZIP from [Releases](https://github.com/codebyatta/openwindow/releases):
   - **64-bit Windows:** `OpenWindow-v1.1.0-win-x64.zip`
   - **32-bit Windows:** `OpenWindow-v1.1.0-win-x86.zip`
2. Extract the ZIP anywhere on your PC.
3. Right-click `install.ps1` and select **Run with PowerShell** (or run `CodeByatta.UI.exe` directly as a portable app).
   - The installer creates Start Menu and Desktop shortcuts and adds OpenWindow to Windows Startup.
   - OpenWindow launches immediately in your system tray.

### Option 2: Build From Source (Developers)
```powershell
# Clone repo
git clone https://github.com/codebyatta/openwindow.git
cd openwindow

# Restore & Build Release
dotnet build OpenWindow.sln -c Release

# Run Unit & Integration Tests (28 tests)
dotnet test OpenWindow.sln -c Release --no-build

# Package Native ZIP Releases (x64 & x86)
.\package-release.ps1 -Version "1.1.0"
```

---

## 🏗️ Architecture & Technology Stack

```
OpenWindow.sln
 ├── src/
 │    ├── CodeByatta.Core/             # Pure Win32, DWM, Magnification, and ShellSanitizer layer
 │    │    ├── Interop/                # User32, DwmApi, MagnificationApi P/Invoke
 │    │    ├── Models/                 # AppConfig, ManagedWindow, WindowPreferences
 │    │    └── Services/               # Topmost, Opacity, DarkMode, PipManager, ShellSanitizer, Tracking
 │    └── CodeByatta.UI/               # WPF / ModernWpf Presentation Layer
 │         ├── Resources/              # Embedded application icons
 │         └── Views/                  # TitleBarToolbar, PipWindow, CropOverlay, Settings
 ├── tests/
 │    ├── CodeByatta.Core.Tests/       # Domain logic, layout, and config unit tests
 │    └── CodeByatta.Integration.Tests # Live Win32 and DWM composition tests
 ├── install.ps1                       # Automated 1-click installer and background launcher
 ├── uninstall.ps1                     # 1-click uninstaller and registry cleanup
 ├── package-release.ps1               # Multi-architecture native release packager
 └── docs/                             # In-depth architectural and developer documentation
```

---

## 🤝 Contributing

Contributions, bug reports, and feature requests are welcome! Please check out [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code style, test requirements, and pull request workflow.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<div align="center">

Crafted with care by **[CodeByatta](https://github.com/codebyatta)**  
*Modern Windows Utility & Desktop Engineering*

</div>
