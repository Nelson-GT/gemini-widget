using System.IO;
using System.Text.Json;

namespace GeminiWidget;

/// <summary>
/// Configuración cargada de <c>appsettings.json</c> (junto al ejecutable).
/// Si el archivo falta o es inválido se usan los valores por defecto.
/// </summary>
public sealed class AppSettings
{
    public int PollingIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// URLs que se recorren en orden hasta encontrar los porcentajes de uso.
    /// La interfaz de Gemini no publica un contrato estable, por eso se admiten varias.
    /// </summary>
    public string[] UsageUrls { get; set; } =
    {
        "https://gemini.google.com/usage",
        "https://gemini.google.com/app"
    };

    public string LoginUrl { get; set; } = "https://gemini.google.com/";

    public static AppSettings Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        try
        {
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
