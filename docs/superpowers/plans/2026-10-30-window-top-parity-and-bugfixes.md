# WindowTop Parity & Bugfixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the 40% opacity cycling bug, implement "Auto-disable opacity on mouse hover" (`DisableWhenMouseAbove`), prevent click-through mode button lockout, add "click-to-expand toolbar" mode, and build the "Saved Windows" manager in Settings.

**Architecture:** Update `ManagedWindow.RefreshInfo()` to correctly detect non-layered windows as 100% (255) opacity; introduce `DisableOpacityOnMouseOver` tracking in `TitleBarToolbarView` proximity loop to dynamically restore 100% opacity when cursor enters window bounds; harden `BtnClickThrough_Click` to prevent focus drops; add `RequireClickToExpandToolbar` setting; and add a dedicated "Window Profiles" management tab to `SettingsWindow` to inspect, add, and remove per-process preferences.

**Tech Stack:** .NET 8.0, C# 12, WPF/XAML, Win32 P/Invoke (`User32`, `DwmApi`), xUnit test suites.

---

### Task 1: Fix Opacity Cycling & Non-Layered Window Detection (TDD)

**Files:**
- Modify: `src/CodeByatta.Core/Models/ManagedWindow.cs:55-75`
- Modify: `src/CodeByatta.UI/Views/TitleBarToolbarView.xaml.cs:450-480`
- Test: `tests/CodeByatta.Core.Tests/ManagedWindowTests.cs`

- [ ] **Step 1: Write unit test for non-layered window opacity reset**
  In `ManagedWindowTests.cs`, add `ManagedWindow_NonLayeredWindow_Reports255Opacity()`.
- [ ] **Step 2: Run test to verify behavior**
  Run: `dotnet test tests/CodeByatta.Core.Tests --filter ManagedWindow_NonLayeredWindow_Reports255Opacity`
- [ ] **Step 3: Update `ManagedWindow.RefreshInfo()`**
  In `ManagedWindow.cs`:
  Check `isLayered = ((long)exStyle & User32.WS_EX_LAYERED) != 0;`
  If not layered, set `Opacity = 255;`.
  If layered, query `GetLayeredWindowAttributes`; if `LWA_ALPHA` flag is present set `Opacity = alpha`, otherwise `255`.
- [ ] **Step 4: Update `BtnOpacity_Click` in `TitleBarToolbarView.xaml.cs`**
  Ensure immediate assignment of `_targetWindow.Opacity = next;` before and after `_opacityService.SetOpacity`.
- [ ] **Step 5: Run tests to verify pass**
  Run: `dotnet test tests/CodeByatta.Core.Tests`
- [ ] **Step 6: Commit**
  `git commit -m "fix(opacity): resolve opacity cycling stuck at 40% and non-layered window reset"`

---

### Task 2: Implement "Disable Opacity When Mouse Is Over Window" (`DisableWhenMouseAbove`)

**Files:**
- Modify: `src/CodeByatta.Core/Models/AppConfig.cs:20-50`
- Modify: `src/CodeByatta.Core/Models/WindowPreferences.cs:15-30`
- Modify: `src/CodeByatta.UI/Views/TitleBarToolbarView.xaml.cs:230-310`
- Modify: `src/CodeByatta.UI/Views/SettingsWindow.xaml:250-280`
- Modify: `src/CodeByatta.UI/Views/SettingsWindow.xaml.cs:40-70`

- [ ] **Step 1: Add configuration properties**
  Add `public bool DisableOpacityOnMouseOver { get; set; } = true;` to `AppConfig.cs` and `WindowPreferences.cs`.
- [ ] **Step 2: Implement hover opacity temporary restoration**
  In `TitleBarToolbarView.xaml.cs` proximity loop:
  When `_targetWindow` has opacity < 255:
  If cursor is inside `_lastBounds` (and not on the toolbar):
    Temporarily apply alpha = 255 if not already boosted.
  When cursor leaves `_lastBounds`:
    Restore original configured opacity level.
- [ ] **Step 3: Add UI toggle in SettingsWindow**
  Under Advanced Features / Opacity Settings:
  Add "Disable opacity when mouse is over window (`DisableWhenMouseAbove`)".
- [ ] **Step 4: Run build and tests**
  Run: `dotnet test`
- [ ] **Step 5: Commit**
  `git commit -m "feat(opacity): implement auto-disable opacity on mouse hover over window body"`

