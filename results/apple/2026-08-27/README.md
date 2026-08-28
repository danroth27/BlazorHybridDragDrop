# Apple WKWebView drag-and-drop results — 2026-08-27

These traces compare the MAUI `BlazorWebView` with a plain `WKWebView` using the same
HTML5 drag scenarios.

## Environment

- macOS 15.7.5
- Xcode 26.3 (17C529)
- iOS SDK 26.2 (23C57)
- iOS Simulator runtime 26.3.1 (23D8133), iPhone 17 Pro
- .NET SDK 10.0.400
- .NET workload set 10.0.202
- `Microsoft.iOS` / `Microsoft.MacCatalyst` 26.2.10233
- Mac interaction was performed through Remote Desktop. Failed or intermittent mouse
  drag initiation should therefore not be treated as a local-hardware result.

Each NDJSON line contains the host and an unthrottled DOM event record:

```json
{"host":"MAUI BlazorWebView","event":{"sequence":1,"scenario":"s1-source","event":"dragstart"}}
```

## Results

| Platform / host | Element drop | Sortable | Image | External file |
|---|---|---|---|---|
| Mac Catalyst MAUI, no payload | Immediate `dragstart`/`dragend` | Immediate `dragstart`/`dragend` | Immediate `dragstart`/`dragend` | Full DOM `drop` |
| Mac Catalyst MAUI, `text/plain` payload | Full DOM `drop` | Intermittent: 1/7 drops in the initial run and 0/5 after removing drag-start styling | Sustained DOM drag; visible drag outside and back | Full DOM `drop` with `Files` and `LICENSE` |
| Mac Catalyst plain WKWebView, no payload | Immediate `dragstart`/`dragend` | Immediate `dragstart`/`dragend` | Not part of the no-payload A/B | Not part of the no-payload A/B |
| Mac Catalyst plain WKWebView, `text/plain` payload | Full DOM `drop` | Intermittent: 1/3 drops in the final A/B; deferring reorder produced 2/4 | Sustained DOM drag and `dragleave`, but no expected image preview was visible | Full DOM `drop` with `Files` and `LICENSE` |
| iOS MAUI, no payload | Immediate `dragstart`/`dragend` | Immediate `dragstart`/`dragend` | Not run in the baseline capture | Not run in the baseline capture |
| iOS MAUI, `text/plain` payload | Full DOM `drop` | Reliable: 5/5 after removing `-webkit-user-drag` from list items | DOM drag events fired, but no normal image drag was visible | No DOM events captured; manual transfer was intercepted by iOS |
| iOS plain WKWebView, no payload | Immediate `dragstart`/`dragend` | Immediate `dragstart`/`dragend` | Not part of the no-payload A/B | Not part of the no-payload A/B |
| iOS plain WKWebView, `text/plain` payload | Full DOM `drop` | Reliable: 5/5 in the A/B control | Long-press opened WebKit's image preview/context UI; dragging showed a snapshot-like visual | DOM `dragenter`/`dragover`/`dragleave`, then iOS offered to save to **On My iPhone**; no DOM `drop` |

## Conclusions

1. WebKit requires these custom element/list drags to contain usable data. In both MAUI and
   plain WKWebView on iOS, an empty data store produced only immediate
   `dragstart`/`dragend`; adding `text/plain` produced complete and reliable drops.
2. On Catalyst, the payload reliably fixed the blue element but was not sufficient for the
   sortable list. Both MAUI and plain WKWebView showed the same immediate cancellations,
   so the remaining list behavior is below Blazor. Remote Desktop remains a confounding
   input variable and requires local-console confirmation.
3. The strongest isolation is the plain iOS in-process A/B: with identical CSS, the
   no-payload element/list attempts ended immediately, then the payload-enabled element and
   5/5 list attempts completed. A separate MAUI run also completed the element and 5/5 list
   drops after `-webkit-user-drag` was removed from those elements. The committed sample
   exposes the image-specific property as a separate option.
4. Removing drag-start styling and deferring the plain host's reorder until after `drop`
   did not eliminate Catalyst intermittency. Successful drops still emitted `dragend`, so
   there is no evidence that one reorder permanently corrupts WebKit's drag state.
5. Image dragging remains platform-specific. iOS uses its image preview/context interaction
   rather than a normal browser-style image drag. Catalyst generated sustained DOM events in
   both hosts, but the visible preview differed between MAUI and the plain host.
6. Finder file drop works through WKWebView on Mac Catalyst in both hosts. The MAUI sample
   intentionally logs the filename without changing the target text; the plain host updates
   its target text.
7. Dragging a Mac file into an iPhone simulator is not a valid WKWebView file-drop test:
   CoreSimulator/iOS intercepts the transfer and opens the **On My iPhone** import UI.

## Raw traces

The `*-payload-ab.ndjson` files contain the definitive no-payload/payload controls; their
first element and list attempts have empty `types`, and later attempts contain
`text/plain`. The no-payload sample size is one attempt per scenario, consistent with the
independent MAUI baselines.

`plain-wkwebview-maccatalyst-text-payload.ndjson` and
`plain-wkwebview-ios-text-payload.ndjson` came from an earlier plain-host build that used
the scenario IDs `s1`/`s2`/`s3`/`s4`. Reliability conclusions use the later
`*-payload-ab.ndjson` traces, whose IDs match the committed host.

Image traces were collected with both the text payload and the image-specific
`-webkit-user-drag` option enabled. Their individual contribution was not isolated.

- `maui-maccatalyst-no-payload.ndjson`
- `maui-maccatalyst-text-payload.ndjson`
- `maui-maccatalyst-payload-no-start-mutation.ndjson`
- `plain-wkwebview-maccatalyst-payload-ab.ndjson`
- `plain-wkwebview-maccatalyst-deferred-reorder.ndjson`
- `plain-wkwebview-maccatalyst-text-payload.ndjson`
- `maui-ios-no-payload.ndjson`
- `maui-ios-text-payload.ndjson`
- `maui-ios-payload-no-user-drag-css.ndjson`
- `plain-wkwebview-ios-payload-ab.ndjson`
- `plain-wkwebview-ios-text-payload.ndjson`
