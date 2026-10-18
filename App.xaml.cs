using System.Windows;

namespace GeminiWidget;

public partial class App : Application
{
    private GeminiScraperService? _service;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Gemini Usage Widget",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var settings = AppSettings.Load();
        _service = new GeminiScraperService(settings, Dispatcher);

        var window = new MainWindow(_service);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _service?.Shutdown(); } catch { /* no interrumpir el cierre */ }
        base.OnExit(e);
    }
}
