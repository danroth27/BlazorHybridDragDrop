using Microsoft.AspNetCore.Components.WebView;

namespace DragDropMaui;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
	{
#if WINDOWS
		// WinUI WebView2 rejects external file drops unless the host opts in.
		e.WebView.AllowDrop = true;
#endif
	}
}
