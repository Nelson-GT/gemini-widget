using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace GeminiWidget;

public partial class LoginWindow : Window
{
    private readonly CoreWebView2Environment _environment;
    private readonly string _startUrl;
    private readonly string _successUrlPrefix;
    private bool _loginCompleted;

    public LoginWindow(CoreWebView2Environment environment, string startUrl, string successUrlPrefix)
    {
        InitializeComponent();

        _environment = environment;
        _startUrl = startUrl;
        _successUrlPrefix = successUrlPrefix;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async(_environment);

        var core = WebView.CoreWebView2;
        core.Settings.UserAgent = GeminiScraperService.UserAgent;

        // Los flujos de OAuth de Google pueden abrir ventanas nuevas: las reutilizamos.
        core.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            core.Navigate(args.Uri);
        };

        core.NavigationCompleted += OnNavigationCompleted;
        core.Navigate(_startUrl);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_loginCompleted)
            return;

        var url = WebView.Source?.ToString() ?? string.Empty;
        if (url.StartsWith(_successUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _loginCompleted = true;
            DialogResult = true;
            Close();
        }
    }
}
