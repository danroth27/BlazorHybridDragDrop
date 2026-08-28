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

## Ownership conclusion

Native element drop, sortable reorder, and external file drop work in both hosts. The
unusable image drag also reproduces in the plain Android WebView, so it is not caused by
MAUI or Blazor. The plain host emitted sustained image drag events even though it did not
show a usable drag preview, which narrows the remaining gap to Android WebView's image-drag
interaction and feedback.
