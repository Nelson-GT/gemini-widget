using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace GeminiWidget;

public sealed record PageSnapshot(string Url, string Title, string Text);

/// <summary>
/// Gestiona el ciclo de vida de WebView2 (instancia invisible), la persistencia
/// de la sesión de Google y el ciclo de polling que extrae los límites de uso.
/// Debe crearse y usarse desde el hilo de UI de WPF.
/// </summary>
public sealed class GeminiScraperService
{
    /// <summary>User-Agent de Chrome para que Google sirva la interfaz de escritorio completa.</summary>
    public static readonly string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";

    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Window? _hostWindow;
    private WebView2? _webView;
    private CoreWebView2Environment? _environment;
    private CancellationTokenSource? _cts;
    private Task? _initTask;

    public GeminiScraperService(AppSettings settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GeminiWidget", "UserContext");
        Model = new UsageModel();
    }

    public UsageModel Model { get; }

    /// <summary>Carpeta persistente donde WebView2 guarda cookies y sesión de Google.</summary>
    public string UserDataFolder { get; }

    /// <summary>Se dispara cuando la página pide iniciar sesión. El handler debe mostrar la ventana de login.</summary>
    public event EventHandler? LoginRequired;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public Task EnsureInitializedAsync() => _initTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        Directory.CreateDirectory(UserDataFolder);

        _environment = await CoreWebView2Environment.CreateAsync(userDataFolder: UserDataFolder);

        // Ventana 1x1 fuera de pantalla: WebView2 necesita estar en una ventana real
        // para inicializarse, pero nunca debe ser visible para el usuario.
        _hostWindow = new Window
        {
            Title = "GeminiWidget.WebViewHost",
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = false
        };

        _webView = new WebView2();
        _hostWindow.Content = _webView;
        _hostWindow.Show();

        await _webView.EnsureCoreWebView2Async(_environment);

        var core = _webView.CoreWebView2;
        core.Settings.UserAgent = UserAgent;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await RefreshAsync(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.PollingIntervalMinutes));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                try
                {
                    await RefreshAsync(token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task RefreshAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await RefreshCoreAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        await EnsureInitializedAsync();
        SetStatus("Consultando límites…");

        var snapshot = new PageSnapshot(string.Empty, string.Empty, string.Empty);
        UsageParseResult? parsed = null;

        foreach (var url in _settings.UsageUrls)
        {
            await NavigateAsync(url, TimeSpan.FromSeconds(30), token);
            snapshot = await WaitForSnapshotAsync(TimeSpan.FromSeconds(15), token);

            if (LooksLikeLogin(snapshot))
                break;

            var attempt = UsageParser.Parse(snapshot.Text);
            if (attempt.Success)
            {
                parsed = attempt;
                break;
            }
        }

        if (LooksLikeLogin(snapshot))
        {
            UpdateModel(m =>
            {
                m.IsAuthenticated = false;
                m.StatusMessage = "Inicia sesión en Gemini";
            });
            RequestLogin();
            return;
        }

        UpdateModel(m =>
        {
            m.IsAuthenticated = true;

            if (parsed is { Success: true })
            {
                m.CurrentUsagePercentage = parsed.CurrentPercentage;
                m.CurrentResetTime = FormatReset(parsed.CurrentReset);
                m.WeeklyUsagePercentage = parsed.WeeklyPercentage;
                m.WeeklyResetTime = FormatReset(parsed.WeeklyReset);
                m.LastUpdated = DateTime.Now;
                m.HasData = true;
                m.StatusMessage = string.Empty;
            }
            else
            {
                m.StatusMessage = "No se detectaron datos de uso";
            }
        });
    }

    private async Task NavigateAsync(string url, TimeSpan timeout, CancellationToken token)
    {
        var core = _webView!.CoreWebView2;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            core.NavigationCompleted -= OnCompleted;
            tcs.TrySetResult(args.IsSuccess);
        }

        core.NavigationCompleted += OnCompleted;
        core.Navigate(url);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(timeout);
        using (timeoutCts.Token.Register(() => tcs.TrySetResult(false)))
        {
            await tcs.Task;
        }
    }

    private async Task<PageSnapshot> WaitForSnapshotAsync(TimeSpan timeout, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + timeout;
        var snapshot = await ReadPageAsync();

        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();

            if (snapshot.Text.Contains('%') || LooksLikeLogin(snapshot))
                return snapshot;

            await Task.Delay(1000, token);
            snapshot = await ReadPageAsync();
        }

        return snapshot;
    }

    private async Task<PageSnapshot> ReadPageAsync()
    {
        const string script =
            "JSON.stringify({url:location.href,title:document.title," +
            "text:(document.body?document.body.innerText:'')})";

        var encoded = await _webView!.ExecuteScriptAsync(script);

        var inner = "{}";
        try { inner = JsonSerializer.Deserialize<string>(encoded) ?? "{}"; }
        catch (JsonException) { }

        try
        {
            using var document = JsonDocument.Parse(inner);
            var root = document.RootElement;
            return new PageSnapshot(
                root.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty,
                root.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                root.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty);
        }
        catch (JsonException)
        {
            return new PageSnapshot(string.Empty, string.Empty, string.Empty);
        }
    }

    private static bool LooksLikeLogin(PageSnapshot snapshot)
    {
        if ((snapshot.Url ?? string.Empty).Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase))
            return true;

        var text = (snapshot.Text ?? string.Empty).ToLowerInvariant();
        var hasLoginWords = text.Contains("iniciar sesión") || text.Contains("sign in")
            || text.Contains("sign-in") || text.Contains("use your google account");

        return hasLoginWords && !text.Contains('%');
    }

    private static string FormatReset(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "—";

        var value = raw.Trim();
        if (value.Contains("restablece", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("resets", StringComparison.OrdinalIgnoreCase))
            return value;

        var hasDate = Regex.IsMatch(value,
            @"\b(ene|feb|mar|abr|may|jun|jul|ago|sep|oct|nov|dic|jan|apr|aug|dec)\w*",
            RegexOptions.IgnoreCase);

        return hasDate ? $"Se restablece el {value}" : $"Se restablece a las {value}";
    }

    private void RequestLogin() =>
        _dispatcher.BeginInvoke(new Action(() => LoginRequired?.Invoke(this, EventArgs.Empty)));

    private void SetStatus(string status) => UpdateModel(m => m.StatusMessage = status);

    private void UpdateModel(Action<UsageModel> update)
    {
        if (_dispatcher.CheckAccess())
            update(Model);
        else
            _dispatcher.Invoke(() => update(Model));
    }

    /// <summary>Abre la ventana modal de inicio de sesión de Google. Devuelve true si el login terminó.</summary>
    public async Task<bool> ShowLoginAsync()
    {
        await EnsureInitializedAsync();

        var successPrefix = new Uri(_settings.LoginUrl).GetLeftPart(UriPartial.Authority);
        var window = new LoginWindow(_environment!, _settings.LoginUrl, successPrefix)
        {
            Owner = _dispatcher.CheckAccess() ? Application.Current?.MainWindow : null
        };

        return window.ShowDialog() == true;
    }

    public void Shutdown()
    {
        try { _cts?.Cancel(); } catch (OperationCanceledException) { }
        _cts?.Dispose();
        _cts = null;

        try { _webView?.Dispose(); } catch { }
        _webView = null;

        try { _hostWindow?.Close(); } catch { }
        _hostWindow = null;

        _environment = null;
        _initTask = null;
    }
}
