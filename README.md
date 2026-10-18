# Gemini Usage Widget

Widget de escritorio para Windows que muestra, en una tarjeta flotante siempre
visible, **los límites de uso de Gemini** (uso actual y límite semanal) tomados de
tu propia cuenta.

- **Uso actual** → porcentaje consumido y hora de reinicio.
- **Límite semanal** → porcentaje consumido y fecha/hora de reinicio.

El widget es minimalista, translúcido y de bajo perfil: reposa casi invisible en la
esquina inferior derecha y se vuelve nítido al pasar el cursor por encima.

> ⚠️ **No usa la API de Gemini.** Inicia sesión en la interfaz web con WebView2 y lee
> tus límites de la propia página. Es un proyecto no oficial: si Google cambia su web,
> puede dejar de funcionar hasta ajustar el scraping.

---

## Cómo se ve

- Tarjeta de ~240×146 px con esquinas redondeadas y fondo oscuro translúcido.
- **Opacidad 28%** en reposo (no molesta) y **95%** al pasar el cursor (animado).
- **Se puede arrastrar** desde cualquier punto de la tarjeta.
- **Clic derecho** para el menú: *Actualizar ahora*, *Iniciar sesión en Gemini*, *Salir*.
- Botón **✕** en la esquina para cerrarla.

---

## Requisitos

| Para… | Necesitas |
|---|---|
| **Ejecutar** el widget | Windows 10/11 (x64) + **.NET 8 Desktop Runtime** + **WebView2 Runtime** |
| **Compilar** el proyecto | **.NET 8 SDK** |

- El **WebView2 Runtime** (Evergreen) ya viene en Windows 11 y en la mayoría de
  Windows 10 con Edge actualizado. Si falta, descárgalo aquí:
  <https://developer.microsoft.com/microsoft-edge/webview2/>
- El **.NET 8 Desktop Runtime** (solo ejecutar) y el **SDK** (compilar):
  <https://dotnet.microsoft.com/download/dotnet/8.0>

Comprueba lo que tienes instalado:

```powershell
dotnet --list-sdks        # SDK (para compilar)
dotnet --list-runtimes    # debe aparecer Microsoft.WindowsDesktop.App 8.x
```

---

## Instalación rápida

1. Clona o descarga el repositorio:

   ```powershell
   git clone https://github.com/Nelson-GT/gemini-widget.git
   cd gemini-widget
   ```

