# AGENTS.md

## What this is

WPF widget (.NET 8, `net8.0-windows`) que monitorea los límites de uso de Gemini.
**No usa la API de Gemini**: hace scraping de la interfaz web con WebView2 y una
sesión de Google persistida. Un solo proyecto, en la raíz del repo.

## Commands

```powershell
dotnet restore
dotnet run           # ejecutar el widget (csproj está en la raíz)
dotnet build         # compilar la solución
dotnet clean
```

- **Requiere el SDK de .NET 8** (no basta el runtime) y, en ejecución, el
  **WebView2 Runtime** (Evergreen; ya viene en Windows 11).
- No hay proyecto de tests todavía. No inventes `dotnet test`.

## Architecture (lo no obvio)

- `App.xaml.cs` no usa `StartupUri`: crea `AppSettings` -> `GeminiScraperService` -> `MainWindow`.
- `GeminiScraperService` es dueño de un `WebView2` **invisible**: vive en una ventana
  real de 1x1 fuera de pantalla (`Left=-10000`). WebView2 necesita una ventana para
  inicializarse; no lo reemplaces por `Visibility=Collapsed`.
- `MainWindow` es solo UI; su `DataContext` es `service.Model` (`UsageModel`).
  El servicio actualiza el modelo **solo** vía `Dispatcher.Invoke/CheckAccess`.
- `LoginWindow` usa el **mismo** `CoreWebView2Environment` (mismo `UserDataFolder`),
  por eso comparte cookies/sesión con el WebView oculto. La sesión persiste en
  `%APPDATA%/GeminiWidget/UserContext`.
- El scraping usa primero los bloques `data-testid="current-usage"` y
  `data-testid="weekly-usage"` (ver `ScrapeScript` en `GeminiScraperService`):
  esa es la fuente fiable. `UsageParser` (por `innerText` + regex) es solo el
  respaldo si Gemini cambia el marcado. Ojo: la página incluye banners promocionales
  ("Disfruta de 5 veces más uso…") que contaminaban el scraping por texto; por eso
  se extrae del bloque con `data-testid`.

## Gotchas que ya costaron tiempo

- `RefreshAsync` está serializado con un `SemaphoreSlim`. No dispares refreshes
  concurrentes: el diálogo de login se difiere con `Dispatcher.BeginInvoke` para no
  bloquear el semáforo (deadlock).
- La UI espera frases ya formateadas en `UsageModel.CurrentResetTime` /
  `WeeklyResetTime` (p. ej. "Se restablece a las 3:59"), no horas crudas.
- `appsettings.json` se copia a la salida (`CopyToOutputDirectory`). Editarlo en
  `bin/` no sirve; edita el de la raíz. Se parsea con `System.Text.Json`
  (no hay `Microsoft.Extensions.Configuration`).
- `UsageUrls` es una lista que se recorre hasta encontrar datos: no asumas que
  `https://gemini.google.com/usage` es el endpoint correcto (en una sesión sin
  login redirige a `/app`).
- Depuración del scraping: cada sondeo deja el `innerText` leído y los valores
  estructurados (`CURRENT`/`WEEKLY`) en `%APPDATA%\GeminiWidget\last-scan.txt`.
  Es lo primero que revisar si no aparecen datos o si no se pide iniciar sesión.
- La detección de sesión (`LooksLikeLogin`) depende del idioma de la cuenta:
  español = "Acceder", inglés = "Sign in". Si el widget no pide login en otro
  idioma, añade la variante ahí. La ventana de login `LoginWindow` solo se cierra
  al pasar por `accounts.google.com` y volver a Gemini, o con el botón manual.
- Única dependencia NuGet: `Microsoft.Web.WebView2` (fijada en `1.0.4258.31`).
