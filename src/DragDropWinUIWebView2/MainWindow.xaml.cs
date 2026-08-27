using System.IO;
using Microsoft.UI.Xaml;

namespace DragDropWinUIWebView2;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        WebView.Source = CreatePageUri(page, "WinUI 3 WebView2 — Windows App SDK 2.0.1");
    }

    private void AllowDropToggle_Toggled(object sender, RoutedEventArgs e)
    {
        WebView.AllowDrop = AllowDropToggle.IsOn;
    }

    private static Uri CreatePageUri(string page, string host)
    {
        var builder = new UriBuilder(page)
        {
            Query = $"host={Uri.EscapeDataString(host)}",
        };

        return builder.Uri;
    }
}
