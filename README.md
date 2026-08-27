# Blazor Hybrid drag & drop — cross-host repro for [dotnet/maui#2205](https://github.com/dotnet/maui/issues/2205)

This repository compares the same drag-and-drop scenarios across Blazor Hybrid hosts and
plain WebView2 controls. It includes the WPF .NET 10 configuration reported in
[issue comment 4827818805](https://github.com/dotnet/maui/issues/2205#issuecomment-4827818805).

## Projects

| Project | Purpose |
|---|---|
| `DragDropShared` | Shared `Routes`, MAUI template `MainLayout`, pages, static assets, event diagnostics, and pointer workaround |
| `DragDropWpf` | WPF Blazor Hybrid using `WebView2CompositionControl` |
| `DragDropWinForms` | Windows Forms Blazor Hybrid using standard WebView2 |
| `DragDropMaui` | MAUI Blazor Hybrid for Windows, Android, iOS, and Mac Catalyst |
| `DragDropWpfWebView2` | Plain WPF comparison: standard `WebView2` and `WebView2CompositionControl` in separate tabs |
| `DragDropWinUIWebView2` | Plain WinUI 3 WebView2 comparison with no MAUI or Blazor |
| `NativeWebViewShared` | Static HTML used by the plain WebView2 reductions |

MAUI, WPF, and Windows Forms all bootstrap `DragDropShared.Routes` into their host page's
`#app` element. The shared RCL owns the routed page, the original MAUI purple-gradient
layout, Bootstrap/global CSS, and the Blazor error UI. The native `index.html` files only
load shared assets and `blazor.webview.js`.

The plain reductions intentionally contain no Blazor code. They determine whether a failure
belongs to Blazor Hybrid or to the underlying WebView2 host control.

## Blazor scenarios

1. Original native HTML5 draggable element and drop zone.
2. Native sortable list.
3. Draggable image, including the historical artifact/freeze/crash report.
4. External file drop from Explorer/Finder.
5. Pointer-event sortable workaround.

The event log records raw DOM events and Blazor callbacks. High-frequency `drag` and
`dragover` events are throttled to avoid changing the behavior being measured.

## Verified results

Manual verification was performed on Windows and a Pixel 7 Android emulator using .NET
10.0.302. David Ortinau additionally tested the five scenarios on iOS Simulator and Mac
Catalyst. The Hybrid projects use the workload dependencies: Windows App SDK
`1.7.250909003` and WebView2 SDK `1.0.3179.45`. The plain reductions were additionally
retested with Windows App SDK `1.8.260710003` and `2.0.1`, WebView2 SDK
`1.0.4129.50`, and WebView2 Runtime `151.0.4129.107`.

| Scenario | WPF Hybrid | WinForms Hybrid | MAUI Windows | MAUI Android | MAUI iOS / Mac Catalyst |
|---|---|---|---|---|---|
| Native element DnD | **Fails:** `dragstart`, `dragend`; no target events | **Works** | **Fails:** drag ends in 2–22 ms | **Works** | **Fails** |
| Native sortable | **Fails** | Native `drop` works; original sample rerender disrupted reorder | **Fails** | **Works** | **Fails** |
| Image drag | Preview appears only after leaving the WPF window; no crash | **Works**, no crash | **Fails:** ends immediately | `dragstart`/`dragend`, but no usable image drag | **Fails** |
| External file drop | **Fails:** disallowed cursor, no events | **Works** | **Works after setting `WebView2.AllowDrop=true`** | Not tested | **Fails in reported verification** |
| Pointer workaround | **Works** | **Works** | **Works** | **Works after `pointercancel` cleanup fix** | **Works** |

### Conclusions from the Hybrid comparison

- The WPF report is confirmed with the correct versioned Windows TFM. The drag can start,
  but the WebView content never becomes a valid drop target.
- MAUI Windows native drags terminate almost immediately. External file drop works after
  opting in through the underlying WinUI `WebView2.AllowDrop` property.
- Windows Forms succeeds with the same Razor component. Blazor and the component are
  therefore not the common cause of the Windows failures.
- Current Android WebView supports native element drop and sortable reorder in this sample,
  contrary to several older comments on the issue. Image drag remains unusable.
- On iOS Simulator and Mac Catalyst, only the pointer-event implementation in scenario 5
  worked in the reported verification. Detailed per-event logs have not yet been collected.

## Additional findings and sample fixes

### WPF requires a versioned Windows TFM

`net10.0-windows` builds but crashes on first render:

```text
System.IO.FileNotFoundException: Could not load file or assembly
'Microsoft.Windows.SDK.NET, Version=10.0.17763.10, ...'
   at Microsoft.Web.WebView2.Wpf.WebView2CompositionControl.TryInitializeD3DImage()
```

Target `net10.0-windows10.0.19041.0` instead.

### Windows Forms build warning

The Windows Forms project builds and runs, but `Microsoft.Web.WebView2` transitively includes
its WPF assembly and produces `MSB3277` for `WindowsBase` versions 4.0 and 5.0.

### UI and instrumentation corrections

- MAUI, WPF, and Windows Forms now render the same shared `Routes` and original MAUI
  `MainLayout`; no native host owns a duplicate Razor layout or page.
- The shared layout owns `#blazor-error-ui`, which remains hidden unless Blazor reports a
  real unhandled error.
- Raw `drag`/`dragover` logging is throttled, and sortable list items are keyed to avoid
  diagnostic rerenders disrupting a drag.
- The pointer workaround handles `pointercancel` and disables touch panning while dragging,
  preventing floating items on Android.

## Plain-control ownership results

| Control | Native element DnD | Image drag | External file drop |
|---|---|---|---|
| WPF standard `WebView2` | **Works** | **Works** | **Works** |
| WPF `WebView2CompositionControl` | **Fails:** immediate `dragstart`/`dragend` | **Fails** | **Fails**, even with `AllowExternalDrop=true` |
| WinUI 3 `WebView2`, Windows App SDK 1.8, `AllowDrop=true` | **Fails:** immediate `dragstart`/`dragend` | **Fails** | **Works** |
| WinUI 3 `WebView2`, Windows App SDK 2.0.1, `AllowDrop=false` | **Fails:** immediate `dragstart`/`dragend` | Preview appears only outside the app | **Fails** |
| WinUI 3 `WebView2`, Windows App SDK 2.0.1, `AllowDrop=true` | **Works** | **Works:** preview appears inside and outside the WebView | **Works** |

Windows App SDK 2.0.1 adds the missing WinUI WebView2 drag support, but the WebView2 must
also have `AllowDrop=true`. With that combination, the native element reached its HTML
drop target, the image preview rendered both inside and outside the WebView, and an
external file reached the HTML drop handler. No stale drag preview remained.

On Windows App SDK 1.8, attempting a native drag in WinUI/MAUI Windows can leave stale
drag visual state. A subsequent external file drag may display the blue “drag me” preview
at the pointer even though the file drop succeeds.

### Ownership and current blockers

- WPF in-WebView HTML drag/drop is blocked by
  [WebView2Feedback#5237](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5237).
- WPF composition-control external drop is blocked by
  [WebView2Feedback#5124](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5124).
- The Windows App SDK 1.8 WinUI in-WebView failures and stale drag visuals are tracked by
  [microsoft-ui-xaml#9187](https://github.com/microsoft/microsoft-ui-xaml/issues/9187) and
  [microsoft-ui-xaml#10576](https://github.com/microsoft/microsoft-ui-xaml/issues/10576).
  The Windows App SDK 2.0.1 verification indicates these scenarios now work when
  `AllowDrop=true`.
- The MAUI Windows external-file-drop fix is tracked by
  [dotnet/maui#37903](https://github.com/dotnet/maui/issues/37903). Merged
  [dotnet/maui#37904](https://github.com/dotnet/maui/pull/37904) sets `AllowDrop=true`
  when the handler creates its WinUI WebView2 and adds a Windows device test. That PR
  merged into `inflight/current`. The full `maui-pr` pipeline is green, and the Windows
  BlazorWebView device suite passed locally with 18 passed, 4 skipped, and 0 failed tests.
- The `net11.0` branch already uses Windows App SDK `2.3.1` through
  [dotnet/maui#36891](https://github.com/dotnet/maui/pull/36891), but does not yet contain
  the #37904 `AllowDrop=true` handler change.
- The merged #37904 packages were validated in this MAUI Blazor Hybrid sample with
  Windows App SDK 2.0.1 and no app-level `AllowDrop` workaround. All five scenarios worked:

  | Scenario | MAUI Windows, #37904 + Windows App SDK 2.0.1 |
  |---|---|
  | Native element DnD | **Works:** DOM and Blazor `drop` events fire |
  | Native sortable | **Works:** repeated reorders succeed |
  | Image drag | **Works:** preview appears inside and outside the window |
  | External file drop | **Works:** `drop files=[test.txt]` and Blazor `ondrop` fire |
  | Pointer workaround | **Works** |

  The validation used MAUI package `10.0.110-ci.pr37904.26427.71` and WebView2 Runtime
  `151.0.4129.107`. No stale visuals, freezes, or crashes were observed.
- Until that change ships, applications can opt in through `BlazorWebViewInitialized`, as
  this sample does.

No new upstream dependency issue is required at this point; the exact WebView2 and WinUI
reports are already open.

## Run the Hybrid hosts

```powershell
# WPF (the reported scenario)
dotnet run --project src\DragDropWpf

# Windows Forms
dotnet run --project src\DragDropWinForms

# MAUI Windows
dotnet build src\DragDropMaui -f net10.0-windows10.0.19041.0 -t:Run

# MAUI Android (with an emulator/device)
dotnet build src\DragDropMaui -f net10.0-android -t:Run
```

## Run the plain WebView2 ownership reductions

### WPF

```powershell
dotnet run --project src\DragDropWpfWebView2
```

Test both tabs:

1. `Standard WebView2`
2. `WebView2CompositionControl` — the control used by WPF `BlazorWebView`

For each tab, test native element drag/drop, image dragging, and file drop. If only the
composition control fails, the WPF issue is isolated to WebView2's composition control.

### WinUI 3

The Windows App SDK PRI build tasks require Visual Studio MSBuild in this environment:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  src\DragDropWinUIWebView2\DragDropWinUIWebView2.csproj `
  /restore /t:Build /p:Configuration=Debug /p:Platform=x64

.\src\DragDropWinUIWebView2\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\DragDropWinUIWebView2.exe
```

The project uses Windows App SDK 2.0.1 and exposes a `WebView2.AllowDrop` toggle. Test the
native element, image, and external-file scenarios with the toggle off, then clear the log,
turn it on, and repeat. All three scenarios require `AllowDrop=true`; native in-page drag
also requires Windows App SDK 2.0 or later.

## Remaining verification

- Collect detailed event logs for scenarios 1–4 on iOS and Mac Catalyst, especially the
  Mac Catalyst external-file-drop behavior.
- Forward-port the #37904 `AllowDrop=true` handler change to `net11.0` and validate it
  against that branch's Windows App SDK 2.3.1 dependency.
- Track the upstream WPF composition-control issues linked above.
