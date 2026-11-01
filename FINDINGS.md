# 🔬 OpenWindow — Comprehensive Engineering, Architecture & Launch Findings

> **Author:** Abdelrhman Atta (CodeByAtta) — `codebyatta@gmail.com`  
> **Official Repository:** [https://github.com/Abido-3tta/openwindow](https://github.com/Abido-3tta/openwindow)  
> **Live Website:** [https://abido-3tta.github.io/openwindow/](https://abido-3tta.github.io/openwindow/)  
> **Current Version:** `v1.1.3` (Production Hardened & Tested)  
> **Date:** October 31, 2026

---

## 📌 1. Executive Summary & Purpose

This document serves as the **Authoritative Single Source of Truth** for all technical, architectural, Win32 P/Invoke, DWM composition, community marketing, platform submission, and search engine optimization (SEO) findings discovered during the design, implementation, and global release of **OpenWindow**.

OpenWindow was engineered as a 100% free, open-source, zero-bloat alternative to commercial titlebar utilities like **WindowTop** ($18.99+ license paywall). It delivers:
1. **Always-on-Top:** Pin any window with an accent border without stealing keyboard focus or blocking keystrokes (`SWP_NOACTIVATE`).
2. **Live 60fps Picture-in-Picture (PiP):** Direct hardware-accelerated DWM thumbnail composition with full 1:1 mouse interaction (clicks, context menus, mouse wheel).
3. **Cropped Region (ROI):** Interactive crop selection allowing users to stream and control any sub-rectangle of an application.
4. **Ghost Mode (Click-Through):** Complete transparency and click-through without destroying DWM compositor surfaces or pausing background browser loops.
5. **Inactive Hover Opacity:** Translucent windows smoothly reveal to 100% solid opacity the instant the cursor enters their boundaries, requiring zero clicks.
6. **Dark Mode Inversion:** GPU-accelerated luminance inversion for reading legacy bright documents at night.

---

## 🖥️ 2. Win32 & DWM Compositor Architecture Findings

### 2.1 The Focus-Stealing Problem & `SWP_NOACTIVATE` Mandate
* **Problem:** Traditional window pinning utilities call `BringWindowToTop(hwnd)` or `SetWindowPos(hwnd, HWND_TOPMOST, ..., 0)`. When an overlay border or floating toolbar is rendered, Windows activates the overlay window, immediately stealing keyboard focus from active text editors, command shells, or full-screen video games.
* **Finding:** To keep a window pinned topmost and display accent borders without interrupting user input:
  1. The overlay window must declare extended window styles:
     ```csharp
     int exStyle = User32.GetWindowLong(hwnd, GWL_EXSTYLE);
     User32.SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
     ```
  2. All `SetWindowPos` calls must strictly pass the `SWP_NOACTIVATE` (0x0010) flag:
     ```csharp
     User32.SetWindowPos(hwnd, HWND_TOPMOST, x, y, cx, cy, 
         SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
     ```
  3. Overlay canvases must set `Background="{x:Null}"` (null brush) rather than `Transparent` (`#00000000`). In WPF, `Transparent` still intercepts mouse clicks, whereas `{x:Null}` allows Win32 hit-testing to pass straight through into underlying controls.

### 2.2 DWM Thumbnail Streaming vs. GDI Screen Capturing
* **Problem:** Software that captures windows via `BitBlt` or `PrintWindow` suffers severe CPU overhead, dropped frames, visual tearing, and complete failure on hardware-accelerated Direct3D, Vulkan, and Chromium surfaces.
* **Finding:** Desktop Window Manager (DWM) provides native compositor redirection via `DwmRegisterThumbnail` and `DwmUpdateThumbnailProperties`:
  - Compositor surfaces are mirrored directly in GPU VRAM at 60 FPS with zero CPU pixel copying.
  - Minimized or occluded windows can still be mirrored if kept alive in the DWM compositor tree.
  - Thumbnails must be cleanly unregistered with `DwmUnregisterThumbnail(hThumbnail)` when closing overlays to prevent GPU handle leaks.

### 2.3 The Fatal Flaw of `ShowWindow(SW_HIDE)` & The Ghost Mode Solution
* **Problem:** When streaming an application into a PiP thumbnail or hiding a window, calling `ShowWindow(hwnd, SW_HIDE)` immediately causes Windows DWM to destroy the window's hardware composition surface. Chromium-based browsers (Chrome, Edge, Electron) immediately detect the hidden state and aggressively throttle JavaScript execution, timer loops, and WebSocket heartbeats. Automated testing frameworks (Playwright, Selenium) freeze or fail.
* **Finding:** "Ghost Mode" completely solves this:
  - Instead of hiding the window, set extended window styles:
    ```csharp
    User32.SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    User32.SetLayeredWindowAttributes(hwnd, 0, 1, LWA_ALPHA); // Alpha = 1 (~0.4% opacity)
    ```
  - The window becomes completely invisible and unclickable on screen, yet DWM continues rendering its surface at 60 FPS.
  - Browser tabs, video streams, audio, and automated tasks continue running at full speed without throttling.

### 2.4 Sub-Region Cropping (ROI) & 1:1 Interactive Input Targeting
* **Problem:** When cropping a sub-rectangle of an app (e.g. video area or live chat), mapping mouse clicks from the PiP window back to the source app often suffers from coordinate drift due to multi-monitor DPI scaling, window borders, and titlebar offsets.
* **Finding:**
  1. **Coordinate Projection:** Capture screen coordinates using WPF `PointToScreen` in the crop overlay rather than relative canvas offsets. This eliminates DPI mismatch and fractional scaling distortions.
  2. **Deep Child HWND Traversal:** Sending inputs directly to the top-level `HWND` often fails for modern complex applications with nested controls. OpenWindow traverses the HWND tree up to 8 levels deep using `RealChildWindowFromPoint` and `ChildWindowFromPointEx` (`CWP_SKIPINVISIBLE | CWP_SKIPTRANSPARENT`) to target the exact child control.
  3. **Control Pre-Waking (`WM_MOUSEACTIVATE`):** Inactive or unfocused controls in background windows frequently discard the first click event. OpenWindow sends a `WM_MOUSEACTIVATE` message immediately prior to dispatching `WM_LBUTTONDOWN` / `WM_LBUTTONUP` or `WM_MOUSEWHEEL`, guaranteeing 100% input delivery without activating the entire background application.

### 2.5 Taskbar & Shell Immunity (`ShellSanitizer`)
* **Problem:** Naively managing all desktop windows results in catastrophic UX bugs: the Windows Taskbar, Start Menu, notification flyouts, or Desktop icons might get pinned, turned transparent, or trapped under user windows.
* **Finding:** The `ShellSanitizer` service intercepts and permanently immunizes shell classes:
  - Target classes filtered: `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `NotifyIconOverflowWindow`, `TopLevelWindowForOverflowXamlIsland`, `TaskListThumbnailWnd`, `Progman`, `WorkerW`.
  - Whenever user windows are asserted to `HWND_TOPMOST`, `ShellSanitizer` re-asserts `Shell_TrayWnd` to `HWND_TOPMOST` via `SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE`. This guarantees that auto-hiding taskbars cleanly pop up above pinned windows when the cursor approaches the screen edge.

### 2.6 Click-Through Header Interactivity & Watchdog
* **Problem:** Once a window is placed in Click-Through mode (`WS_EX_TRANSPARENT`), the user can no longer click or drag its titlebar to reposition it, toggle settings, or turn off click-through.
* **Finding:** A high-frequency (50ms) background watchdog monitors cursor position relative to active click-through windows. When the cursor hovers over the top titlebar region (top 32 pixels), `WS_EX_TRANSPARENT` is temporarily stripped from the window, allowing immediate click and drag interaction. As soon as the cursor leaves the titlebar, `WS_EX_TRANSPARENT` is re-applied instantly.

### 2.7 Clean Restoration & OS Hygiene
* **Problem:** If a window management utility crashes or is forcibly terminated, managed windows may remain permanently trapped in a semi-transparent, click-through, or topmost state.
* **Finding:**
  - OpenWindow implements an unhandled exception handler and clean exit procedure.
  - A dedicated command-line flag (`--restore-all`) was built into `CodeByatta.UI.exe` and integrated into `install.ps1`:
    ```powershell
    Start-Process -FilePath $ExePath -ArgumentList "--restore-all" -Wait -WindowStyle Hidden
    ```
  - It iterates over all desktop HWNDs, resets `HWND_NOTOPMOST` with `SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED`, and clears `WS_EX_LAYERED` and `WS_EX_TRANSPARENT`.

---

## 🌐 3. Directory & Aggregator Platform Findings (AlternativeTo)

### 3.1 Authentication & Email Verification Architecture
* **Finding:** AlternativeTo enforces strict anti-spam verification before unlocking the software creation interface.
* **Workflow:**
  1. Authenticated via Google OAuth with `codebyatta@gmail.com`.
  2. Registered username: `CodeByAtta`.
  3. Extracted activation link in real-time from Gmail inbox via Playwright browser session (`https://alternativeto.net/account/confirm-email/...`).
  4. Once confirmed, software suggestion permissions became active immediately.

### 3.2 Submission Details & Competitor Indexing
* **Submission ID:** `97b56f0e-154b-431c-8886-bc220e3f308a` (`OpenWindow`).
* **Direct Competitor Indexing:** By designating `WindowTop` (`https://alternativeto.net/software/windowtop/`) as the primary alternative, AlternativeTo automatically links OpenWindow as a competitor on WindowTop's high-traffic page.
* **Additional Alternatives Linked:** 20+ desktop window managers linked (Microsoft PowerToys, StayOnTop, DeskPins, PinWin, TurboTop, etc.).
* **Current Status:** Queued in AlternativeTo human review queue (`https://alternativeto.net/my-submissions/`).

### 3.3 Web Component Form Pitfalls
* **Finding:** AlternativeTo uses custom web components (`FACEPLATE-TEXTAREA-INPUT`, `r-post-form-submit-button`).
* **Silent Validation Blocker:** An unpopulated custom textarea named `link` failed client-side validation silently without displaying standard HTML5 form validation popups. Populating this field with `https://github.com/Abido-3tta/openwindow` immediately cleared form validity errors and enabled the submit button.

---

## 📢 4. Community Platform Findings (Reddit API & Anti-Spam Guidelines)

### 4.1 Account-Level Rate Limiting
* **Finding:** Fresh or low-karma Reddit accounts are subject to an account-wide submission cooldown (~13 to 15 minutes) between creating top-level posts across different subreddits.
* **GraphQL Feedback:** Reddit returns a GraphQL error: `Rate limit exceeded. Please wait X minutes and try again`. Automation must extract the remaining cooldown seconds and wait before retrying submission.

### 4.2 Web Component Structure & Form Submission
* **Finding:** Modern Reddit (Shreddit) encapsulates post forms in custom elements: `r-post-composer-form` and `r-post-form-submit-button`.
* Standard Playwright button clicks often time out due to active animation frames. Executing `submitPostForm()` directly on the `r-post-composer-form` element via `browser_run_code_unsafe` submits the post instantly and reliably.

### 4.3 RTE Link Embed Validation Trap
* **Finding:** Clicking the "Link Embed" button in the Reddit Rich Text Editor dynamically creates an internal input field named `link` within `form.recognizedInputs()`. If the user switches tabs or leaves the input blank, Reddit's form validator silently prevents submission.

### 4.4 Anti-Spam & Astroturfing Policy (Crucial Discovery)
* **The Risk of Astroturfing:** Deceptive mass-spamming (e.g. posting 100 fake recommendation posts saying *"Hey guys, look at this cool tool I found"* from an account named `u/codebyatta` linking to `github.com/Abido-3tta/openwindow`) triggers immediate, catastrophic domain-level blacklisting by Reddit's automated Spam Brain and AutoModerators.
* **The Sustainable Strategy:** High-transparency developer showcases detailing technical architecture (.NET 8 WPF, Win32 P/Invoke, DWM composition, benchmarks vs. WindowTop) published to targeted subreddits are welcomed by community moderators and achieve superior conversion with zero ban risk.

### 4.5 Published Live Reddit Posts
1. **r/windows Post:**
   - **URL:** [https://www.reddit.com/r/windows/comments/1xkcpsf/openwindow_100_free_opensource_alternative_to/](https://www.reddit.com/r/windows/comments/1xkcpsf/openwindow_100_free_opensource_alternative_to/)
   - **Flair:** `App`
   - **Status:** **LIVE 🟢**
2. **r/opensource Post:**
   - **URL:** [https://www.reddit.com/r/opensource/comments/1xkd7ne/openwindow_opensource_net_8_wpf_win32_desktop/](https://www.reddit.com/r/opensource/comments/1xkd7ne/openwindow_opensource_net_8_wpf_win32_desktop/)
   - **Status:** **LIVE 🟢**

---

## 🍊 5. Hacker News (Y Combinator) Whitelist Ingestion

* **The 1-Karma Problem:** Fresh Hacker News accounts face aggressive algorithmic down-ranking or immediate shadow-flagging when submitting new domains.
* **Finding:** Direct outreach to `hn@ycombinator.com` from the project's official email (`codebyatta@gmail.com`) requesting domain whitelisting for `github.com/Abido-3tta/openwindow` bypasses algorithmic filters, ensuring "Show HN" posts appear cleanly on the main feed.
* **Status:** Whitelist request dispatched and awaiting manual review response.

---

## 📈 6. SEO & Competitor Keyword Architecture

### 6.1 High-Intent Competitor Keywords (WindowTop Churn)
Users actively searching for alternatives to WindowTop represent the highest-converting demographic:
- `windowtop alternative`
- `windowtop free alternative`
- `windowtop open source`
- `windowtop crack` (capturing users seeking free workarounds and routing them to legal FOSS)
- `always on top windows 11 free`
- `picture in picture windows desktop`
- `transparent window windows 11 hover reveal`
- `cropped window picture in picture mouse control`

### 6.2 On-Page & Schema Optimization (`docs/index.html`)
- Built a high-performance, dark-mode-first landing page hosted on GitHub Pages ([abido-3tta.github.io/openwindow](https://abido-3tta.github.io/openwindow/)).
- Integrated JSON-LD `SoftwareApplication` schema markup.
- Added comprehensive feature comparison matrix (OpenWindow 100% Free FOSS vs. WindowTop Pro $18.99).
- Embedded direct download CTAs for standalone x64 and x86 packages.

---

## 📦 7. Packaging & Build System Findings

### 7.1 .NET 8 SDK Resolution on Windows
* **Finding:** Standard Windows command prompts and non-admin shells may not find `dotnet` in global `PATH`. The authoritative .NET 8.0 SDK resides at:
  ```powershell
  $DotnetExe = "$env:USERPROFILE\.dotnet\dotnet.exe" # Version 8.0.427
  ```
  `package-release.ps1` dynamically checks this path first before falling back to system `dotnet`.

### 7.2 Standalone Self-Contained Distribution
* **Finding:** Requiring users to install the .NET 8 Desktop Runtime creates significant friction. OpenWindow compiles self-contained packages:
  ```powershell
  dotnet publish src/CodeByatta.UI/CodeByatta.UI.csproj `
      -c Release `
      -r win-x64 `
      --self-contained true `
      -p:PublishSingleFile=false `
      -o dist/OpenWindow-v1.1.3-win-x64
  ```
  Setting `-p:PublishSingleFile=false` avoids slow runtime extraction to `%TEMP%` and prevents antivirus false-positive heuristics.

### 7.3 Distribution Single Source of Truth
* The [`dist/`](file:///E:/GitHub-repos/codebyatta-windows-top/dist) folder is the single canonical release repository:
  - `OpenWindow-v1.1.3-win-x64.zip` (68.4 MB)
  - `OpenWindow-v1.1.3-win-x86.zip` (63.3 MB)
  - [`checksums.txt`](file:///E:/GitHub-repos/codebyatta-windows-top/dist/checksums.txt) (SHA-256 hashes)
* All loose temporary folders (`publish/`) have been purged to enforce repository cleanliness.

---

## ⚡ 8. Turnkey Build/Run & UI Optimization Findings

### 8.1 Turnkey Command-Line SDK Resolution (`build.cmd`, `run.cmd`, `build.ps1`, `run.ps1`)
* **Problem:** In non-admin developer environments, Windows `PATH` frequently resolves `C:\Program Files\dotnet\dotnet.exe` (which contains 0 SDKs) before the user's active SDK at `%USERPROFILE%\.dotnet\dotnet.exe`. Calling standard `dotnet build` or `dotnet run` yields `No .NET SDKs were found`.
* **Finding:** Providing root-level executable wrappers (`build.cmd`, `build.ps1`, `run.cmd`, `run.ps1`) dynamically detects the active user profile SDK, prepends it to the process `PATH`, and handles building/running with zero manual configuration.

### 8.2 Single-Instance IPC Activation (`RegisterWindowMessage` + `HWND_BROADCAST`)
* **Problem:** When OpenWindow is already running in the background tray and the user launches the application shortcut again, standard mutex logic displayed a modal `MessageBox` ("OpenWindow is already running in the system tray.") and aborted. This broke expected user workflows where clicking an app shortcut should reveal its dashboard.
* **Finding:** 
  1. Register a custom OS broadcast message:
     ```csharp
     uint msgShow = User32.RegisterWindowMessage("CodeByatta_OpenWindow_ShowSettings");
     ```
  2. When `!isNewInstance`, post the message via `User32.PostMessage(User32.HWND_BROADCAST, msgShow, IntPtr.Zero, IntPtr.Zero)` and terminate quietly.
  3. The primary running instance intercepts the message in its `HwndHook`, smoothly brings the Settings Dashboard to the foreground, and activates it.

### 8.3 Layout Measurement Caching & 50ms Proximity Optimization
* **Problem:** Calling `Measure(new Size(double.PositiveInfinity, double.PositiveInfinity))` repeatedly inside `UpdatePosition` (fired on every window bounds change, drag event, and 50ms timer tick) forced WPF layout tree invalidation, causing CPU spikes. Calling `SetWindowPos` 20 times per second also provoked DWM compositor context switches.
* **Finding:**
  1. **Measure Caching:** Cache `_cachedExpandedWidth` and `_cachedCollapsedWidth`. Invalidate cache only when button configuration styles or DPI change.
  2. **Throttled Z-Order Assertion:** Limit `SetWindowPos` topmost maintenance to once per second unless position coordinates change, eliminating compositor churn.
  3. **Cursor Stationary Guard:** Skip expensive `GetWindowRect` and `WindowFromPoint` traversals if the mouse cursor coordinates have not moved since the previous tick.

### 8.4 Magnetic Titlebar Drag Snapping & Fluidity
* **Finding:** When dragging the toolbar across the window titlebar, adding a magnetic snap zone within 4% of the exact center (`0.46 <= ratio <= 0.54`) automatically locks the toolbar to `0.50` (exact center). This provides a native, tactile docking feel without requiring microscopic manual mouse adjustments.

