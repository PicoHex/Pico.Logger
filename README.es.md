# PicoLog

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

![CI](https://github.com/PicoHex/PicoLog/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/PicoLog.svg)](https://www.nuget.org/packages/PicoLog)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

PicoLog es un framework de logging ligero y compatible con AOT para cargas de trabajo .NET de edge, escritorio, utilidades e IoT.

El diseño actual es intencionalmente pequeño:

- **un modelo de logger**: `ILogger` / `ILogger<T>`
- **un punto de entrada de DI**: `AddPicoLog(...)`
- **un propietario del ciclo de vida**: `ILoggerFactory`

Las propiedades estructuradas forman parte del propio evento de log, no de un tipo de logger separado. `PicoLog.Abs` es el paquete de contrato público para abstracciones de logging, sinks, formatters, `LogEntry` y acompañantes de flush, mientras que `PicoLog` proporciona la implementación de runtime.

## Características

- **Diseño compatible con AOT**: evita infraestructura muy dependiente de reflexión e incluye un ejemplo con Native AOT
- **Canalización asíncrona acotada**: los loggers entregan entradas a canalizaciones por categoría respaldadas por canales acotados
- **Semántica explícita del ciclo de vida**: `FlushAsync()` es una barrera en mitad de la ejecución, `DisposeAsync()` es el drenaje de apagado
- **Propiedades estructuradas en `ILogger`**: las sobrecargas nativas conservan cargas clave/valor en `LogEntry.Properties`
- **Superficie de DI pequeña**: `AddPicoLog(...)` registra `ILoggerFactory` y adaptadores tipados `ILogger<T>`
- **Sinks y formatter integrados**: consola, consola con color, archivo y un formatter de texto legible
- **Contratos acompañantes de flush**: las capacidades de flush en runtime siguen disponibles a través de `IFlushableLoggerFactory` e `IFlushableLogSink`
- **Métricas integradas**: métricas de cola, descarte, fallo de sink y apagado mediante `System.Diagnostics.Metrics`
- **Proyecto de benchmarks**: benchmarks basados en PicoBench para los costes de handoff de PicoLog y las líneas base de MEL
- **Soporte de scopes**: los scopes anidados fluyen mediante `AsyncLocal` y se adjuntan a cada `LogEntry`

## Estructura del proyecto

```text
PicoLog/
├── src/
│   ├── PicoLog.Abs/        # Public contracts (ILogger, ILoggerFactory, LogEntry, sinks, formatters, flush helpers)
│   ├── PicoLog/            # Runtime implementation package
│   └── PicoLog.DI/         # PicoDI integration via AddPicoLog(...)
├── benchmarks/
│   └── PicoLog.Benchmarks/ # PicoBench-based benchmark project
├── samples/
│   └── PicoLog.Sample/     # End-to-end sample app
└── tests/                  # Test projects
```

## Instalación

### Runtime principal

```bash
dotnet add package PicoLog
```

### Integración con PicoDI

```bash
dotnet add package PicoLog.DI
```

## Inicio rápido

### Uso básico

```csharp
using PicoLog;
using PicoLog.Abs;

var formatter = new ConsoleFormatter();
using var consoleSink = new ConsoleSink(formatter);
await using var fileSink = new FileSink(formatter, "logs/app.log");

await using var loggerFactory = new LoggerFactory([consoleSink, fileSink])
{
    MinLevel = LogLevel.Info
};

var logger = new Logger<MyService>(loggerFactory);

logger.Info("Application starting");
logger.Warning("Configuration file is missing an optional section");

logger.Log(
    LogLevel.Info,
    "Request completed",
    [
        new("requestId", "req-42"),
        new("statusCode", 200),
        new("elapsedMs", 18.7)
    ],
    exception: null
);

await logger.ErrorAsync(
    "Error occurred",
    new InvalidOperationException("Something went wrong")
);

await loggerFactory.FlushAsync();
```

`FlushAsync()` **no** es la liberación de recursos. Es una barrera para las entradas que ya habían sido aceptadas antes de la instantánea de flush. Usa `DisposeAsync()` para el drenaje final de apagado y la limpieza de sinks.

### Integración con DI

```csharp
using PicoDI;
using PicoDI.Abs;
using PicoLog;
using PicoLog.Abs;
using PicoLog.DI;

ISvcContainer container = new SvcContainer();

container.AddPicoLog(options =>
{
    options.MinLevel = LogLevel.Info;
    options.Factory.QueueFullMode = LogQueueFullMode.Wait;
    options.File.BatchSize = 64;
    options.WriteTo.ColoredConsole();
    options.WriteTo.File("logs/app.log");
});

container.RegisterScoped<IMyService, MyService>();

await using var scope = container.CreateScope();

var service = scope.GetService<IMyService>();
var logger = scope.GetService<ILogger<MyService>>();
var loggerFactory = scope.GetService<ILoggerFactory>();

await service.DoWorkAsync();

logger.Log(
    LogLevel.Info,
    "DI structured event",
    [new("tenant", "alpha"), new("attempt", 3)],
    exception: null
);

await loggerFactory.FlushAsync();
await loggerFactory.DisposeAsync();
```

`AddPicoLog()` es el único punto de entrada de DI. El código de negocio normalmente debería depender de `ILogger<T>`. `ILoggerFactory` es el propietario explícito del ciclo de vida en la raíz de la aplicación para flush y apagado.

## Modelo principal

### Un modelo de logger

PicoLog ya no divide el logging en interfaces de logger “plain” y “structured”.

- `ILogger` / `ILogger<T>` es la superficie principal de escritura
- los eventos simples usan `Log(level, message, exception?)`
- los eventos estructurados usan `Log(level, message, properties, exception)`
- las variantes asíncronas siguen la misma forma mediante `LogAsync(...)`

`LogStructured()` y `LogStructuredAsync()` siguen existiendo como envoltorios de conveniencia en `LoggerExtensions`, pero son solo azúcar sintáctico sobre las sobrecargas nativas de `ILogger`.

### División de paquetes

- **`PicoLog.Abs`**: el paquete de contrato público, incluyendo `ILogger`, `ILogger<T>`, `ILoggerFactory`, `LogLevel`, `LoggerExtensions`, `ILogSink`, `ILogFormatter`, `LogEntry`, `IFlushableLoggerFactory`, `IFlushableLogSink` y `FlushExtensions`
- **`PicoLog`**: paquete de implementación de runtime, como `LoggerFactory`, `Logger<T>`, sinks integrados, formatters integrados y el comportamiento de runtime/pipeline asíncrona
- **`PicoLog.DI`**: integración con PicoDI mediante `AddPicoLog(...)`

## Configuración

### Nivel mínimo

`LoggerFactory.MinLevel` controla qué entradas se aceptan. Los valores numéricos más bajos son más graves, así que el nivel predeterminado `Debug` permite desde `Emergency` hasta `Debug`, pero filtra `Trace`.

```csharp
await using var loggerFactory = new LoggerFactory(sinks)
{
    MinLevel = LogLevel.Warning
};
```

### Propiedad del ciclo de vida

`LoggerFactory` posee:

- loggers en caché por categoría
- canalizaciones por categoría
- tareas de drenaje en segundo plano
- sinks registrados

Eso significa que las API de ciclo de vida pertenecen a la historia de la factory, no a `ILogger<T>`.

```csharp
await using var loggerFactory = new LoggerFactory(sinks);

var logger = new Logger<MyService>(loggerFactory);
logger.Info("Starting up");

await loggerFactory.FlushAsync();   // mid-run barrier
await loggerFactory.DisposeAsync(); // final shutdown drain
```

Si resuelves `ILoggerFactory` desde DI, recuerda que sigue siendo el propietario singleton del ciclo de vida a nivel de aplicación. Resolverlo desde un scope **no** hace que el scope sea su propietario.

### Presión de cola

`LoggerFactoryOptions.QueueFullMode` hace explícita la presión de cola tanto para escrituras síncronas como asíncronas.

Que `LogAsync()` complete significa que el manejo del handoff en el límite del logger ya terminó allí. La entrada pudo haber sido:

- aceptada
- descartada por la política de cola
- rechazada durante el apagado

Eso **no** significa que un sink haya terminado de escribir de forma duradera.

- `DropOldest` mantiene el logging sin bloqueo descartando la entrada más antigua en la cola. Es el valor predeterminado.
- `DropWrite` rechaza la entrada nueva e informa del descarte mediante `OnMessagesDropped`.
- `Wait` bloquea el logging síncrono hasta `SyncWriteTimeout` y hace que el logging asíncrono espere espacio en la cola hasta que se solicite cancelación.

```csharp
var options = new LoggerFactoryOptions
{
    MinLevel = LogLevel.Info,
    QueueFullMode = LogQueueFullMode.Wait,
    SyncWriteTimeout = TimeSpan.FromMilliseconds(500)
};

await using var loggerFactory = new LoggerFactory(sinks, options);
```

## Piezas integradas del runtime

### Sinks

- `ConsoleSink` escribe entradas con formato simple en la salida estándar.
- `ColoredConsoleSink` serializa los cambios de color para que el estado de la consola no se filtre entre escrituras concurrentes.
- `FileSink` agrupa escrituras UTF-8 en archivo en una cola en segundo plano antes de hacer flush al disco y admite flush a nivel de sink mediante `IFlushableLogSink`.

Cuando se usa `AddPicoLog()`, los sinks configurados se crean dentro de la logger factory, así que la factory sigue siendo la única propietaria de su ciclo de vida.

### Formatter

`ConsoleFormatter` produce líneas legibles con marca de tiempo, nivel, categoría, mensaje, propiedades estructuradas opcionales, texto de excepción y scopes opcionales.

```csharp
var formatter = new ConsoleFormatter();
var sink = new ConsoleSink(formatter);
```

### Logging estructurado

Los datos estructurados forman parte del propio evento de log.

```csharp
logger.Log(
    LogLevel.Warning,
    "Cache miss",
    new KeyValuePair<string, object?>[]
    {
        new("cacheKey", "user:42"),
        new("node", "edge-a"),
        new("attempt", 3)
    },
    exception: null
);
```

Esas propiedades se conservan en `LogEntry.Properties`, y los sinks o formatters deciden cómo consumirlas.

Por ejemplo, `ConsoleFormatter` las añade en una forma textual compacta:

```text
[2026-04-05 11:30:42.123] WARNING   [MyService] Cache miss {cacheKey="user:42", node="edge-a", attempt=3}
```

## Extensiones de logging

Los métodos de extensión incluidos están definidos sobre `ILogger` e `ILogger<T>`:

- `Trace`, `Debug`, `Info`, `Notice`, `Warning`, `Error`, `Critical`, `Alert`, `Emergency`
- equivalentes asíncronos como `InfoAsync` y `ErrorAsync`
- `LogStructured` y `LogStructuredAsync` como envoltorios de conveniencia sobre las sobrecargas nativas de `ILogger` conscientes de propiedades
- extensiones `FlushAsync()` best-effort sobre `ILoggerFactory` e `ILogSink`

La extensión `ILoggerFactory.FlushAsync()` vive en `PicoLog.Abs` como parte del paquete de contrato público. La capacidad estricta de runtime sigue siendo `IFlushableLoggerFactory`, mientras que la extensión mantiene simple el punto de llamada común.

## Integración con PicoDI

`AddPicoLog()` registra:

- un singleton `ILoggerFactory`
- adaptadores tipados `ILogger<T>`
- comportamiento de sink predeterminado integrado cuando no se configura una canalización `WriteTo` explícita
- puente opcional de sinks registrados en DI cuando se habilita `ReadFrom.RegisteredSinks()`

Para código nuevo, conviene preferir el generador de sinks `WriteTo` como ruta principal de configuración.

```csharp
container.AddPicoLog(options =>
{
    options.MinLevel = LogLevel.Info;
    options.WriteTo.ColoredConsole();
    options.WriteTo.File("logs/app.log");
});
```

También puedes enlazar sinks ya registrados en PicoDI:

```csharp
container.Register(new SvcDescriptor(typeof(ILogSink), _ => new AuditSink()));

container.AddPicoLog(options =>
{
    options.ReadFrom.RegisteredSinks();
    options.WriteTo.ColoredConsole();
});
```

## Métricas

El paquete principal `PicoLog` emite una pequeña superficie de métricas integradas mediante `System.Diagnostics.Metrics` usando el nombre de medidor `PicoLog`.

- `picolog.entries.enqueued`
- `picolog.entries.dropped`
- `picolog.sinks.failures`
- `picolog.writes.rejected_after_shutdown`
- `picolog.queue.entries`
- `picolog.shutdown.drain.duration`

Estos instrumentos son intencionalmente de baja cardinalidad y ligeros.

```csharp
using var listener = new MeterListener();

listener.InstrumentPublished = (instrument, meterListener) =>
{
    if (instrument.Meter.Name == PicoLogMetrics.MeterName)
        meterListener.EnableMeasurementEvents(instrument);
};

listener.Start();
```

## Compatibilidad con AOT

El proyecto de ejemplo se publica con Native AOT habilitado.

```xml
<PropertyGroup>
  <PublishAOT>true</PublishAOT>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r win-x64 -p:PublishAOT=true
```

El repositorio también incluye un script de validación a nivel de publicación que publica el ejemplo, ejecuta el binario generado y verifica que las entradas finales de log de apagado se hayan vaciado correctamente:

```powershell
./scripts/Validate-AotSample.ps1
```

## Compilar desde el código fuente

### Requisitos previos

- .NET SDK 10.0 o posterior
- Git

### Clonar y compilar

```bash
git clone https://github.com/PicoHex/PicoLog.git
cd PicoLog
dotnet restore
dotnet build --configuration Release
```

### Ejecutar pruebas

```bash
dotnet test --solution ./PicoLog.slnx --configuration Release
```

## Notas de rendimiento

- las instancias de logger se almacenan en caché por categoría dentro de `LoggerFactory`
- `LoggerFactory` posee un canal acotado, una canalización por categoría y una tarea de drenaje en segundo plano por categoría
- `FlushAsync()` es una barrera para las entradas aceptadas antes de la instantánea de flush, no un atajo para liberar recursos
- la liberación de la factory sigue realizando el drenaje final antes de liberar los sinks
- `FileSink` agrupa escrituras en su propia cola acotada y expone flush a nivel de sink mediante `IFlushableLogSink`
- elegir `DropOldest`, `DropWrite` o `Wait` es una compensación entre rendimiento y entrega, no un error de corrección

## Benchmarks

El repositorio incluye `benchmarks/PicoLog.Benchmarks`, un proyecto de benchmarks basado en PicoBench para comparar los costes de handoff de PicoLog con líneas base de Microsoft.Extensions.Logging.

- `MicrosoftAsyncHandoff` es la línea base ligera de MEL con canal de cadenas.
- `MicrosoftAsyncEntryHandoff` es la línea base de MEL más justa y con entrada completa que refleja el coste del sobre de marca de tiempo, categoría y mensaje de PicoLog sin añadir E/S real.
- los nombres de benchmark en modo wait, como `PicoWaitControl_*`, son etiquetas internas de escenarios de benchmark, no nombres de API públicos.

Ejecuta el proyecto de benchmarks:

```bash
dotnet run -c Release --project benchmarks/PicoLog.Benchmarks
```

O publica y ejecuta el artefacto directamente:

```bash
dotnet publish benchmarks/PicoLog.Benchmarks/PicoLog.Benchmarks.csproj -c Release -r win-x64
benchmarks/PicoLog.Benchmarks/bin/Release/net10.0/win-x64/publish/PicoLog.Benchmarks.exe main
```

## Extender PicoLog

### Sink personalizado

```csharp
public sealed class CustomSink : ILogSink, IFlushableLogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"CUSTOM: {entry.Message}");
        return Task.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

var formatter = new ConsoleFormatter();
await using var loggerFactory = new LoggerFactory([new CustomSink(), new FileSink(formatter)]);
await loggerFactory.FlushAsync();
```

Si un sink no implementa `IFlushableLogSink`, la extensión `ILogSink.FlushAsync()` es best effort y se completa de inmediato.

### Formatter personalizado

```csharp
public sealed class CustomFormatter : ILogFormatter
{
    public string Format(LogEntry entry)
    {
        return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}";
    }
}
```

## Pruebas

La suite de pruebas actualmente cubre:

- escrituras del sink de archivo compitiendo con la liberación asíncrona
- almacenamiento en caché de loggers por categoría
- filtrado por nivel mínimo
- captura y formato de cargas estructuradas
- emisión de métricas integradas
- captura de scopes y flush al liberar la factory
- barreras de flush para entradas aceptadas y extensiones de flush best effort
- rechazo de escrituras después de que comienza el apagado
- aislamiento de fallos de sinks
- flush de mensajes finales asíncronos
- persistencia real del final del sink de archivo
- salida de archivo DI configurada
- resolución de loggers tipados en PicoDI

El ejemplo también se verifica mediante publicación y ejecución con Native AOT.

## Encaje y no objetivos

### Puntos fuertes

- La implementación principal es pequeña y fácil de razonar: `LoggerFactory` posee los registros por categoría, las canalizaciones, la vida de las tareas de drenaje y la vida de los sinks, mientras que cada `InternalLogger` sigue siendo una fachada ligera de escritura sin propiedad.
- El proyecto es compatible con AOT y evita infraestructura muy dependiente de reflexión.
- Las propiedades estructuradas y las métricas integradas cubren necesidades operativas comunes sin forzar un ecosistema de logging más grande dentro de la aplicación.
- El comportamiento bajo presión de cola es explícito, no oculto.
- La semántica de flush sigue siendo explícita: `FlushAsync()` es una barrera para trabajo ya aceptado, mientras que `DisposeAsync()` sigue siendo la ruta de apagado para el drenaje final y la liberación de recursos.
- La integración integrada con PicoDI sigue siendo delgada y predecible.

### Buen encaje

- Aplicaciones .NET pequeñas o medianas que quieren un núcleo de logging ligero sin adoptar un ecosistema de logging más grande
- Cargas de trabajo de edge, IoT, escritorio y utilidades donde importan el coste de arranque, el tamaño binario y la compatibilidad con AOT
- Escenarios de logging de aplicaciones donde la entrega best effort es aceptable, las barreras de flush en mitad de la ejecución resultan útiles de vez en cuando y el drenaje explícito al apagar es suficiente
- Equipos que prefieren un conjunto pequeño de primitivas y se sienten cómodos añadiendo sinks o formatters personalizados cuando haga falta

### No objetivos y puntos débiles

- Esto no es una plataforma completa de observabilidad.
- No está optimizado para categorías de logger con cardinalidad muy alta, porque el diseño actual crea una canalización por categoría propiedad de la factory y una tarea de drenaje en segundo plano por categoría.
- No es la opción predeterminada para logging de auditoría o cumplimiento donde la pérdida silenciosa es inaceptable.
- Las métricas integradas son intencionalmente pequeñas y no intentan modelar un sistema de telemetría integral de extremo a extremo.

## Contribuir

1. Haz un fork del repositorio
2. Crea una rama de funcionalidad
3. Realiza tus cambios
4. Añade o actualiza pruebas
5. Envía un pull request

## Licencia

Este proyecto está licenciado bajo la licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.
