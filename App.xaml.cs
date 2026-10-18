using System.IO;
using System.Threading;
using System.Windows;

namespace GeminiWidget;

public partial class App : Application
{
    // Objetos con nombre de ámbito local (por sesión de usuario).
    private const string InstanceMutexName = @"Local\GeminiWidget.SingleInstance";
    private const string ShowEventName = @"Local\GeminiWidget.Show";

    private GeminiScraperService? _service;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showRegistration;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ---- Instancia única ----
        // Sin esto, abrir dos veces el acceso directo crearía dos widgets y dos
        // pollers compitiendo por la misma carpeta de sesión de WebView2.
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        _ownsMutex = isFirstInstance;

        if (!isFirstInstance)
        {
            // Ya hay un widget abierto: se le pide que se muestre y salimos sin crear nada.
            SignalExistingInstance();
            Shutdown();
            return;
        }

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

        ListenForShowRequests();
    }

    /// <summary>Avisa a la instancia ya en ejecución de que debe mostrarse al frente.</summary>
    private static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var handle))
            {
                using (handle)
                    handle.Set();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException
            or WaitHandleCannotBeOpenedException)
        {
            // Si no se puede avisar (p. ej. la otra instancia está cerrando), da igual: igual salimos.
        }
    }

    private void ListenForShowRequests()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => Dispatcher.BeginInvoke(new Action(() => (MainWindow as MainWindow)?.BringToFront())),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showRegistration?.Unregister(null);
        _showEvent?.Dispose();

        try { _service?.Shutdown(); } catch { /* no interrumpir el cierre */ }

        if (_ownsMutex)
            _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }
}
