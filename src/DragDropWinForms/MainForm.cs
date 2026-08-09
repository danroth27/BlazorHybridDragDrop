using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using DragDropShared;

namespace DragDropWinForms;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "Drag & Drop — WinForms Blazor Hybrid";
        Width = 720;
        Height = 860;

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddSingleton(new DragDropHostInfo("WinForms (Blazor Hybrid)"));
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        var blazor = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot\\index.html",
            Services = services.BuildServiceProvider(),
        };
        blazor.RootComponents.Add<Routes>("#app");
        Controls.Add(blazor);
    }
}
