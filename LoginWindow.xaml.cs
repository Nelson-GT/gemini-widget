using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace GeminiWidget;

public partial class LoginWindow : Window
{
    private readonly CoreWebView2Environment _environment;
    private readonly string _startUrl;
    private readonly string _successUrlPrefix;
    private bool _sawAccountsPage;
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

    private void OnDoneClick(object sender, RoutedEventArgs e) => Complete();

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var url = WebView.Source?.ToString() ?? string.Empty;

        // Hay que pasar por la pantalla de Google antes de dar el login por bueno;
        // si no, la propia URL inicial de gemini.google.com cerraría la ventana.
        if (url.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase))
            _sawAccountsPage = true;

        if (_sawAccountsPage && url.StartsWith(_successUrlPrefix, StringComparison.OrdinalIgnoreCase))
            Complete();
    }

    private void Complete()
    {
        if (_loginCompleted)
            return;

        _loginCompleted = true;
        DialogResult = true;
        Close();
    }
}
