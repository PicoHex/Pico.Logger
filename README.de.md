# PicoLog

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

![CI](https://github.com/PicoHex/PicoLog/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/PicoLog.svg)](https://www.nuget.org/packages/PicoLog)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

PicoLog ist ein leichtgewichtiges, AOT-freundliches Logging-Framework für .NET-Edge-, Desktop-, Utility- und IoT-Workloads.

Das aktuelle Design ist bewusst klein gehalten:

- **ein Logger-Modell**: `ILogger` / `ILogger<T>`
- **ein DI-Einstiegspunkt**: `AddPicoLog(...)`
- **ein Lifecycle-Besitzer**: `ILoggerFactory`

Strukturierte Eigenschaften sind Teil des Logereignisses selbst, kein separater Logger-Typ. Laufzeit- und Erweiterungstypen wie Sinks, Formatter, `LogEntry` und Flush-Begleiter liegen in `PicoLog`, während verbraucherorientierte Verträge in `PicoLog.Abs` liegen.

## Funktionen

- **AOT-freundliches Design**: vermeidet reflektionslastige Infrastruktur und enthält ein Native-AOT-Beispiel
- **Begrenzte asynchrone Pipeline**: Logger übergeben Einträge an kategoriespezifische Pipelines auf Basis begrenzter Channels
- **Explizite Lifecycle-Semantik**: `FlushAsync()` ist eine Barriere während der Laufzeit, `DisposeAsync()` ist das Shutdown-Drain
- **Strukturierte Eigenschaften auf `ILogger`**: native Overloads bewahren Schlüssel/Wert-Nutzdaten in `LogEntry.Properties`
- **Kleine DI-Oberfläche**: `AddPicoLog(...)` registriert `ILoggerFactory` und typisierte `ILogger<T>`-Adapter
- **Eingebaute Sinks und Formatter**: Konsole, farbige Konsole, Datei und ein gut lesbarer Text-Formatter
- **Flush-Begleitverträge**: Laufzeit-Flush-Fähigkeiten bleiben über `IFlushableLoggerFactory` und `IFlushableLogSink` verfügbar
- **Eingebaute Metriken**: Queue-, Drop-, Sink-Fehler- und Shutdown-Metriken über `System.Diagnostics.Metrics`
- **Benchmark-Projekt**: PicoBench-basierte Benchmarks für PicoLog-Handoff-Kosten und MEL-Baselines
- **Scope-Unterstützung**: verschachtelte Scopes laufen über `AsyncLocal` und werden an jeden `LogEntry` angehängt

## Projektstruktur

```text
PicoLog/
├── src/
│   ├── PicoLog.Abs/        # Consumer-facing contracts (ILogger, ILogger<T>, ILoggerFactory, LogLevel)
│   ├── PicoLog/            # Runtime implementation and extensibility contracts
│   └── PicoLog.DI/         # PicoDI integration via AddPicoLog(...)
├── benchmarks/
│   └── PicoLog.Benchmarks/ # PicoBench-based benchmark project
├── samples/
│   └── PicoLog.Sample/     # End-to-end sample app
└── tests/                  # Test projects
```

## Installation

### Kernlaufzeit

```bash
dotnet add package PicoLog
```

### PicoDI-Integration

```bash
dotnet add package PicoLog.DI
```

## Schnellstart

### Grundlegende Verwendung

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

`FlushAsync()` ist **keine** Freigabe. Es ist eine Barriere für Einträge, die bereits vor dem Flush-Snapshot akzeptiert wurden. Verwende `DisposeAsync()` für das endgültige Shutdown-Drain und die Bereinigung der Sinks.

### DI-Integration

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

`AddPicoLog()` ist der einzige DI-Einstiegspunkt. Business-Code sollte normalerweise von `ILogger<T>` abhängen. `ILoggerFactory` ist der explizite Lifecycle-Besitzer am App-Root für Flush und Shutdown.

## Kernmodell

### Ein Logger-Modell

PicoLog trennt Logging nicht mehr in Logger-Schnittstellen für „plain“ und „structured“.

- `ILogger` / `ILogger<T>` ist die Hauptoberfläche zum Schreiben
- einfache Ereignisse verwenden `Log(level, message, exception?)`
- strukturierte Ereignisse verwenden `Log(level, message, properties, exception)`
- asynchrone Varianten folgen mit `LogAsync(...)` derselben Form

`LogStructured()` und `LogStructuredAsync()` existieren weiterhin als Komfort-Wrapper in `LoggerExtensions`, sind aber nur Syntaxzucker über die nativen `ILogger`-Overloads.

### Paketaufteilung

- **`PicoLog.Abs`**: verbraucherorientierte Verträge wie `ILogger`, `ILogger<T>`, `ILoggerFactory`, `LogLevel` und `LoggerExtensions`
- **`PicoLog`**: Laufzeit-Implementierungspaket mit `LoggerFactory`, `Logger<T>`, eingebauten Sinks, eingebauten Formattern und asynchronem Pipeline-/Runtime-Verhalten
- **`PicoLog.DI`**: PicoDI-Integration über `AddPicoLog(...)`

## Konfiguration

### Minimale Ebene

`LoggerFactory.MinLevel` steuert, welche Einträge akzeptiert werden. Niedrigere numerische Werte sind schwerwiegender, daher lässt die Standardebene `Debug` alles von `Emergency` bis `Debug` zu, filtert aber `Trace` heraus.

```csharp
await using var loggerFactory = new LoggerFactory(sinks)
{
    MinLevel = LogLevel.Warning
};
```

### Lifecycle-Besitz

`LoggerFactory` besitzt:

- zwischengespeicherte Logger pro Kategorie
- Pipelines pro Kategorie
- Hintergrund-Drain-Tasks
- registrierte Sinks

Das bedeutet, Lifecycle-APIs gehören zur Factory-Geschichte, nicht zu `ILogger<T>`.

```csharp
await using var loggerFactory = new LoggerFactory(sinks);

var logger = new Logger<MyService>(loggerFactory);
logger.Info("Starting up");

await loggerFactory.FlushAsync();   // mid-run barrier
await loggerFactory.DisposeAsync(); // final shutdown drain
```

Wenn du `ILoggerFactory` aus DI auflöst, denke daran, dass sie weiterhin der appweite Singleton-Lifecycle-Besitzer ist. Das Auflösen aus einem Scope macht sie **nicht** zu einer scope-eigenen Instanz.

### Queue-Druck

`LoggerFactoryOptions.QueueFullMode` macht Queue-Druck sowohl für synchrone als auch für asynchrone Schreibvorgänge explizit.

Der Abschluss von `LogAsync()` bedeutet, dass die Handoff-Behandlung an der Logger-Grenze dort beendet ist. Der Eintrag könnte:

- akzeptiert worden sein
- durch die Queue-Richtlinie verworfen worden sein
- während des Shutdowns abgelehnt worden sein

Das bedeutet **nicht**, dass ein Sink das Schreiben dauerhaft abgeschlossen hat.

- `DropOldest` hält Logging blockierungsfrei, indem der älteste Eintrag in der Queue verworfen wird. Das ist der Standard.
- `DropWrite` lehnt den neuen Eintrag ab und meldet den Verlust über `OnMessagesDropped`.
- `Wait` blockiert synchrones Logging bis zu `SyncWriteTimeout` und lässt asynchrones Logging auf freien Queue-Platz warten, bis eine Abbruchanforderung vorliegt.

```csharp
var options = new LoggerFactoryOptions
{
    MinLevel = LogLevel.Info,
    QueueFullMode = LogQueueFullMode.Wait,
    SyncWriteTimeout = TimeSpan.FromMilliseconds(500)
};

await using var loggerFactory = new LoggerFactory(sinks, options);
```

## Eingebaute Laufzeitbausteine

### Sinks

- `ConsoleSink` schreibt einfach formatierte Einträge auf die Standardausgabe.
- `ColoredConsoleSink` serialisiert Farbwechsel, damit Konsolenzustand nicht zwischen gleichzeitigen Schreibvorgängen ausläuft.
- `FileSink` bündelt UTF-8-Dateischreibvorgänge in einer Hintergrund-Queue vor dem Flush auf die Festplatte und unterstützt Sink-Flush über `IFlushableLogSink`.

Wenn `AddPicoLog()` verwendet wird, werden konfigurierte Sinks innerhalb der Logger-Factory erstellt, sodass die Factory der einzige Besitzer ihrer Lebensdauer bleibt.

### Formatter

`ConsoleFormatter` erzeugt gut lesbare Zeilen mit Zeitstempel, Ebene, Kategorie, Nachricht, optionalen strukturierten Eigenschaften, Ausnahmetext und optionalen Scopes.

```csharp
var formatter = new ConsoleFormatter();
var sink = new ConsoleSink(formatter);
```

### Strukturiertes Logging

Strukturierte Daten sind Teil des Logereignisses selbst.

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

Diese Eigenschaften bleiben in `LogEntry.Properties` erhalten, und Sinks oder Formatter entscheiden, wie sie sie verwenden.

Zum Beispiel hängt `ConsoleFormatter` sie in einer kompakten Textform an:

```text
[2026-04-05 11:30:42.123] WARNING   [MyService] Cache miss {cacheKey="user:42", node="edge-a", attempt=3}
```

## Logging-Erweiterungen

Die mitgelieferten Erweiterungsmethoden sind auf `ILogger` und `ILogger<T>` definiert:

- `Trace`, `Debug`, `Info`, `Notice`, `Warning`, `Error`, `Critical`, `Alert`, `Emergency`
- asynchrone Gegenstücke wie `InfoAsync` und `ErrorAsync`
- `LogStructured` und `LogStructuredAsync` als Komfort-Wrapper über die nativen property-bewussten `ILogger`-Overloads
- Best-Effort-`FlushAsync()`-Erweiterungen auf `ILoggerFactory` und `ILogSink`

Die Erweiterung `ILoggerFactory.FlushAsync()` liegt in `PicoLog`, nicht in `PicoLog.Abs`. Die strenge Laufzeitfähigkeit bleibt `IFlushableLoggerFactory`, während die Erweiterung die übliche Aufrufstelle einfach hält.

## PicoDI-Integration

`AddPicoLog()` registriert:

- ein Singleton-`ILoggerFactory`
- typisierte `ILogger<T>`-Adapter
- eingebautes Standard-Sink-Verhalten, wenn keine explizite `WriteTo`-Pipeline konfiguriert ist
- optionale Einbindung von in DI registrierten Sinks, wenn `ReadFrom.RegisteredSinks()` aktiviert ist

Für neuen Code solltest du den `WriteTo`-Sink-Builder als primären Konfigurationspfad bevorzugen.

```csharp
container.AddPicoLog(options =>
{
    options.MinLevel = LogLevel.Info;
    options.WriteTo.ColoredConsole();
    options.WriteTo.File("logs/app.log");
});
```

Du kannst auch bereits in PicoDI registrierte Sinks einbinden:

```csharp
container.Register(new SvcDescriptor(typeof(ILogSink), _ => new AuditSink()));

container.AddPicoLog(options =>
{
    options.ReadFrom.RegisteredSinks();
    options.WriteTo.ColoredConsole();
});
```

## Metriken

Das Kernpaket `PicoLog` emittiert eine kleine eingebaute Metrikoberfläche über `System.Diagnostics.Metrics` mit dem Meter-Namen `PicoLog`.

- `picolog.entries.enqueued`
- `picolog.entries.dropped`
- `picolog.sinks.failures`
- `picolog.writes.rejected_after_shutdown`
- `picolog.queue.entries`
- `picolog.shutdown.drain.duration`

Diese Instrumente sind bewusst kardinalitätsarm und leichtgewichtig.

```csharp
using var listener = new MeterListener();

listener.InstrumentPublished = (instrument, meterListener) =>
{
    if (instrument.Meter.Name == PicoLogMetrics.MeterName)
        meterListener.EnableMeasurementEvents(instrument);
};

listener.Start();
```

## AOT-Kompatibilität

Das Beispielprojekt wird mit aktiviertem Native AOT veröffentlicht.

```xml
<PropertyGroup>
  <PublishAOT>true</PublishAOT>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r win-x64 -p:PublishAOT=true
```

Das Repository enthält außerdem ein Publish-Validierungsskript, das das Beispiel veröffentlicht, die erzeugte ausführbare Datei startet und überprüft, dass die finalen Shutdown-Logeinträge korrekt geleert wurden:

```powershell
./scripts/Validate-AotSample.ps1
```

## Aus dem Quellcode erstellen

### Voraussetzungen

- .NET SDK 10.0 oder neuer
- Git

### Klonen und erstellen

```bash
git clone https://github.com/PicoHex/PicoLog.git
cd PicoLog
dotnet restore
dotnet build --configuration Release
```

### Tests ausführen

```bash
dotnet test --solution ./PicoLog.slnx --configuration Release
```

## Hinweise zur Leistung

- Logger-Instanzen werden pro Kategorie in `LoggerFactory` zwischengespeichert
- `LoggerFactory` besitzt pro Kategorie einen begrenzten Channel, eine Kategorien-Pipeline und einen Hintergrund-Drain-Task
- `FlushAsync()` ist eine Barriere für Einträge, die vor dem Flush-Snapshot akzeptiert wurden, kein Abkürzungsweg für Freigabe
- die Freigabe der Factory führt weiterhin das finale Drain aus, bevor Sinks freigegeben werden
- `FileSink` bündelt Schreibvorgänge in seiner eigenen begrenzten Queue und bietet Sink-Flush über `IFlushableLogSink`
- die Wahl von `DropOldest`, `DropWrite` oder `Wait` ist ein Trade-off zwischen Durchsatz und Zustellung, kein Korrektheitsfehler

## Benchmarks

Das Repository enthält `benchmarks/PicoLog.Benchmarks`, ein PicoBench-basiertes Benchmark-Projekt zum Vergleich der PicoLog-Handoff-Kosten mit Baselines aus Microsoft.Extensions.Logging.

- `MicrosoftAsyncHandoff` ist die leichtgewichtige MEL-Baseline mit String-Channel.
- `MicrosoftAsyncEntryHandoff` ist die fairere MEL-Baseline mit vollständigem Eintrag, die die Kosten für den Envelope aus Zeitstempel, Kategorie und Nachricht von PicoLog nachbildet, ohne echte I/O hinzuzufügen.
- Benchmark-Namen für den Wait-Modus wie `PicoWaitControl_*` sind interne Bezeichnungen für Benchmark-Szenarien, keine öffentlichen API-Namen.

Benchmark-Projekt ausführen:

```bash
dotnet run -c Release --project benchmarks/PicoLog.Benchmarks
```

Oder das Artefakt direkt veröffentlichen und ausführen:

```bash
dotnet publish benchmarks/PicoLog.Benchmarks/PicoLog.Benchmarks.csproj -c Release -r win-x64
benchmarks/PicoLog.Benchmarks/bin/Release/net10.0/win-x64/publish/PicoLog.Benchmarks.exe main
```

## PicoLog erweitern

### Benutzerdefinierter Sink

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

Wenn ein Sink `IFlushableLogSink` nicht implementiert, ist die Erweiterung `ILogSink.FlushAsync()` best effort und wird sofort abgeschlossen.

### Benutzerdefinierter Formatter

```csharp
public sealed class CustomFormatter : ILogFormatter
{
    public string Format(LogEntry entry)
    {
        return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}";
    }
}
```

## Tests

Die Testsuite deckt derzeit ab:

- konkurrierende Dateisink-Schreibvorgänge während asynchroner Freigabe
- Logger-Caching nach Kategorie
- Filterung nach Mindestebene
- Erfassung und Formatierung strukturierter Nutzdaten
- Emission eingebauter Metriken
- Scope-Erfassung und Flush bei Factory-Freigabe
- Flush-Barrieren für akzeptierte Einträge und Best-Effort-Flush-Erweiterungen
- Ablehnung von Schreibvorgängen nach Beginn des Shutdowns
- Isolierung von Sink-Fehlern
- Flush von asynchronen Tail-Nachrichten
- Persistenz des echten Dateisink-Tails
- konfigurierte DI-Dateiausgabe
- Auflösung typisierter PicoDI-Logger

Das Beispiel wird außerdem über Native-AOT-Publish und -Ausführung verifiziert.

## Einsatzbereich und Nicht-Ziele

### Stärken

- Die Kernimplementierung ist klein und leicht nachvollziehbar: `LoggerFactory` besitzt Registrierungen pro Kategorie, Pipelines, Lebensdauern der Drain-Tasks und Lebensdauern der Sinks, während jeder `InternalLogger` eine leichtgewichtige, nicht besitzende Schreibfassade bleibt.
- Das Projekt ist AOT-freundlich und vermeidet reflektionslastige Infrastruktur.
- Strukturierte Eigenschaften und eingebaute Metriken decken gängige operative Anforderungen ab, ohne dem Anwendungsprozess ein größeres Logging-Ökosystem aufzuzwingen.
- Queue-Druck ist explizit statt verborgen.
- Die Flush-Semantik bleibt explizit: `FlushAsync()` ist eine Barriere für bereits akzeptierte Arbeit, während `DisposeAsync()` der Shutdown-Pfad für finales Drain und Ressourcenfreigabe bleibt.
- Die eingebaute PicoDI-Integration bleibt schlank und vorhersehbar.

### Gute Einsatzfälle

- Kleine bis mittelgroße .NET-Anwendungen, die einen leichtgewichtigen Logging-Kern wollen, ohne ein größeres Logging-Ökosystem zu übernehmen
- Edge-, IoT-, Desktop- und Utility-Workloads, bei denen Startkosten, Binärgröße und AOT-Kompatibilität wichtig sind
- Anwendungsszenarien für Logging, in denen Best-Effort-Zustellung akzeptabel ist, Flush-Barrieren während der Laufzeit gelegentlich nützlich sind und explizites Shutdown-Drain ausreicht
- Teams, die einen kleinen Satz an Primitiven bevorzugen und bei Bedarf eigene Sinks oder Formatter hinzufügen können

### Nicht-Ziele und Schwachstellen

- Dies ist keine vollständige Observability-Plattform.
- Es ist nicht für Logger-Kategorien mit sehr hoher Kardinalität optimiert, weil das aktuelle Design pro Kategorie eine Factory-eigene Pipeline und einen Hintergrund-Drain-Task erstellt.
- Es ist keine Standardwahl für Audit- oder Compliance-Logging, bei dem stiller Verlust nicht akzeptabel ist.
- Eingebaute Metriken sind bewusst klein und versuchen nicht, ein größeres End-to-End-Telemetriesystem abzubilden.

## Mitwirken

1. Forke das Repository
2. Erstelle einen Feature-Branch
3. Nimm deine Änderungen vor
4. Füge Tests hinzu oder aktualisiere sie
5. Reiche einen Pull Request ein

## Lizenz

Dieses Projekt ist unter der MIT-Lizenz lizenziert. Details findest du in der Datei [LICENSE](LICENSE).
