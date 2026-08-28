# Android WebView drag-and-drop investigation

## Environment

- Host: Windows
- Device: Pixel 7 API 35 Android emulator
- .NET SDK: `10.0.400`
- Android System WebView: `145.0.7632.159`
- Test hosts:
  - MAUI `BlazorWebView`
  - Plain native Android `WebView` with no MAUI or Blazor
- Native test options: **Seed text/plain in dragstart** off;
  **Apply -webkit-user-drag: element to the image** off

## Results

### Baseline without `DropDataContentProvider`

| Scenario | MAUI BlazorWebView | Plain Android WebView |
|---|---|---|
| Native element drop | **Works** | **Works**; target receives `drop` |
| Native sortable | **Works** | **Works** across repeated reorders |
| Image drag | No usable visual drag | Sustained DOM drag events, but no usable visual drag |
| External file drop | **Works** from Android Files | **Works** from Android Files |
| Pointer sortable | **Works** | Not applicable |

The external-file test used Android Files and the WebView host in split screen. Dropping
`dragdrop-test.txt` into the plain WebView produced:

```text
dragenter types=[text/plain]
dragover types=[text/plain]
drop types=[Files] files=[dragdrop-test.txt]
```

The same file drop reached the MAUI page's DOM and Blazor handlers.

## Image drag host requirement

Google documents image drag-out from Android WebView as requiring AndroidX WebKit's
[`DropDataContentProvider`](https://developer.android.com/reference/androidx/webkit/DropDataContentProvider)
in the application manifest. The initial MAUI and plain-host tests did not reference
`Xamarin.AndroidX.WebKit` or declare that provider.

Chromium's current Android drag implementation explicitly declines to start an image drag
in WebView when the provider cannot supply shareable image data. The broad Android WebView
drag-and-drop work is tracked by
[Chromium #40235067](https://issues.chromium.org/issues/40235067). The invalid-image drag
shadow fallback is associated with
[Chromium #40826511](https://issues.chromium.org/issues/40826511), formerly crbug #1304433.

### Provider-enabled comparison

Both hosts were retested with `Xamarin.AndroidX.WebKit` `1.14.0.1` and the documented
provider declaration:

```xml
<provider
  android:name="androidx.webkit.DropDataContentProvider"
  android:authorities="<application-id>.DropDataProvider"
  android:exported="false"
  android:grantUriPermissions="true" />
```

Version `1.14.0.1` was selected because its AndroidX Core `1.16.0.3` dependency matches
the .NET 10 MAUI dependency graph. The latest binding tested, `1.16.0.1`, pulled AndroidX
Core `1.19.0.1` and caused duplicate AndroidX Core classes during D8 in the MAUI project.

| Provider-enabled image test | MAUI BlazorWebView | Plain Android WebView |
|---|---|---|
| Visible drag preview | **Works** | **Works** |
| Drop onto the page's file target | **Works:** `drop files=[download.svg]` | **Works** |
| Drag into Android Files | Preview appears, but Files reports "You can't move files from another app" | Same rejection |

The MAUI DOM log for the successful in-page drop included:

```text
[s3-img] dragstart files=[svg>]
[s4-filedrop] dragenter
[s4-filedrop] dragover
[s4-filedrop] drop files=[download.svg]
[Blazor] s4 ondrop (file names in JS log)
[s3-img] dragend
```

The page does not render the dropped filename, but the DOM and Blazor handlers both
received the drop. Android Files' cross-app rejection is not MAUI-specific: the same
provider-backed SVG and message occurred with the plain Android WebView. The provider
supports reading dragged image content, while its implementation does not support delete
or update operations, so a target that treats the operation as a move cannot complete it.

## Ownership conclusion

Native element drop, sortable reorder, and external file drop work in both hosts. The
baseline image failure was caused by missing AndroidX WebKit host configuration, not
Blazor or a MAUI drag implementation. Adding `DropDataContentProvider` produces equivalent
working image drag behavior in MAUI and the plain WebView. Applications that need Android
image dragging must currently reference a compatible AndroidX WebKit binding and declare
the provider. Dragging the provider-backed image into Android Files remains limited by the
destination's attempted move semantics, but dropping the image inside the WebView works.
