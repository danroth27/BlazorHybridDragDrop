# Blazor Hybrid Drag & Drop — cross-host repro for [dotnet/maui#2205](https://github.com/dotnet/maui/issues/2205)

A single shared set of drag-and-drop test components hosted in **four** Blazor Hybrid
front ends so the *same* HTML/JS drag-and-drop behavior can be compared across hosts and
platforms:

| Host | Project | Target | Build/run on this machine |
|------|---------|--------|---------------------------|
| .NET MAUI | `src/DragDropMaui` | `net10.0-android/ios/maccatalyst/windows` | ✅ Windows + Android (iOS/Mac need a Mac) |
| WPF | `src/DragDropWpf` | `net10.0-windows10.0.19041.0` | ✅ |
| Windows Forms | `src/DragDropWinForms` | `net10.0-windows10.0.19041.0` | ✅ |
| Shared UI | `src/DragDropShared` (RCL) | `net10.0` | — referenced by all hosts |

The **WPF** host reproduces the exact configuration reported in
[comment 4827818805](https://github.com/dotnet/maui/issues/2205#issuecomment-4827818805)
(“blazor hybrid WPF using .Net10-windows10.0… and the WPF BlazorWebView”).

All hosts render the same component: `DragDropShared.DragDropApp`.

## Scenarios (all on one page, with a live event log)

Each scenario maps to a symptom reported on the issue. The page shows a **live event log**
capturing both Blazor-level handlers and raw DOM events, so you can see exactly *which*
drag events fire on each platform (the core complaint is “`dragstart` fires but
`dragover`/`drop`/`dragend` never do”).

1. **Native HTML5 drag & drop** — the original VegasVault repro (draggable box → drop zone).
2. **Native sortable list** — reorder items via native DnD (MudBlazor DropZone / SortableJS style).
3. **Draggable image** — dragging an `<img>`; reported to leave an artifact / freeze / crash the app on Windows WebView2.
4. **External file drop** — drag a file from Explorer/Finder into the WebView; watch for `drop files=[…]`.
5. **Pointer-based sortable (workaround)** — reordering built on **pointer events**, not HTML5 DnD, so it works on every platform including Android/iOS. This is the recommended workaround.

## How to run

Prerequisites: .NET 10 SDK (`global.json` pins the 10.0.3xx band), the MAUI workload,
and the Edge WebView2 Runtime (already present on most Windows machines).

```powershell
# WPF (reporter's scenario)
dotnet run --project src/DragDropWpf

# Windows Forms
dotnet run --project src/DragDropWinForms

# MAUI on Windows
dotnet build src/DragDropMaui -f net10.0-windows10.0.19041.0 -t:Run

# MAUI on Android (device/emulator attached)
dotnet build src/DragDropMaui -f net10.0-android -t:Run
```

For iOS / Mac Catalyst you must build & run from a **Mac** (see “What we still need” below).

## Findings so far

### ✅ Build & launch status (verified on this Windows machine, .NET 10.0.302)

| Host | Builds | Launches | Notes |
|------|:------:|:--------:|-------|
| WPF | ✅ | ✅ | **Only after fixing the TFM — see Finding #1** |
| Windows Forms | ✅ | ✅ | Benign `MSB3277 WindowsBase` version-conflict warning (from the WebView2 WPF assembly) |
| MAUI (Windows) | ✅ | ✅ | |
| MAUI (Android) | ✅ | ⏳ | Not yet run — needs emulator/device |
| MAUI (iOS/Mac Catalyst) | ⏳ | ⏳ | Needs a Mac |

### 🔴 Finding #1 — WPF BlazorWebView crashes at startup when targeting `net10.0-windows` (no OS version)

Targeting the version-less TFM `net10.0-windows` makes the **WPF** host crash on the first
render with:

```
System.IO.FileNotFoundException: Could not load file or assembly
'Microsoft.Windows.SDK.NET, Version=10.0.17763.10, …'
   at Microsoft.Web.WebView2.Wpf.WebView2CompositionControl.TryInitializeD3DImage()
   at Microsoft.Web.WebView2.Wpf.WebView2CompositionControl.OnApplyTemplate()
```

The `WebView2CompositionControl` used by WPF needs the Windows SDK WinRT projections
(`Microsoft.Windows.SDK.NET`), which are only referenced when you target a Windows-SDK TFM.
**Fix:** target `net10.0-windows10.0.19041.0` (done in this repo). Windows Forms does *not*
hit this path and survives the version-less TFM, which makes the WPF failure easy to miss.
This is a plausible contributor to what the reporter is seeing — worth a docs/template note.

### ℹ️ Expected platform behavior (context for the issue)

- **Windows**: The historical WebView2/WinUI drag-and-drop bugs
  ([WebView2Feedback#2805](https://github.com/MicrosoftEdge/WebView2Feedback/issues/2805),
  [microsoft-ui-xaml#7366](https://github.com/microsoft/microsoft-ui-xaml/issues/7366)) are
  **both closed/fixed** via Windows App SDK 1.5/1.6+. Native DnD should now work on Windows.
- **Android / iOS**: Mobile WebViews (Chromium/WebKit) do **not** implement mouse-style HTML5
  drag-and-drop on touch. Scenarios 1–4 are expected to be limited there regardless of MAUI —
  scenario 5 (pointer-based) is the supported path.

*(Behavioral drag/drop results per scenario are filled in by manual UI testing — see below.)*

## What we still need (manual UI testing)

Drag-and-drop is a **human gesture**; it can’t be fully automated here, and this build
machine is a headless/virtual desktop (window renders but full-screen capture only shows the
wallpaper). To complete the verification we need, per host/platform, someone to:

1. Launch the app, then for **each scenario** try the drag interaction and record what the
   **event log** shows (which events fire) and whether the app freezes/crashes.
2. Specifically re-test the reporter’s case: **WPF on .NET 10**, dragging the box (scenario 1)
   and the **image** (scenario 3 — the crash repro).
3. For **iOS / Mac Catalyst**: build & run from a Mac (this machine is Windows-only).
4. For **Android**: run on an emulator or device.

A findings table to fill in during manual testing:

| Scenario | WPF | WinForms | MAUI Win | MAUI Android | MAUI iOS | MAUI Mac |
|----------|-----|----------|----------|--------------|----------|----------|
| 1 Native DnD | | | | | | |
| 2 Sortable | | | | | | |
| 3 Image drag (crash?) | | | | | | |
| 4 File drop | | | | | | |
| 5 Pointer (workaround) | | | | | | |
