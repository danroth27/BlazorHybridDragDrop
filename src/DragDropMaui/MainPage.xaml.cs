using Microsoft.AspNetCore.Components.WebView;

#if IOS || MACCATALYST
using Foundation;
using WebKit;
#endif

namespace DragDropMaui;

public partial class MainPage : ContentPage
{
#if IOS || MACCATALYST
	private AppleDragLogHandler? _dragLogHandler;
	private WKUserContentController? _userContentController;
#endif

	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnHandlerChanging(HandlerChangingEventArgs args)
	{
#if IOS || MACCATALYST
		if (args.NewHandler is null)
		{
			_userContentController?.RemoveScriptMessageHandler("dragLog");
			_userContentController = null;
			_dragLogHandler?.Dispose();
			_dragLogHandler = null;
		}
#endif
		base.OnHandlerChanging(args);
	}

	private void OnBlazorWebViewInitializing(object? sender, BlazorWebViewInitializingEventArgs e)
	{
#if IOS || MACCATALYST
		_userContentController = e.Configuration.UserContentController;
		_userContentController.RemoveScriptMessageHandler("dragLog");
		_dragLogHandler ??= new("MAUI BlazorWebView");
		_userContentController.AddScriptMessageHandler(_dragLogHandler, "dragLog");
#endif
	}

	private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
	{
#if WINDOWS
		// WinUI WebView2 rejects external file drops unless the host opts in.
		e.WebView.AllowDrop = true;
#endif
	}

#if IOS || MACCATALYST
	private sealed class AppleDragLogHandler : NSObject, IWKScriptMessageHandler
	{
		private readonly string _host;
		private readonly string _logPath;
		private readonly Lock _lock = new();

		public AppleDragLogHandler(string host)
		{
			_host = host;
			_logPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				"dragdrop-dom-events.ndjson");
			File.WriteAllText(_logPath, string.Empty);
			Console.WriteLine($"[DragDrop] DOM event log: {_logPath}");
		}

		public void DidReceiveScriptMessage(
			WKUserContentController userContentController,
			WKScriptMessage message)
		{
			if (message.Body is not NSString json)
			{
				Console.Error.WriteLine(
					$"[DragDrop] Ignored non-string DOM log message of type " +
					$"{message.Body?.GetType().FullName ?? "null"}.");
				return;
			}

			var encodedHost = System.Text.Json.JsonEncodedText.Encode(_host);
			var entry = $"{{\"host\":\"{encodedHost}\",\"event\":{json}}}";
			lock (_lock)
			{
				File.AppendAllText(_logPath, entry + Environment.NewLine);
			}
			Console.WriteLine($"[DragDrop DOM] {entry}");
		}
	}
#endif
}
