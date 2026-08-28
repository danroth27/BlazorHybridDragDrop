using Android.Util;
using Android.Webkit;
using Java.Interop;

namespace DragDropNativeAndroid;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private WebView? _webView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SetContentView(Resource.Layout.activity_main);

        _webView = FindViewById<WebView>(Resource.Id.webview)
            ?? throw new InvalidOperationException("The WebView was not found.");

        _webView.Settings.JavaScriptEnabled = true;
        _webView.Settings.DomStorageEnabled = true;
        _webView.Settings.AllowFileAccess = true;
        _webView.Settings.AllowContentAccess = true;
        _webView.SetWebViewClient(new WebViewClient());
        _webView.SetWebChromeClient(new DragWebChromeClient());
        _webView.AddJavascriptInterface(new DragLogBridge(this), "dragLog");
        WebView.SetWebContentsDebuggingEnabled(true);
        _webView.LoadUrl("file:///android_asset/index.html?host=Plain%20Android%20WebView");
    }

    protected override void OnDestroy()
    {
        _webView?.Destroy();
        _webView = null;
        base.OnDestroy();
    }

    private sealed class DragLogBridge : Java.Lang.Object
    {
        private readonly string _logPath;
        private readonly Lock _lock = new();

        public DragLogBridge(Android.Content.Context context)
        {
            var logDirectory = context.GetExternalFilesDir(null)?.AbsolutePath
                ?? context.FilesDir?.AbsolutePath
                ?? throw new InvalidOperationException("No application files directory is available.");

            _logPath = Path.Combine(logDirectory, "dragdrop-dom-events.ndjson");
            File.WriteAllText(_logPath, string.Empty);
            Log.Info("DragDrop", $"DOM event log: {_logPath}");
        }

        [JavascriptInterface]
        [Export("postMessage")]
        public void PostMessage(string json)
        {
            var entry = $"{{\"host\":\"Plain Android WebView\",\"event\":{json}}}";
            lock (_lock)
            {
                File.AppendAllText(_logPath, entry + Environment.NewLine);
            }

            Log.Info("DragDrop", entry);
        }
    }

    private sealed class DragWebChromeClient : WebChromeClient
    {
        public override bool OnConsoleMessage(ConsoleMessage? consoleMessage)
        {
            if (consoleMessage?.Message() is { } message)
            {
                Log.Debug("DragDrop", message);
            }

            return base.OnConsoleMessage(consoleMessage);
        }
    }
}