2. Genera la versión completa (ver [Compilar](#compilar-tu-propia-versión)):

   ```powershell
   dotnet publish -c Release
   ```

3. Ejecuta el resultado:

   ```
   bin\Release\net8.0-windows\publish\GeminiWidget.exe
   ```

4. La **primera vez** se abrirá una ventana para iniciar sesión con tu cuenta de
   Google. Al terminar, pulsa **«Ya inicié sesión»** (o se cierra sola). La sesión
   queda guardada y no tendrás que repetirlo.

5. Listo: el widget aparecerá en la esquina inferior derecha. Pasa el cursor por
   encima para verlo con claridad.

---

## Compilar tu propia versión

Desde la raíz del repositorio (donde está `GeminiWidget.csproj`):

```powershell
dotnet restore                       # restaurar dependencias
dotnet build                         # compilar
dotnet run                           # ejecutar en modo desarrollo
dotnet publish -c Release            # versión final optimizada
```

La carpeta publicada es:

```
bin\Release\net8.0-windows\publish\
```

> 📌 **Importante:** esa carpeta debe permanecer **completa**. El `.exe` necesita los
> `.dll` y `appsettings.json` que están junto a él. Para moverlo, copia la carpeta
> entera; nunca solo el `.exe`.

### Opción sin instalar .NET (autocontenido)

Si el equipo destino **no** tiene el .NET 8 Desktop Runtime, publica una versión
autocontenida que incluye el runtime:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Pesa más, pero funciona en cualquier Windows x64 sin instalar nada más (el WebView2
Runtime sigue siendo necesario).

---

## Arrancar automáticamente con Windows

**Opción A — carpeta de inicio (la más simple):**

1. Crea un **acceso directo** a `…\publish\GeminiWidget.exe`
   (clic derecho sobre el exe → *Mostrar más opciones* → *Crear acceso directo*).
2. Pulsa `Win + R`, escribe `shell:startup` y pulsa Enter. Se abre la carpeta de
   inicio del usuario.
3. Pega ahí el acceso directo.

**Opción B — Programador de tareas (más robusto):**

1. Abre *Programador de tareas* → *Crear tarea básica*.
2. Desencadenador: **Al iniciar sesión**.
3. Acción: **Iniciar un programa** → selecciona `GeminiWidget.exe`.
4. En *Propiedades* marca **Ejecutar solo cuando el usuario haya iniciado sesión**.
   Opcionalmente añade un **retraso de 30 s** al desencadenador para no competir con
   el arranque del sistema.

> El widget **no admite varias instancias a la vez**: dos copias pelean por la misma
> carpeta de sesión de WebView2. Asegúrate de arrancar solo una.

---

## Configuración

El archivo `appsettings.json` (junto al ejecutable) se puede editar sin recompilar.
Si cambias el de la raíz del repo, vuelve a publicar para que se copie a la salida;
o edita directamente el que está junto al `.exe`.

```json
{
  "PollingIntervalMinutes": 10,
  "UsageUrls": [
    "https://gemini.google.com/usage",
    "https://gemini.google.com/app"
  ],
  "LoginUrl": "https://gemini.google.com/"
}
```

| Campo | Descripción |
|---|---|
| `PollingIntervalMinutes` | Cada cuántos minutos se consultan los límites (mínimo 1). |
| `UsageUrls` | URLs que se recorren en orden hasta encontrar datos. |
| `LoginUrl` | Página que se abre para iniciar sesión. |

---

## Arquitectura

Aplicación **WPF sobre .NET 8** (`net8.0-windows`), un único proyecto en la raíz del
repositorio. Patrón sencillo tipo MVVM: un servicio dueño de un WebView2 invisible
extrae los datos y rellena un modelo que la ventana muestra por *data binding*.

```
App  ──crea──►  GeminiScraperService  ──actualiza──►  UsageModel  ◄──binding──  MainWindow
                        │
                        └── WebView2 (invisible) ──► página de Gemini  (WebView2 + sesión persistida)
```

| Archivo | Responsabilidad |
|---|---|
| `App.xaml(.cs)` | Arranque (sin `StartupUri`): carga ajustes, crea el servicio y la ventana. |
| `MainWindow.xaml(.cs)` | UI: tarjeta flotante, arrastre, opacidad al hover, menú contextual. |
| `LoginWindow.xaml(.cs)` | Ventana modal para el login de Google (comparte la sesión). |
| `GeminiScraperService.cs` | WebView2 invisible, polling con `PeriodicTimer`, scraping y actualización del modelo. |
| `UsageModel.cs` | Propiedades mostradas: `CurrentUsagePercentage`, `CurrentResetTime`, `WeeklyUsagePercentage`, `WeeklyResetTime`, `LastUpdated`. |
| `UsageParser.cs` | Respaldo heurístico (texto + regex) si cambia el marcado. |
| `AppSettings.cs` | Lectura de `appsettings.json` con `System.Text.Json`. |
| `Converters.cs` | Conversores de binding para la UI. |
| `app.manifest` | DPI *Per-Monitor V2* (arrastre correcto entre monitores). |
| `ejemplo.html` | Fragmento del DOM real de Gemini que usa el scraper como referencia. |

### Cómo obtiene los datos

1. Un **WebView2 invisible** vive en una ventana de 1×1 fuera de pantalla.
2. Usa un **perfil persistente** en `%APPDATA%\GeminiWidget\UserContext`, así que
   reutiliza tu sesión de Google.
3. Cada N minutos navega a la página de límites y ejecuta un script que lee los
   bloques `data-testid="current-usage"` y `data-testid="weekly-usage"`.
4. Escribe los valores en el `UsageModel` desde el hilo de UI (`Dispatcher`), y el
   *binding* de WPF actualiza la tarjeta.

---

## Solución de problemas

- **No aparece nada de información / no pide iniciar sesión.** Revisa
  `%APPDATA%\GeminiWidget\last-scan.txt`: contiene el texto y los valores que el
  widget leyó de la página. Es lo primero que hay que mirar.
- **No veo el widget.** Está en la esquina inferior derecha y a 28% de opacidad;
  pasa el cursor por encima.
- **La ventana de login se cierra sola.** Corregido: ahora solo se cierra tras pasar
  por `accounts.google.com`, o al pulsar «Ya inicié sesión».
- **Google dice «este navegador o app puede no ser segura».** Es una limitación del
  login dentro de navegadores embebidos; puede requerir iniciar sesión de otra forma.
- **Se queda en blanco o con datos viejos.** Clic derecho → *Actualizar ahora*. Si
  persiste, borra `%APPDATA%\GeminiWidget\UserContext` y vuelve a iniciar sesión.
- **Dos instancias abiertas.** Ciérralas todas y abre solo una.

---

## Privacidad

- **No hay API key.** El widget no se conecta a la API de Gemini.
- Tu sesión de Google se guarda **solo en tu equipo**
  (`%APPDATA%\GeminiWidget\UserContext`) y no se envía a ningún servicio aparte de
  Google.
- No hay telemetría ni recolección de datos.

---

## Limitaciones

- Solo **Windows** (WPF + WebView2).
- Es un *scraping* de la web de Gemini: si Google cambia su interfaz, habrá que
  ajustar `ScrapeScript` en `GeminiScraperService.cs` o `UsageParser.cs`.
- No soporta múltiples cuentas simultáneas ni múltiples instancias.

---

## Licencia

Uso personal. Proyecto no oficial y sin relación con Google.
