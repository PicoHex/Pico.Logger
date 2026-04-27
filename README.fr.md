# PicoLog

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

![CI](https://github.com/PicoHex/PicoLog/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/PicoLog.svg)](https://www.nuget.org/packages/PicoLog)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

PicoLog est un framework de journalisation léger et compatible AOT pour les charges de travail .NET edge, desktop, utilitaires et IoT.

La conception actuelle est volontairement réduite :

- **un modèle de logger** : `ILogger` / `ILogger<T>`
- **un point d’entrée DI** : `AddPicoLog(...)`
- **un propriétaire du cycle de vie** : `ILoggerFactory`

Les propriétés structurées font partie de l’événement de log lui-même, pas d’un type de logger séparé. `PicoLog.Abs` est le package de contrat public pour les abstractions de journalisation, les sinks, les formatters, `LogEntry` et les compagnons de flush, tandis que `PicoLog` fournit l’implémentation runtime.

## Fonctionnalités

- **Conception compatible AOT** : évite une infrastructure très dépendante de la réflexion et inclut un exemple Native AOT
- **Pipeline asynchrone borné** : les loggers transmettent les entrées à des pipelines par catégorie reposant sur des canaux bornés
- **Sémantique explicite du cycle de vie** : `FlushAsync()` est une barrière en cours d’exécution, `DisposeAsync()` est le drainage de fermeture
- **Propriétés structurées sur `ILogger`** : les surcharges natives conservent les charges clé/valeur dans `LogEntry.Properties`
- **Surface DI réduite** : `AddPicoLog(...)` enregistre `ILoggerFactory` et des adaptateurs `ILogger<T>` typés
- **Sinks et formatter intégrés** : console, console colorée, fichier et formatter texte lisible
- **Contrats compagnons de flush** : les capacités de flush du runtime restent disponibles via `IFlushableLoggerFactory` et `IFlushableLogSink`
- **Métriques intégrées** : métriques de file, de perte, d’échec de sink et d’arrêt via `System.Diagnostics.Metrics`
- **Projet de benchmarks** : benchmarks basés sur PicoBench pour les coûts de handoff de PicoLog et les références MEL
- **Prise en charge des scopes** : les scopes imbriqués transitent via `AsyncLocal` et sont attachés à chaque `LogEntry`

## Structure du projet

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

## Installation

### Runtime principal

```bash
dotnet add package PicoLog
```

### Intégration PicoDI

```bash
dotnet add package PicoLog.DI
```

## Démarrage rapide

### Utilisation de base

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

`FlushAsync()` n’est **pas** une libération. C’est une barrière pour les entrées déjà acceptées avant l’instantané de flush. Utilisez `DisposeAsync()` pour le drainage final à l’arrêt et le nettoyage des sinks.

### Intégration DI

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

`AddPicoLog()` est l’unique point d’entrée DI. Le code métier devrait normalement dépendre de `ILogger<T>`. `ILoggerFactory` est le propriétaire explicite du cycle de vie à la racine de l’application pour le flush et l’arrêt.

## Modèle principal

### Un seul modèle de logger

PicoLog ne sépare plus la journalisation entre interfaces de logger « plain » et « structured ».

- `ILogger` / `ILogger<T>` est la surface principale d’écriture
- les événements simples utilisent `Log(level, message, exception?)`
- les événements structurés utilisent `Log(level, message, properties, exception)`
- les variantes asynchrones suivent la même forme via `LogAsync(...)`

`LogStructured()` et `LogStructuredAsync()` existent toujours comme wrappers de confort dans `LoggerExtensions`, mais ce ne sont que des raccourcis au-dessus des surcharges natives de `ILogger`.

### Répartition des packages

- **`PicoLog.Abs`** : le package de contrat public, incluant `ILogger`, `ILogger<T>`, `ILoggerFactory`, `LogLevel`, `LoggerExtensions`, `ILogSink`, `ILogFormatter`, `LogEntry`, `IFlushableLoggerFactory`, `IFlushableLogSink` et `FlushExtensions`
- **`PicoLog`** : package d’implémentation runtime avec `LoggerFactory`, `Logger<T>`, les sinks intégrés, les formatters intégrés et le comportement runtime/pipeline asynchrone
- **`PicoLog.DI`** : intégration PicoDI via `AddPicoLog(...)`

## Configuration

### Niveau minimum

`LoggerFactory.MinLevel` contrôle quelles entrées sont acceptées. Les valeurs numériques plus faibles sont plus sévères, donc le niveau par défaut `Debug` autorise `Emergency` jusqu’à `Debug`, mais filtre `Trace`.

```csharp
await using var loggerFactory = new LoggerFactory(sinks)
{
    MinLevel = LogLevel.Warning
};
```

### Propriété du cycle de vie

`LoggerFactory` possède :

- les loggers mis en cache par catégorie
- les pipelines par catégorie
- les tâches de drainage en arrière-plan
- les sinks enregistrés

Cela signifie que les API de cycle de vie appartiennent à l’histoire de la factory, pas à `ILogger<T>`.

```csharp
await using var loggerFactory = new LoggerFactory(sinks);

var logger = new Logger<MyService>(loggerFactory);
logger.Info("Starting up");

await loggerFactory.FlushAsync();   // mid-run barrier
await loggerFactory.DisposeAsync(); // final shutdown drain
```

Si vous résolvez `ILoggerFactory` depuis DI, rappelez-vous qu’il reste le propriétaire singleton du cycle de vie au niveau de l’application. Le résoudre depuis un scope ne le rend **pas** possédé par ce scope.

### Pression sur la file

`LoggerFactoryOptions.QueueFullMode` rend la pression sur la file explicite pour les écritures synchrones comme asynchrones.

La complétion de `LogAsync()` signifie que la gestion du handoff à la frontière du logger est terminée à cet endroit. L’entrée peut avoir été :

- acceptée
- abandonnée par la politique de file
- rejetée pendant l’arrêt

Cela **ne** signifie **pas** qu’un sink a terminé l’écriture de façon durable.

- `DropOldest` garde une journalisation non bloquante en supprimant l’entrée la plus ancienne de la file. C’est le comportement par défaut.
- `DropWrite` rejette la nouvelle entrée et signale la perte via `OnMessagesDropped`.
- `Wait` bloque la journalisation synchrone jusqu’à `SyncWriteTimeout` et fait attendre la journalisation asynchrone jusqu’à ce qu’il y ait de la place dans la file ou qu’une annulation soit demandée.

```csharp
var options = new LoggerFactoryOptions
{
    MinLevel = LogLevel.Info,
    QueueFullMode = LogQueueFullMode.Wait,
    SyncWriteTimeout = TimeSpan.FromMilliseconds(500)
};

await using var loggerFactory = new LoggerFactory(sinks, options);
```

## Éléments de runtime intégrés

### Sinks

- `ConsoleSink` écrit des entrées simplement formatées vers la sortie standard.
- `ColoredConsoleSink` sérialise les changements de couleur afin que l’état de la console ne fuite pas entre écritures concurrentes.
- `FileSink` regroupe les écritures de fichiers UTF-8 dans une file en arrière-plan avant le flush vers le disque et prend en charge le flush au niveau du sink via `IFlushableLogSink`.

Quand `AddPicoLog()` est utilisé, les sinks configurés sont créés à l’intérieur de la logger factory, donc la factory reste l’unique propriétaire de leur durée de vie.

### Formatter

`ConsoleFormatter` produit des lignes lisibles avec horodatage, niveau, catégorie, message, propriétés structurées optionnelles, texte d’exception et scopes optionnels.

```csharp
var formatter = new ConsoleFormatter();
var sink = new ConsoleSink(formatter);
```

### Journalisation structurée

Les données structurées font partie de l’événement de log lui-même.

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

Ces propriétés sont conservées dans `LogEntry.Properties`, et les sinks ou formatters décident comment les consommer.

Par exemple, `ConsoleFormatter` les ajoute sous une forme textuelle compacte :

```text
[2026-04-05 11:30:42.123] WARNING   [MyService] Cache miss {cacheKey="user:42", node="edge-a", attempt=3}
```

## Extensions de journalisation

Les méthodes d’extension fournies sont définies sur `ILogger` et `ILogger<T>` :

- `Trace`, `Debug`, `Info`, `Notice`, `Warning`, `Error`, `Critical`, `Alert`, `Emergency`
- leurs équivalents asynchrones comme `InfoAsync` et `ErrorAsync`
- `LogStructured` et `LogStructuredAsync` comme wrappers de confort au-dessus des surcharges natives de `ILogger` prenant les propriétés en charge
- extensions `FlushAsync()` en best-effort sur `ILoggerFactory` et `ILogSink`

L’extension `ILoggerFactory.FlushAsync()` vit dans `PicoLog.Abs` en tant que partie du package de contrat public. La capacité stricte du runtime reste `IFlushableLoggerFactory`, tandis que l’extension garde le point d’appel courant simple.

## Intégration PicoDI

`AddPicoLog()` enregistre :

- un singleton `ILoggerFactory`
- des adaptateurs `ILogger<T>` typés
- un comportement de sink par défaut intégré lorsqu’aucun pipeline `WriteTo` explicite n’est configuré
- un pont optionnel vers les sinks enregistrés en DI quand `ReadFrom.RegisteredSinks()` est activé

Pour le nouveau code, préférez le générateur de sinks `WriteTo` comme chemin principal de configuration.

```csharp
container.AddPicoLog(options =>
{
    options.MinLevel = LogLevel.Info;
    options.WriteTo.ColoredConsole();
    options.WriteTo.File("logs/app.log");
});
```

Vous pouvez aussi réutiliser des sinks déjà enregistrés dans PicoDI :

```csharp
container.Register(new SvcDescriptor(typeof(ILogSink), _ => new AuditSink()));

container.AddPicoLog(options =>
{
    options.ReadFrom.RegisteredSinks();
    options.WriteTo.ColoredConsole();
});
```

## Métriques

Le package principal `PicoLog` émet une petite surface de métriques intégrées via `System.Diagnostics.Metrics` en utilisant le nom de meter `PicoLog`.

- `picolog.entries.enqueued`
- `picolog.entries.dropped`
- `picolog.sinks.failures`
- `picolog.writes.rejected_after_shutdown`
- `picolog.queue.entries`
- `picolog.shutdown.drain.duration`

Ces instruments sont volontairement à faible cardinalité et légers.

```csharp
using var listener = new MeterListener();

listener.InstrumentPublished = (instrument, meterListener) =>
{
    if (instrument.Meter.Name == PicoLogMetrics.MeterName)
        meterListener.EnableMeasurementEvents(instrument);
};

listener.Start();
```

## Compatibilité AOT

Le projet d’exemple se publie avec Native AOT activé.

```xml
<PropertyGroup>
  <PublishAOT>true</PublishAOT>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r win-x64 -p:PublishAOT=true
```

Le dépôt inclut aussi un script de validation au niveau de la publication qui publie l’exemple, exécute le binaire généré et vérifie que les dernières entrées de log de fermeture ont bien été vidées :

```powershell
./scripts/Validate-AotSample.ps1
```

## Construire depuis les sources

### Prérequis

- .NET SDK 10.0 ou version ultérieure
- Git

### Cloner et construire

```bash
git clone https://github.com/PicoHex/PicoLog.git
cd PicoLog
dotnet restore
dotnet build --configuration Release
```

### Exécuter les tests

```bash
dotnet test --solution ./PicoLog.slnx --configuration Release
```

## Notes de performance

- les instances de logger sont mises en cache par catégorie dans `LoggerFactory`
- `LoggerFactory` possède un canal borné, un pipeline de catégorie et une tâche de drainage en arrière-plan par catégorie
- `FlushAsync()` est une barrière pour les entrées acceptées avant l’instantané de flush, pas un raccourci de libération
- la libération de la factory effectue toujours le drainage final avant de libérer les sinks
- `FileSink` regroupe les écritures dans sa propre file bornée et expose le flush au niveau du sink via `IFlushableLogSink`
- choisir `DropOldest`, `DropWrite` ou `Wait` est un compromis entre débit et livraison, pas un bug de correction

## Benchmarks

Le dépôt inclut `benchmarks/PicoLog.Benchmarks`, un projet de benchmarks basé sur PicoBench pour comparer les coûts de handoff de PicoLog aux références Microsoft.Extensions.Logging.

- `MicrosoftAsyncHandoff` est la référence MEL légère basée sur un canal de chaînes.
- `MicrosoftAsyncEntryHandoff` est la référence MEL plus équitable à entrée complète, qui reflète le coût de l’enveloppe horodatage/catégorie/message de PicoLog sans ajouter de vraie E/S.
- les noms de benchmark en mode wait comme `PicoWaitControl_*` sont des libellés internes de scénarios de benchmark, pas des noms d’API publics.

Exécutez le projet de benchmarks :

```bash
dotnet run -c Release --project benchmarks/PicoLog.Benchmarks
```

Ou publiez puis exécutez directement l’artefact :

```bash
dotnet publish benchmarks/PicoLog.Benchmarks/PicoLog.Benchmarks.csproj -c Release -r win-x64
benchmarks/PicoLog.Benchmarks/bin/Release/net10.0/win-x64/publish/PicoLog.Benchmarks.exe main
```

## Étendre PicoLog

### Sink personnalisé

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

Si un sink n’implémente pas `IFlushableLogSink`, l’extension `ILogSink.FlushAsync()` est en best-effort et se termine immédiatement.

### Formatter personnalisé

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

La suite de tests couvre actuellement :

- les écritures du sink fichier en concurrence avec la libération asynchrone
- la mise en cache des loggers par catégorie
- le filtrage par niveau minimum
- la capture et le formatage des charges structurées
- l’émission des métriques intégrées
- la capture des scopes et le flush lors de la libération de la factory
- les barrières de flush pour les entrées acceptées et les extensions de flush en best-effort
- le rejet des écritures après le début de l’arrêt
- l’isolation des échecs de sink
- le flush des messages de fin asynchrones
- la persistance réelle de fin de fichier pour le sink fichier
- la sortie fichier DI configurée
- la résolution PicoDI des loggers typés

L’exemple est aussi vérifié via la publication et l’exécution Native AOT.

## Cas d’usage et non-objectifs

### Points forts

- L’implémentation centrale est petite et facile à raisonner : `LoggerFactory` possède les enregistrements par catégorie, les pipelines, la durée de vie des tâches de drainage et celle des sinks, tandis que chaque `InternalLogger` reste une façade d’écriture légère et non propriétaire.
- Le projet est compatible AOT et évite une infrastructure très dépendante de la réflexion.
- Les propriétés structurées et les métriques intégrées couvrent les besoins opérationnels courants sans imposer un écosystème de journalisation plus large à l’application.
- Le comportement sous pression de file est explicite plutôt que caché.
- La sémantique de flush reste explicite : `FlushAsync()` est une barrière pour le travail déjà accepté, tandis que `DisposeAsync()` reste le chemin d’arrêt pour le drainage final et la libération des ressources.
- L’intégration PicoDI intégrée reste fine et prévisible.

### Bon choix pour

- Les applications .NET petites à moyennes qui veulent un cœur de journalisation léger sans adopter un écosystème de journalisation plus vaste
- Les charges de travail edge, IoT, desktop et utilitaires où le coût de démarrage, la taille binaire et la compatibilité AOT comptent
- Les scénarios de journalisation applicative où la livraison en best-effort est acceptable, où les barrières de flush en cours d’exécution sont parfois utiles, et où un drainage explicite à l’arrêt suffit
- Les équipes qui préfèrent un petit ensemble de primitives et sont à l’aise pour ajouter des sinks ou formatters personnalisés selon les besoins

### Non-objectifs et points faibles

- Ce n’est pas une plateforme complète d’observabilité.
- Ce n’est pas optimisé pour des catégories de logger à très forte cardinalité, car la conception actuelle crée un pipeline possédé par la factory et une tâche de drainage en arrière-plan par catégorie.
- Ce n’est pas le choix par défaut pour la journalisation d’audit ou de conformité quand une perte silencieuse est inacceptable.
- Les métriques intégrées sont volontairement réduites et ne cherchent pas à modéliser un système de télémétrie de bout en bout plus large.

## Contribution

1. Forkez le dépôt
2. Créez une branche de fonctionnalité
3. Faites vos modifications
4. Ajoutez ou mettez à jour les tests
5. Soumettez une pull request

## Licence

Ce projet est sous licence MIT. Consultez le fichier [LICENSE](LICENSE) pour les détails.
