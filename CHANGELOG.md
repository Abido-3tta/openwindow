# Changelog

All notable changes to **OpenWindow** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.0] - 2026-10-30

### Added
- **Global Inactive & Background Hover Opacity Reveal:** Enhanced `OpacityService` with centralized `ActiveTransparentWindows` tracking. Transparent windows now smoothly reveal 100% opacity whenever the cursor enters their body—even when the window is in the background, inactive, or unfocused—**zero click required**. Transparency is automatically restored when the cursor leaves.
- **Smart Toolbar Hover Exemption:** Moving the mouse over the in-titlebar floating toolbar keeps the window transparent so you can inspect and adjust opacity sliders/buttons without visual flicker.
- **Official Fluent Glassmorphic Logo & Multi-Res Icon:** Integrated the official "Frosted Glass Stack" logo icon with smooth anti-aliased alpha transparency, producing multi-resolution Win32 `.ico` (`16px`, `24px`, `32px`, `48px`, `64px`, `128px`, `256px`) and 512px master `.png`.
- **Clean-Slate Automated Installer (`install.ps1`):** Upgraded installer to completely wipe previous installation directories and deploy pre-compiled standalone native release binaries directly from `dist/` or `publish/`.
- **Start Menu Shell Cache Sanitation:** Automatically clears stale Windows Shell history entries (`UFH\SHC` and `FeatureUsage\AppSwitched`), ensuring only the official `OpenWindow` appears in the Windows 11/10 Start Menu Recent list.
- **Horizontal-Only Constrained Dragging:** The toolbar locks strictly inside the title bar header (`Top = dipTop + 3.0`) and glides horizontally with smooth mouse-capture tracking (`Cursor="SizeWE"`).
- **Proportioned Collapsed Anchor Handle:** Resized anchor pill (`MinHeight="28"`, `Padding="8,4,12,4"`, `FontSize="11.5"`) with embedded horizontal drag grip (`AnchorDragGrip`) for dragging in both expanded and collapsed states.
- **Double-Click Anchor Pin:** Direct double-clicking on the collapsed anchor handle (`⚡ OpenWindow ▼`) immediately toggles Always-on-Top without opening the full toolbar.
- **Symmetric Center Preservation on Expand/Collapse:** Fixed width pre-measurement via `GetToolbarTargetWidth()` guaranteeing symmetrical expansion and collapse centered around the exact same coordinate.
- **Selective Header Interactivity for Click-Through:** 50ms active monitoring of all click-through windows. Hovering or clicking on the upper titlebar temporarily restores interactivity and activates the window without needing the Windows Taskbar.
- **Adaptive Compact Mode for Narrow Windows:** Automatic responsive layout (< 580 DIPs) hiding secondary buttons to fit compact utility windows and dialogs.
- **Browser & IDE Tab Avoidance:** Dynamic header boundary calculation for tabbed applications (Google Chrome, Microsoft Edge, Mozilla Firefox, Brave, VS Code) preventing toolbar overlap with active tabs.
- **Standalone Self-Contained Desktop Native Distribution:** Full support for zero-dependency standalone native deployment on Windows 10 and 11 across both 64-bit (`win-x64`) and 32-bit (`win-x86`) architectures without requiring the .NET SDK or runtime.
- **1-Click In-App Auto-Updater & Release Notification Engine:** Background query against GitHub Releases API (`https://api.github.com/repos/codebyatta/openwindow/releases/latest`) on startup and every 24 hours. Features a Fluent ModernWpf `UpdateDialog` with release notes preview, live download progress bar, and 1-click background self-restart installation.
- **Settings Update Dashboard:** Added automatic update checking toggle switch and a manual "Check for Updates Now" button with real-time connectivity status.
- **Automated Multi-Arch Release Packager:** New `package-release.ps1` script generating standalone release ZIP bundles with installer and SHA-256 checksums.

