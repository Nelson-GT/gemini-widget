using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GeminiWidget;

/// <summary>
/// Datos que muestra el widget. Las propiedades de tiempo de restablecimiento
/// contienen la frase ya formateada para la UI (por ejemplo
/// "Se restablece a las 3:59"), o "—" cuando aún no hay datos.
/// </summary>
public sealed class UsageModel : INotifyPropertyChanged
{
    private double _currentUsagePercentage;
    private string _currentResetTime = "—";
    private double _weeklyUsagePercentage;
    private string _weeklyResetTime = "—";
    private DateTime _lastUpdated;
    private bool _isAuthenticated;
    private string _statusMessage = "Iniciando…";
    private bool _hasData;

    public double CurrentUsagePercentage
    {
        get => _currentUsagePercentage;
        set { if (Set(ref _currentUsagePercentage, value)) OnPropertyChanged(nameof(CurrentUsageText)); }
    }

    public string CurrentResetTime
    {
        get => _currentResetTime;
        set => Set(ref _currentResetTime, value);
    }

    public double WeeklyUsagePercentage
    {
        get => _weeklyUsagePercentage;
        set { if (Set(ref _weeklyUsagePercentage, value)) OnPropertyChanged(nameof(WeeklyUsageText)); }
    }

    public string WeeklyResetTime
    {
        get => _weeklyResetTime;
        set => Set(ref _weeklyResetTime, value);
    }

    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set { if (Set(ref _lastUpdated, value)) OnPropertyChanged(nameof(LastUpdatedText)); }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => Set(ref _isAuthenticated, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public bool HasData
    {
        get => _hasData;
        set => Set(ref _hasData, value);
    }

    public string CurrentUsageText => $"{CurrentUsagePercentage:0}%";

    public string WeeklyUsageText => $"{WeeklyUsagePercentage:0}%";

    public string LastUpdatedText => LastUpdated == default ? "Sin datos" : $"Actualizado {LastUpdated:HH:mm}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
