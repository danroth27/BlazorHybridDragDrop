using Foundation;
using CoreGraphics;
using UIKit;
using WebKit;

namespace DragDropNativeWKWebView;

[Register("SceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
	private readonly DragLogHandler _dragLogHandler = new("Plain WKWebView");

	[Export("window")]
	public UIWindow? Window { get; set; }

	[Export("scene:willConnectToSession:options:")]
	public void WillConnect(
		UIScene scene,
		UISceneSession session,
		UISceneConnectionOptions connectionOptions)
	{
		if (scene is not UIWindowScene windowScene)
		{
			throw new InvalidOperationException("The application scene is not a window scene.");
		}

		var configuration = new WKWebViewConfiguration();
		configuration.UserContentController.AddScriptMessageHandler(_dragLogHandler, "dragLog");

		var viewController = new UIViewController();
		var webView = new WKWebView(CoreGraphics.CGRect.Empty, configuration)
		{
			TranslatesAutoresizingMaskIntoConstraints = false
		};

		var htmlPath = NSBundle.MainBundle.PathForResource("index", "html")
			?? throw new InvalidOperationException("The bundled index.html file was not found.");
		var htmlUrl = NSUrl.FromFilename(htmlPath);
		webView.LoadFileUrl(htmlUrl, htmlUrl.RemoveLastPathComponent());

		viewController.View!.AddSubview(webView);
		NSLayoutConstraint.ActivateConstraints(
		[
			webView.LeadingAnchor.ConstraintEqualTo(viewController.View.LeadingAnchor),
			webView.TrailingAnchor.ConstraintEqualTo(viewController.View.TrailingAnchor),
			webView.TopAnchor.ConstraintEqualTo(viewController.View.TopAnchor),
			webView.BottomAnchor.ConstraintEqualTo(viewController.View.BottomAnchor)
		]);

		CGRect windowFrame;
		if (OperatingSystem.IsIOSVersionAtLeast(26) ||
			OperatingSystem.IsMacCatalystVersionAtLeast(26))
		{
			windowFrame = windowScene.EffectiveGeometry.CoordinateSpace.Bounds;
		}
		else
		{
			windowFrame = windowScene.CoordinateSpace.Bounds;
		}

		Window = new UIWindow(windowScene)
		{
			Frame = windowFrame,
			RootViewController = viewController
		};
		Window.MakeKeyAndVisible();
	}

	private sealed class DragLogHandler : NSObject, IWKScriptMessageHandler
	{
		private readonly string _host;
		private readonly string _logPath;
		private readonly Lock _lock = new();

		public DragLogHandler(string host)
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
}