### Fixed
- **Shell & Taskbar Immunity (`ShellSanitizer`):** Complete isolation of Windows Taskbar (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`), Notification Area Overflow flyouts (`NotifyIconOverflowWindow`, `TopLevelWindowForOverflowXamlIsland`), and Desktop/Shell processes from opacity, topmost, click-through, and toolbar attachment.
- **Always-on-Top Taskbar Auto-Reveal Priority:** Continuous Z-order assertion and cursor edge detection ensuring the Windows Taskbar always stays atop user pinned topmost windows and pops up smoothly on hover.
- **Active Shell Transparency Repair:** Proactive background detection and stripping of stale `WS_EX_LAYERED` attributes on `NotifyIconOverflowWindow` and `Shell_TrayWnd`, restoring solid 100% opacity.
- **Config Preference Sanitization:** Isolated folder window memory under `explorer_folder`, preventing preferences from bleeding into `explorer.exe` shell infrastructure.
- Fixed toolbar disappearing or becoming unreachable when navigating between Click-Through windows and background windows.
- Fixed `XamlParseException` on missing `resources/app.ico` by embedding icon as a project resource and applying safe code-behind fallback.
- Fixed drag reset bug where releasing mouse snapped the toolbar vertically.
- Fixed `Alt + C` hotkey targeting background window when hovering over a click-through window.

---

## [1.0.0] - 2026-10-30

### Added
- **Floating Titlebar Toolbar:** Non-activating, draggable pill that automatically docks on the title bar of any active foreground window.
- **Always-on-Top (Pin):** Keeps any window pinned on top of all others with configurable accent borders and 3-second watchdog enforcement (`Alt + Z`).
- **Live 60fps Picture-in-Picture (PiP):** Direct DWM surface composition streaming with Ghost Mode (`WS_EX_LAYERED | WS_EX_TRANSPARENT`, Alpha = 1) ensuring zero browser throttling and zero automation conflicts (`Alt + Q`).
- **Interactive Mouse Control:** Direct mouse interaction (`WM_LBUTTONDOWN`, `WM_MOUSEMOVE`, `WM_MOUSEWHEEL`) inside the PiP viewport.
- **Sub-Region Cropping:** Crop any rectangular region of a window to stream in PiP mode.
- **Smart Opacity:** Smooth transparency cycling (100% -> 80% -> 60% -> 40% -> 100%) and mouse wheel scrolling (`Alt + A`).
- **Auto-Disable Opacity on Hover (`DisableWhenMouseAbove`):** Automatically restores 100% opacity when cursor is over the window body for crystal-clear readability.
- **Click-Through Mode:** Allows mouse clicks to pass through transparent windows to underlying applications (`Alt + C`).
- **Inverted Dark Mode:** Hardware-accelerated GPU color inversion via Windows Magnification API (`Alt + D`).
- **Per-Window Location Memory:** Remembers custom toolbar drag positions (`ArrowCenterRatio` and `SideOffset`) across minimize, maximize, and restore cycles.
- **Saved Windows Profile Manager:** Full dashboard in Settings to inspect, add, remove, or clear remembered process profiles.
- **Interactive Color Pickers:** Full Windows `ColorDialog` and 7 quick color presets for Topmost borders and Arrow chevrons.
- **Windows Startup Integration:** Automatic background startup via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **System Tray:** Full background tray menu with shortcuts and quick toggles.

### Fixed
- Fixed opacity cycling getting stuck at 40% on non-layered windows.
- Fixed click-through focus dropping when clicks passed through to underlying windows.
- Fixed toolbar re-expansion bounce loop on collapse by requiring deliberate cursor exit.
- Fixed WPF `XamlParseException` on `resources/app.ico` by embedding icon as a project manifest resource and loading defensively.
- Fixed offscreen minimize coordinate anomalies (`X < -10000`).

### Infrastructure
- Automated PowerShell installer (`install.ps1`) with desktop and Start menu shortcuts.
- GitHub Actions CI pipeline for automated build, test, and release artifact generation.
- Full xUnit test suite (unit and integration tests).
