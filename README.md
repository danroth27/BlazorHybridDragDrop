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
10.0.302. The Hybrid projects use the workload dependencies: Windows App SDK
`1.7.250909003` and WebView2 SDK `1.0.3179.45`. The plain reductions were additionally
retested with Windows App SDK `1.8.260710003` and WebView2 SDK `1.0.4129.50`.

| Scenario | WPF Hybrid | WinForms Hybrid | MAUI Windows | MAUI Android |
|---|---|---|---|---|
| Native element DnD | **Fails:** `dragstart`, `dragend`; no target events | **Works** | **Fails:** drag ends in 2–22 ms | **Works** |
| Native sortable | **Fails** | Native `drop` works; original sample rerender disrupted reorder | **Fails** | **Works** |
| Image drag | Preview appears only after leaving the WPF window; no crash | **Works**, no crash | **Fails:** ends immediately | `dragstart`/`dragend`, but no usable image drag |
| External file drop | **Fails:** disallowed cursor, no events | **Works** | **Works after setting `WebView2.AllowDrop=true`** | Not tested |
| Pointer workaround | **Works** | **Works** | **Works** | Original sample left floating items after `pointercancel`; now fixed |

### Conclusions from the Hybrid comparison

- The WPF report is confirmed with the correct versioned Windows TFM. The drag can start,
  but the WebView content never becomes a valid drop target.
- MAUI Windows native drags terminate almost immediately. External file drop works after
  opting in through the underlying WinUI `WebView2.AllowDrop` property.
- Windows Forms succeeds with the same Razor component. Blazor and the component are
  therefore not the common cause of the Windows failures.
- Current Android WebView supports the basic native element and sortable scenarios tested
  here, contrary to several older comments on the issue.
- iOS and Mac Catalyst remain untested.

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
| WinUI 3 `WebView2` | **Fails:** immediate `dragstart`/`dragend` | **Fails** | **Works** with `AllowDrop=true` |

These results are unchanged with WebView2 SDK `1.0.4129.50` and Windows App SDK
`1.8.260710003`.

Attempting a native drag in WinUI/MAUI Windows can leave stale drag visual state. A
subsequent external file drag may display the blue “drag me” preview at the pointer even
though the file drop succeeds.

### Ownership and current blockers

- WPF in-WebView HTML drag/drop is blocked by
  [WebView2Feedback#5237](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5237).
- WPF composition-control external drop is blocked by
  [WebView2Feedback#5124](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5124).
- WinUI in-WebView drag/drop and stale drag visuals are tracked by
  [microsoft-ui-xaml#9187](https://github.com/microsoft/microsoft-ui-xaml/issues/9187) and
  [microsoft-ui-xaml#10576](https://github.com/microsoft/microsoft-ui-xaml/issues/10576).
- MAUI can fix its external-file-drop behavior by setting `AllowDrop=true` when creating the
  WinUI WebView2, or applications can opt in through `BlazorWebViewInitialized` as this
  sample does.

No new external issue is required at this point; exact upstream reports are already open.

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

The project explicitly sets `AllowDrop=true`. This enables external file drops, but native
HTML drag/drop remains blocked by the open WinUI issues above.

## Remaining verification

- Test MAUI iOS and Mac Catalyst on a Mac.
- Retest the corrected Android pointer workaround.
- Decide whether to propose a MAUI change that enables `AllowDrop` by default on Windows.