---

### Task 3: Fix Click-Through Reactivation & Anti-Detachment

**Files:**
- Modify: `src/CodeByatta.UI/Views/TitleBarToolbarView.xaml.cs:480-505`
- Modify: `src/CodeByatta.UI/App.xaml.cs:310-330`

- [ ] **Step 1: Harden Click-Through Toggle in `TitleBarToolbarView.xaml.cs`**
  When enabling:
  - If target is not Topmost, enable Topmost automatically.
  - If target is 100% opaque, set to default opacity (e.g. 80%) so user sees underlying windows.
  - Expand toolbar and stop auto-hide so the Click-Through button remains clickable.
  When disabling:
  - Clear `WS_EX_TRANSPARENT`.
  - Call `User32.SetForegroundWindow(_targetWindow.Hwnd)` to restore active input focus.
- [ ] **Step 2: Prevent toolbar detachment on click-through windows in `App.xaml.cs`**
  In `TrackingService_ForegroundWindowChanged`:
  If current `_targetWindow` is in Click-Through mode and cursor is still over its bounds, retain toolbar attachment.
- [ ] **Step 3: Verify and Commit**
  `git commit -m "fix(clickthrough): prevent button lockout and retain toolbar attachment on click-through windows"`

---

### Task 4: Add "Require Click to Expand Toolbar (Disable Hover Auto-Expand)" Mode

**Files:**
- Modify: `src/CodeByatta.Core/Models/AppConfig.cs`
- Modify: `src/CodeByatta.UI/Views/TitleBarToolbarView.xaml.cs`
- Modify: `src/CodeByatta.UI/Views/SettingsWindow.xaml`
- Modify: `src/CodeByatta.UI/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Add `RequireClickToExpandToolbar` to `AppConfig.cs`**
  Default to `false` (hover to expand), allow toggling to `true` (click to expand only).
- [ ] **Step 2: Respect setting in `TitleBarToolbarView.xaml.cs`**
  In `ProximityTimer_Tick`:
  If `_config.RequireClickToExpandToolbar` is enabled, ignore hover timer and only expand when `AnchorHandle_MouseDown` is clicked.
- [ ] **Step 3: Add toggle in `SettingsWindow.xaml`**
  Add toggle switch: "Require click to expand toolbar (disable hover auto-expand)".
- [ ] **Step 4: Verify and Commit**
  `git commit -m "feat(toolbar): add click-only expansion mode to prevent unwanted hover expansion"`

---

### Task 5: Add "Saved Windows" Manager in Settings (View, Save, Remove Process Customizations)

**Files:**
- Modify: `src/CodeByatta.UI/Views/SettingsWindow.xaml`
- Modify: `src/CodeByatta.UI/Views/SettingsWindow.xaml.cs`
- Test: `tests/CodeByatta.Core.Tests/AppConfigTests.cs`

- [ ] **Step 1: Add "Window Profiles" Navigation Item and Panel in `SettingsWindow.xaml`**
  Include:
  - DataGrid / ListView showing saved process profiles (`ProcessName`, `Topmost`, `Opacity`, `ClickThrough`, `ArrowPosition`, `Collapsed`).
  - "Add Process..." input and button.
  - "Remove Selected" button.
  - "Clear All Saved Profiles" button.
- [ ] **Step 2: Wire event handlers in `SettingsWindow.xaml.cs`**
  Implement loading, saving, removing items from `_config.WindowSettings`, and synchronizing with active window cache.
- [ ] **Step 3: Run tests to verify persistence**
  Run: `dotnet test`
- [ ] **Step 4: Commit**
  `git commit -m "feat(settings): add interactive Window Profiles manager with add, remove, and clear capabilities"`

---

### Task 6: End-to-End Verification, Unit Tests & Production Deployment

**Files:**
- All projects in `CodeByattaWindowsTop.sln`
- `install.ps1`

- [ ] **Step 1: Run comprehensive tests**
  Run: `dotnet test -c Release`
- [ ] **Step 2: Publish Release package**
  Run: `dotnet publish src/CodeByatta.UI/CodeByatta.UI.csproj -c Release -o publish/`
- [ ] **Step 3: Run `install.ps1` to update local binaries and restart background process**
  Run: `powershell -ExecutionPolicy Bypass -File install.ps1`
- [ ] **Step 4: Verify running process and commit**
  Confirm PID and git commit.
