using Foundation;
using UIKit;

namespace DragDropNativeWKWebView;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;

	public override UISceneConfiguration GetConfiguration(
		UIApplication application,
		UISceneSession connectingSceneSession,
		UISceneConnectionOptions options) =>
		UISceneConfiguration.Create("Default Configuration", connectingSceneSession.Role);
}
