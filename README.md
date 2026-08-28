# Blazor Hybrid drag & drop — cross-host repro for [dotnet/maui#2205](https://github.com/dotnet/maui/issues/2205)

This repository compares the same drag-and-drop scenarios across Blazor Hybrid hosts and
plain native WebView controls. It includes the WPF .NET 10 configuration reported in
[issue comment 4827818805](https://github.com/dotnet/maui/issues/2205#issuecomment-4827818805).

## Projects

| Project | Purpose |
|---|---|
| `DragDropShared` | Shared `Routes`, MAUI template `MainLayout`, pages, static assets, event diagnostics, and pointer workaround |
| `DragDropWpf` | WPF Blazor Hybrid using `WebView2CompositionControl` |
| `DragDropWinForms` | Windows Forms Blazor Hybrid using standard WebView2 |
| `DragDropMaui` | MAUI Blazor Hybrid for Windows, Android, iOS, and Mac Catalyst |
| `DragDropNativeAndroid` | Plain native Android WebView comparison with no MAUI or Blazor |
| `DragDropNativeWKWebView` | Plain UIKit `WKWebView` comparison for iOS and Mac Catalyst, with no MAUI or Blazor |
| `DragDropWpfWebView2` | Plain WPF comparison: standard `WebView2` and `WebView2CompositionControl` in separate tabs |
| `DragDropWinUIWebView2` | Plain WinUI 3 WebView2 comparison with no MAUI or Blazor |
| `NativeMobileWebViewShared` | Static diagnostic page shared by the Android WebView and Apple WKWebView reductions |
| `NativeWebViewShared` | Static HTML used by the plain WebView2 reductions |

MAUI, WPF, and Windows Forms all bootstrap `DragDropShared.Routes` into their host page's
`#app` element. The shared RCL owns the routed page, the original MAUI purple-gradient
layout, Bootstrap/global CSS, and the Blazor error UI. The native `index.html` files only
load shared assets and `blazor.webview.js`.

The plain reductions intentionally contain no Blazor code. They determine whether a failure
belongs to Blazor Hybrid or to the underlying native WebView control.

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

| Scenario | WPF Hybrid | WinForms Hybrid | MAUI Windows | MAUI Android | MAUI iOS | MAUI Mac Catalyst |
|---|---|---|---|---|---|---|
| Native element DnD | **Fails:** `dragstart`, `dragend`; no target events | **Works** | **Fails with Windows App SDK 1.7; works with 2.x and `AllowDrop=true`** | **Works** | **Works with [`dataTransfer` seeded in `dragstart`](https://bugs.webkit.org/show_bug.cgi?id=265857); otherwise ends immediately** | **Works with seeded `dataTransfer`; otherwise ends immediately** |
| Native sortable | **Fails** | Native `drop` works; original sample rerender disrupted reorder | **Fails with Windows App SDK 1.7; works with 2.x and `AllowDrop=true`** | **Works** | **Works reliably with seeded `dataTransfer`** | **Intermittent with seeded `dataTransfer` in both MAUI and plain WKWebView** |
| Image drag | Preview appears only after leaving the WPF window; no crash | **Works**, no crash | **Fails with Windows App SDK 1.7; works with 2.x and `AllowDrop=true`** | No usable visual drag | WebKit image preview/context behavior, not a normal image drag | Sustained DOM drag; visible preview differed between MAUI and plain WKWebView |
| External file drop | **Fails:** disallowed cursor, no events | **Works** | **Works after setting `WebView2.AllowDrop=true`** | **Works from Android Files in split screen** | iPhone Simulator intercepts Mac file transfer before DOM `drop` | **Works from Finder** |
| Pointer workaround | **Works** | **Works** | **Works** | **Works after `pointercancel` cleanup fix** | **Works** | **Works** |

### Conclusions from the Hybrid comparison

- The WPF report is confirmed: its composition control never becomes a valid drop target,
  while Windows Forms succeeds with the same Razor component. Blazor is not the common
  cause.
- MAUI Windows supports all five scenarios with Windows App SDK 2.x and
  `WebView2.AllowDrop=true`. The released Windows App SDK 1.7 setup still fails native
  in-page drag.
- MAUI and plain Android WebView both support native element drop, sortable reorder, and
  external file drop from Android Files. Both lack a usable image-drag visual, assigning
  that remaining limitation to Android WebView rather than MAUI or Blazor.
- On iOS, both MAUI BlazorWebView and plain WKWebView cancel custom element/list drags when
  `dataTransfer` is empty. Enabling **Seed text/plain in dragstart** produced 5/5
  successful sortable drops without `-webkit-user-drag`.
- On Mac Catalyst, seeding `dataTransfer` reliably fixes the blue element, but sortable
  drags remain intermittent in both hosts. Removing drag-start styling and deferring the
  plain DOM reorder did not remove the immediate cancellations. Because testing used Remote
  Desktop, this needs confirmation from a local console before assigning it to WebKit.
- Finder file drop reaches both the DOM and Blazor on Mac Catalyst. The iPhone simulator
  intercepts Mac file transfers and offers to save them to **On My iPhone**, so it cannot
  validate external file drop into WKWebView.
- Apple event summaries, representative sequences, and environment details are in
  [`results/apple/2026-08-27`](results/apple/2026-08-27/README.md).
- Android comparison details are in
  [`results/android/2026-08-28`](results/android/2026-08-28/README.md).

### Apple WebKit empty drag data behavior

The original scenarios did not put data into the drag data store. Earlier Android WebView
and WinForms WebView2 runs accepted that, but Apple WebKit terminates these custom drags
almost immediately:

```javascript
element.addEventListener("dragstart", event => {
    event.dataTransfer.setData("text/plain", element.textContent);
});
```

WebKit [#265857](https://bugs.webkit.org/show_bug.cgi?id=265857) tracks the same empty-store
symptom in WebKitGTK. Because the HTML specification initializes an empty drag data store
without requiring applications to populate it, seeding data should be treated as a WebKit
compatibility requirement rather than a standards requirement.

The sample exposes this as **Seed text/plain in dragstart** so the empty-store baseline and
the WebKit-compatible behavior can be compared without changing code. Non-Apple rows in the
table were collected with the option disabled.

The separate **Apply -webkit-user-drag: element to the image** option reproduces the image
configuration used for the Apple image traces without changing the element or sortable
scenarios.

The plain `DragDropNativeWKWebView` host records every DOM drag event to
`Documents/dragdrop-dom-events.ndjson`. The MAUI host installs the same Apple-only logging
bridge through `BlazorWebViewInitializing`.

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
| Android WebView 145 | **Works**, including sortable reorder | Sustained DOM events, but no usable visual drag | **Works** from Android Files |
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
- WebKit [#265857](https://bugs.webkit.org/show_bug.cgi?id=265857) tracks the same empty
  `DataTransfer` failure observed on Apple platforms, although that report is scoped to
  WebKitGTK.
- Plain Android WebView reproduces MAUI Android's image-drag limitation, while element,
  sortable, and external file drops work in both hosts. No MAUI-specific Android defect was
  found.
- The MAUI Windows external-file-drop fix is tracked by
  [dotnet/maui#37903](https://github.com/dotnet/maui/issues/37903). Merged
  [dotnet/maui#37904](https://github.com/dotnet/maui/pull/37904) sets `AllowDrop=true`
  when the handler creates its WinUI WebView2 and adds a Windows device test. It merged
  into `inflight/current` for .NET 10 SR11.
- The `net11.0` branch already uses Windows App SDK `2.3.1` through
  [dotnet/maui#36891](https://github.com/dotnet/maui/pull/36891). #37904 is expected to
  reach `net11.0` through the normal `inflight/current` → `main` → `net11.0` branch flow.
- The #37904 packages were validated in this sample with Windows App SDK 2.0.1 and no
  app-level workaround. All five scenarios worked without stale visuals, freezes, or
  crashes.
- Until that change ships, applications can opt in through `BlazorWebViewInitialized`, as
  this sample does.

The known WebView2, WinUI, and WebKit behaviors all have upstream tracking links above.

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

# Plain native Android WebView
dotnet build src\DragDropNativeAndroid -t:Run
```

For Android external file drop, put the app and Android Files in split screen, open
**Downloads**, and drag a file onto the external-file target. The plain host writes its
complete DOM event log to
`/storage/emulated/0/Android/data/com.companyname.DragDropNativeAndroid/files/dragdrop-dom-events.ndjson`.

### Apple hosts

```bash
# MAUI Mac Catalyst
dotnet build src/DragDropMaui -f net10.0-maccatalyst \
  -p:RuntimeIdentifier=maccatalyst-arm64 -t:Run

# Plain WKWebView Mac Catalyst
dotnet build src/DragDropNativeWKWebView -f net10.0-maccatalyst \
  -p:RuntimeIdentifier=maccatalyst-arm64 -t:Run

# Build MAUI for the arm64 iOS simulator. SkipMauiAppIcon works around an
# Xcode 26.3 SDK / iOS 26.3.1 simulator actool mismatch on this test host.
dotnet build src/DragDropMaui -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64 -p:SkipMauiAppIcon=true

# Plain WKWebView iOS simulator
dotnet build src/DragDropNativeWKWebView -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64
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

- Confirm Catalyst mouse-drag reliability on a local console rather than Remote Desktop.
- Test iOS external file drop from another iPad app or a physical device; Mac-to-iPhone
  Simulator transfer is intercepted by CoreSimulator/iOS.
- Reduce the Catalyst image-preview difference: both hosts emit sustained DOM drag events,
  but only the MAUI run showed the expected image visual outside the web view.
- Verify that the normal branch flow carries #37904 into `net11.0`, then validate it
  against that branch's Windows App SDK 2.3.1 dependency.
- Track the upstream WPF composition-control issues linked above.
