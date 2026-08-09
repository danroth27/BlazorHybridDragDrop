using System.IO;
using Microsoft.UI.Xaml;

namespace DragDropWinUIWebView2;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        var builder = new UriBuilder(page)
        {
            Query = $"host={Uri.EscapeDataString("WinUI 3 WebView2")}",
        };
        WebView.Source = builder.Uri;
    }
}
