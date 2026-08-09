using System.Windows;
using DragDropShared;
using Microsoft.Extensions.DependencyInjection;

namespace DragDropWpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddSingleton(new DragDropHostInfo("WPF (Blazor Hybrid)"));
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        Resources.Add("services", services.BuildServiceProvider());
        InitializeComponent();
    }
}
