using System.IO;
using System.Windows;

namespace DragDropWpfWebView2;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        StandardWebView.Source = CreateUri(page, "WPF standard WebView2");
        CompositionWebView.Source = CreateUri(page, "WPF WebView2CompositionControl");
    }

    private static Uri CreateUri(string page, string host)
    {
        var builder = new UriBuilder(page)
        {
            Query = $"host={Uri.EscapeDataString(host)}",
        };
        return builder.Uri;
    }
}
