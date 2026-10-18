using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace GeminiWidget;

public partial class MainWindow : Window
{
    private readonly GeminiScraperService _service;
    private bool _loginPrompted;

    public MainWindow(GeminiScraperService service)
    {
        InitializeComponent();

        _service = service;
        DataContext = service.Model;
        _service.LoginRequired += OnLoginRequired;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        _service.Start();
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _service.LoginRequired -= OnLoginRequired;
        _service.Shutdown();
    }

    /// <summary>
    /// Se llama cuando el usuario intenta abrir el widget estando ya en ejecución.
    /// En lugar de crear otra instancia, se trae esta al frente y parpadea para
    /// que el usuario sepa que ya estaba abierto.
    /// </summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();

        var animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220))
        {
            AutoReverse = true
        };
        BeginAnimation(OpacityProperty, animation);
    }

    // Arrastre fluido: se engancha al borde de la tarjeta, no a la ventana,
    // para que los botones (que marcan el evento como manejado) no lo activen.
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || e.ClickCount != 1)
            return;

        try { DragMove(); }
        catch (InvalidOperationException) { /* el botón se soltó durante el arrastre */ }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await _service.RefreshAsync();

    private async void OnLoginClick(object sender, RoutedEventArgs e) =>
        await PromptLoginAsync();

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();

    private async void OnLoginRequired(object? sender, EventArgs e)
    {
        // Solo se abre automáticamente la primera vez; después el usuario usa el botón.
        if (_loginPrompted)
            return;

        _loginPrompted = true;
        await PromptLoginAsync();
    }

    private async Task PromptLoginAsync()
    {
        var authenticated = await _service.ShowLoginAsync();
        if (authenticated)
            await _service.RefreshAsync();
    }
}
