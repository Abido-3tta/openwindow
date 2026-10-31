# OpenWindow - Remaining Steps, Feedback Architectures & Implementation Roadmap

> **Author:** Abdelrhman Atta (CodeByatta) — `codebyatta@gmail.com`  
> **Repository:** [github.com/Abido-3tta/openwindow](https://github.com/Abido-3tta/openwindow)  
> **Live Website:** [abido-3tta.github.io/openwindow](https://abido-3tta.github.io/openwindow/)  
> **Current Version:** `v1.1.3` (Production Hardened & Fully Verified)  
> **Date:** October 31, 2026

---

## 📌 1. Executive Summary & Current Deliverables (v1.1.3 Completed)

All 5 core production objectives and deep fixes have been completed, audited, and verified across 37 automated tests:

1. **Always-on-Top Input Non-Blocking:**
   - Enforced `SWP_NOACTIVATE` across [`TopmostBorderOverlayWindow`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/TopmostBorderOverlayWindow.xaml.cs) and [`App.xaml.cs`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/App.xaml.cs).
   - Eliminated `BringWindowToTop` calls that previously stole keyboard focus from text input fields and active games.
   - Set overlay canvas background to null (`Background="{x:Null}"`) so clicks pass directly into underlying controls.

2. **Luxury Frosted Acrylic Toolbar Styling:**
   - Removed Win32 DwmApi acrylic rectangle backdrop from floating toolbars to eliminate sharp 90° corners.
   - Implemented native WPF rounded frosted glass styling (`Border.CornerRadius="16"`) with omnidirectional drop shadows and specular border highlights (`#40FFFFFF`).

3. **1:1 Cropped & Full PiP Interactive Mouse Control:**
   - Overhauled [`PipWindow.xaml.cs`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/PipWindow.xaml.cs) and [`CropOverlayWindow.xaml.cs`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/CropOverlayWindow.xaml.cs).
   - Replaced relative canvas offsets with WPF `PointToScreen` to record absolute physical screen coordinates.
   - Added deep child HWND traversal up to 8 levels deep via `RealChildWindowFromPoint` and `ChildWindowFromPointEx` (filtering invisible and transparent windows).
   - Pre-wakes inactive windows and background browsers via `WM_MOUSEACTIVATE` before clicks or wheel deltas.
   - Supports left, right (with `WM_CONTEXTMENU`), middle, double-click, and vertical/horizontal wheel scrolling.

4. **CrashReporterService & Telemetry Pipeline:**
   - Built [`CrashReporterService.cs`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.Core/Services/CrashReporterService.cs) to catch unhandled AppDomain, Dispatcher, and Task exceptions.
   - Serializes JSON crash reports to `%APPDATA%\CodeByatta\OpenWindow\crash_reports\`.
   - Dispatches telemetry pings to configured webhooks (Discord, Telegram, generic HTTP).
   - Displays [`CrashReportDialog.xaml`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/CrashReportDialog.xaml) on next startup.

5. **Pin Button & Developer Profile Card:**
   - Redesigned Pin button with vibrant red state `#FF453A` when active and neutral slate when unpinned.
   - Added Developer Profile Card with avatar in [`SettingsWindow.xaml`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/SettingsWindow.xaml).

---

## 📡 2. Complete Guide: Options for Receiving User Feedback & Issue Reports

To capture user reports, feature requests, bug diagnostics, and telemetry beyond basic Webhooks (Discord, Telegram, generic HTTP), the following channels can be embedded directly into OpenWindow:

### Group A: Serverless & Zero-Maintenance Ingestion

#### 1. GitHub Issues Web Intent (Recommended - 0 Infrastructure)
- **Concept:** Launch the user's default browser directly to a pre-filled GitHub Issue form with environment diagnostics embedded in the query string.
- **URL Pattern:**
  ```csharp
  var title = Uri.EscapeDataString($"[Bug]: {errorBrief}");
  var body = Uri.EscapeDataString(
      $"### Description\n{userNotes}\n\n" +
      $"### Environment\n- **OpenWindow Version:** {AppConfig.Version}\n" +
      $"- **OS:** {Environment.OSVersion} ({RuntimeInformation.OSArchitecture})\n" +
      $"- **.NET Runtime:** {Environment.Version}\n\n" +
      $"### Stack Trace\n```\n{stackTrace}\n```");
  Process.Start(new ProcessStartInfo($"https://github.com/Abido-3tta/openwindow/issues/new?title={title}&body={body}") { UseShellExecute = true });
  ```
- **Pros:** 100% free, zero backend maintenance, public issue tracking, encourages community engagement.
- **Cons:** Requires the user to have a GitHub account to submit.

#### 2. Formspree / Formkeep / Basin (Serverless Form Ingestion)
- **Concept:** An HTTP POST from C# `HttpClient` directly to an endpoint that forwards submissions directly into `codebyatta@gmail.com`.
- **Implementation:**
  ```csharp
  var payload = new { email = userEmail, message = userNotes, diagnostics = crashJson };
  await httpClient.PostAsJsonAsync("https://formspree.io/f/{your_form_id}", payload);
  ```
- **Pros:** Zero user sign-in needed; user types report inside OpenWindow, clicks "Send", and it arrives in your inbox.
- **Cons:** Free tier typically limited to 50 submissions/month; paid tier required for higher volume.

#### 3. Google Sheets Webhook (via Google Apps Script - Free & Permanent)
- **Concept:** Deploy a 5-line Google Apps Script on Google Drive as a Web App (`doPost(e)`). When OpenWindow posts JSON, the script appends a new row in a private Google Sheet.
- **Apps Script Code:**
  ```javascript
  function doPost(e) {
    var data = JSON.parse(e.postData.contents);
    var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
    sheet.appendRow([new Date(), data.version, data.os, data.type, data.message, data.stack]);
    return ContentService.createTextOutput(JSON.stringify({ status: "success" })).setMimeType(ContentService.MimeType.JSON);
  }
  ```
- **Pros:** 100% free forever, unlimited storage, immediate live notification on phone, easy filtering and sorting.
- **Cons:** Requires one-time Google Apps Script setup.

#### 4. Supabase / Firebase Cloud REST Table
- **Concept:** OpenWindow communicates with a Supabase PostgreSQL or Firestore NoSQL table via anonymous public API key with write-only row permissions.
- **Pros:** Enterprise-grade security rules (Row Level Security allows write, denies public read), real-time dashboards.
- **Cons:** Requires cloud project maintenance and API key rotation.

#### 5. Sentry.io Community SDK (`Sentry.Wpf`)
- **Concept:** Official native C# SDK capturing stack traces, breadcrumbs, OS version, GPU hardware, and unhandled exceptions automatically.
- **Implementation:** Install `Sentry.Wpf` package and initialize in `App.xaml.cs`:
  ```csharp
  SentrySdk.Init(o => { o.Dsn = "https://examplePublicKey@o0.ingest.sentry.io/0"; });
  ```
- **Pros:** Industry standard, captures exact line numbers, thread states, and release versions.
- **Cons:** External SDK dependency, 5,000 free events/month cap.

---

### Group B: Direct In-App Client Channels

#### 6. In-App User Feedback & Star-Rating Modal Dialog
- **Concept:** Native ModernWpf dialog with 5-star rating, category dropdown (Bug, Feature Request, Compliment), and comment box.
- **Action:** Submits payload asynchronously in background using one of the Group A backends without opening a web browser.

#### 7. 1-Click Desktop Diagnostic ZIP Export
- **Concept:** A button in Settings labelled **"Export Diagnostic Package (.zip)"**.
- **Action:** Gathers `%APPDATA%\CodeByatta\OpenWindow\crash_reports\`, `settings.json`, and hardware info, zips them into `%USERPROFILE%\Desktop\OpenWindow-Diagnostics.zip`, and reveals it in Windows Explorer.
- **Pros:** Privacy-preserving, offline-friendly, zero reliance on external network access.

#### 8. Default Email Client Protocol (`mailto:`)
- **Concept:** Generates a pre-formatted email link:
  ```csharp
  var mailto = $"mailto:codebyatta@gmail.com?subject=OpenWindow%20Feedback%20v1.1.3&body=Description:%0A%0AOS:%20{Environment.OSVersion}";
  Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
  ```
- **Pros:** Instant, zero code dependencies, works on all Windows versions with configured mail app.

---

## 🛠️ 3. Remaining Tasks & Action Items Backlog

When ready to proceed with next developments, execute the following bounded tasks:

### Task 1: Complete Creator Avatar Integration & Verification
- **Target:** [`SettingsWindow.xaml`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/SettingsWindow.xaml)
- **Status:** Verified high-resolution avatar is compiled as embedded `<Resource>` (`developer_avatar.png`).
- **Action:** Ensure live installed binary in `%LOCALAPPDATA%\Programs\OpenWindow` matches latest release build and displays avatar.

### Task 2: Implement User Feedback Ingestion Provider
- **Target:** [`CrashReporterService.cs`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.Core/Services/CrashReporterService.cs) and [`SettingsWindow.xaml`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.UI/Views/SettingsWindow.xaml)
- **Action:** Add selected feedback provider (GitHub Issues pre-fill button + Google Sheets webhook or Formspree).

### Task 3: Automatic GitHub Releases Polling Integration
- **Target:** [`UpdateService.cs`](file:///E:/GitHub-repos/codebyatta-windows-top/src/CodeByatta.Core/Services/UpdateService.cs)
- **Action:** Wire "Check for Updates Now" button to query `https://api.github.com/repos/Abido-3tta/openwindow/releases/latest` and display version changelog.

### Task 4: Advanced Window Snapping & Grid Tiling Engine
- **Target:** `CodeByatta.Core/Services/TilingService.cs`
- **Action:** Add hotkey and toolbar button to snap active windows to screen quadrants (left half, right half, top-right quarter).

### Task 5: Multi-Language Localization Expansion
- **Target:** `src/CodeByatta.UI/Resources/Strings/`
- **Action:** Add native Arabic (`ar-SA`), German (`de-DE`), and Japanese (`ja-JP`) language resource dictionaries.

---

## 🧪 4. Test Suite Reference

Run full test suite at any time:
```powershell
& "C:\Users\Dell\.dotnet\dotnet.exe" test OpenWindow.sln -c Release
```
Total passing tests: **37/37** across `CodeByatta.Core.Tests` and `CodeByatta.Integration.Tests`.
