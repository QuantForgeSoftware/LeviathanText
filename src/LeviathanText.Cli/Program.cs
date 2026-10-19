using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using LeviathanText.Recognition;
using LeviathanText.Training;

namespace LeviathanText.Cli;

/// <summary>
/// Local CLI (todo Phase 1: batch processing + CLI; no web server required).
/// Store location: --store argument, LEVIATHAN_STORE environment variable, or
/// ~/.leviathantext/store.db.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (LeviathanException ex)
        {
            Console.Error.WriteLine($"error: {ex.Error}");
            return 2;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var (storePath, rest) = ExtractStore(args);
        using var store = new LocalStore(storePath);
        var projects = new ProjectService(store);
        var interchange = new InterchangeService(projects);

        if (rest.Length == 0) return Help();

        switch (rest[0])
        {
            case "help": return Help();

            case "project" when rest.Length >= 2 && rest[1] == "new":
            {
                var name = rest.Length >= 3 ? rest[2] : throw new ArgumentException("Usage: project new <name>");
                var project = projects.Create(name);
                Console.WriteLine(project.Id);
                return 0;
            }

            case "project" when rest.Length >= 2 && rest[1] == "list":
            {
                foreach (var (id, name) in projects.List())
                    Console.WriteLine($"{id}\t{name}");
                return 0;
            }

            case "import" when rest.Length >= 4 && rest[1] == "iiif":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var (imported, manifest, warnings) = interchange.ImportIiif(project, rest[3]);
                Console.WriteLine($"imported object {imported.Id}, surface {imported.Surfaces[0].Id}, " +
                                  $"captures {imported.Surfaces[0].Captures.Count}");
                if (manifest.ManifestLabel is not null) Console.WriteLine($"manifest: {manifest.ManifestLabel}");
                if (manifest.RightsStatement is not null) Console.WriteLine($"rights: {manifest.RightsStatement}");
                foreach (var warning in warnings) Console.WriteLine($"note: {warning}");
                return 0;
            }

            case "import" when rest.Length >= 4 && rest[1] == "dir":
            {
                // Collection-scale import: every PAGE XML in a directory, through the
                // bounded-queue pipeline (memory stays bounded; failures are reported
                // per file and do not stop the run).
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var directory = rest[3];
                var files = Directory.GetFiles(directory, "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
                if (files.Count == 0) { Console.Error.WriteLine("no PAGE XML files found"); return 1; }

                var current = project;
                var results = StreamingPipeline.RunAsync(
                    files.Select(file => (new EntityId(EntityKind.Line, Path.GetFileNameWithoutExtension(file)), (Func<CancellationToken, Task>)(_ =>
                    {
                        var (imported, _) = interchange.ImportPage(current, file);
                        current = projects.Load(current.Id);
                        return Task.CompletedTask;
                    }))),
                    maxDegreeOfParallelism: 1).GetAwaiter().GetResult();

                var ok = results.Count(r => r.Succeeded);
                Console.WriteLine($"imported {ok}/{files.Count} PAGE files");
                foreach (var failure in results.Where(r => !r.Succeeded).Take(5))
                    Console.Error.WriteLine($"failed: {failure.ItemId}: {failure.Error}");
                return ok == files.Count ? 0 : 1;
            }

            case "import" when rest.Length >= 4 && rest[1] == "page":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var (imported, unsupported) = interchange.ImportPage(project, rest[3]);
                Console.WriteLine($"imported object {imported.Id}, surface {imported.Surfaces[0].Id}, lines {imported.Surfaces[0].Lines.Count}");
                foreach (var issue in unsupported)
                    Console.WriteLine($"unsupported: {issue.Element}: {issue.Description}");
                return 0;
            }

            case "export" when rest.Length >= 3 && rest[1] == "training":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var document = TrainingDataExport.Export(project);
                Console.WriteLine(TrainingDataExport.ToJson(document));
                Console.Error.WriteLine($"clean={document.CleanLines} uncertain={document.UncertainLines} excluded={document.ExcludedLines}");
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "diplomatic":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var (text, report) = interchange.ExportDiplomatic(project, EntityId.Parse(rest[3], EntityKind.Surface));
                Console.WriteLine(text);
                Console.Error.WriteLine(report);
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "page":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                Console.WriteLine(interchange.ExportPage(project, EntityId.Parse(rest[3], EntityKind.Surface)));
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "tei":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceId = EntityId.Parse(rest[3], EntityKind.Surface);
                Console.WriteLine(interchange.ExportTei(project, surfaceId, $"LeviathanText export {surfaceId}"));
                return 0;
            }

            case "layout" when rest.Length >= 4 && rest[1] == "detect":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceId = EntityId.Parse(rest[3], EntityKind.Surface);
                var cache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
                var layout = new LayoutService(store, new ImageResolver(store, cache));
                var outcome = layout.Detect(project, surfaceId);
                Console.WriteLine($"detected {outcome.LineCount} lines ({outcome.Method}) in region(s) of {outcome.Surface.Id}");
                return 0;
            }

            case "train" when rest.Length >= 3 && rest[1] == "lines":
            {
                // Self-service training: PAGE+image corpus -> checkpoint -> model package.
                var dataDir = rest[2];
                var outDir = rest.Length >= 5 && rest[3] == "--out" ? rest[4] : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "models", "trained");
                var epochs = rest.Length >= 7 && rest[5] == "--epochs" ? int.Parse(rest[6]) : 120;

                var snapshot = P01LineDataset.Load(dataDir);
                if (snapshot.Train.Count == 0) throw new ArgumentException("dataset produced no training lines");
                var vocabulary = snapshot.Train.Concat(snapshot.Dev)
                    .SelectMany(l => LeviathanText.Core.Unicode.Graphemes.Enumerate(l.Text).Select(g => g.Text))
                    .Distinct(StringComparer.Ordinal).ToList();
                var labels = new LabelSet(vocabulary);

                var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-train-cli", Guid.NewGuid().ToString("N"));
                var config = new TrainingConfig
                {
                    Epochs = epochs, BatchSize = 8, LearningRate = 3e-3, Seed = 42,
                    EarlyStoppingPatience = epochs, HiddenSize = 64,
                    ModelName = Path.GetFileName(Path.TrimEndingDirectorySeparator(outDir)),
                    CheckpointDirectory = checkpointDir,
                };
                var trainer = new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config);
                var run = trainer.Fit(snapshot.Train, snapshot.Dev);

                var splitAssignment = string.Join('\n', snapshot.WitnessSplitMap.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
                var splitHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(splitAssignment))).ToLowerInvariant();
                var bestDigest = File.Exists(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"))
                    ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best")))).ToLowerInvariant()
                    : "";

                var manifest = new TrainingRunManifest
                {
                    ModelName = config.ModelName,
                    ConfigHash = config.ComputeHash(),
                    NumClasses = labels.NumClasses,
                    HiddenSize = config.HiddenSize,
                    WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
                    InputHeightPixels = CrnnModel.InputHeight,
                    Dropout = 0,
                    AugmentTraining = false,
                    WeightDecay = 0,
                    TrainLines = snapshot.Train.Count,
                    DevLines = snapshot.Dev.Count,
                    TestLockedLines = snapshot.TestLocked.Count,
                    SplitHash = splitHash,
                    LabelSetHash = CheckpointStore.LabelSetHash(labels),
                    CheckpointDigest = bestDigest,
                    BestValidationCer = run.BestValidationCer,
                    EpochsCompleted = run.EpochsCompleted,
                    CreatedAt = DateTimeOffset.UtcNow,
                    History = run.History,
                };
                var package = ModelPackageWriter.Write(outDir, checkpointDir, config.ModelName, manifest,
                    "Pilot model trained from a PAGE/image corpus; labels not expert-adjudicated.");

                Console.WriteLine($"trained {config.ModelName}: best dev CER {run.BestValidationCer:0.###} over {run.EpochsCompleted} epochs");
                Console.WriteLine($"package: {package.Directory}");
                return 0;
            }

            case "recognize" when rest.Length >= 4 && rest[1] == "surface":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceId = EntityId.Parse(rest[3], EntityKind.Surface);
                var packageDir = rest.Length >= 6 && rest[4] == "--package" ? rest[5] : null;
                packageDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "models", "p01-pilot");

                // Load the packaged model (manifest + label hash + weights verified by the loader);
                // a package that cannot prove its identity fails with ModelIncompatible.
                var loaded = RecognitionModelLoader.Load(packageDir);

                var imageCache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
                var pipeline = new RecognitionPipeline(store, new ImageResolver(store, imageCache));
                var outcome = pipeline.Run(project, surfaceId, new RecognitionPipeline.Options
                {
                    ModelDirectory = packageDir,
                    ModelName = Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDir)),
                    ModelFactory = () => loaded.Model,
                    LabelSetFactory = () => loaded.Labels,
                });

                foreach (var line in outcome.Lines)
                    Console.WriteLine($"{line.LineId}\t{line.L0Text}");
                if (outcome.SkippedLines.Count > 0)
                    Console.Error.WriteLine($"skipped {outcome.SkippedLines.Count} line(s) without geometry: {string.Join(", ", outcome.SkippedLines)}");
                foreach (var stage in outcome.Stages)
                    Console.Error.WriteLine(stage);
                return 0;
            }

            default:
                return Help();
        }
    }

    private static (string StorePath, string[] Args) ExtractStore(string[] args)
    {
        string? storePath = null;
        var rest = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--store" && i + 1 < args.Length) { storePath = args[++i]; continue; }
            rest.Add(args[i]);
        }
        storePath ??= Environment.GetEnvironmentVariable("LEVIATHAN_STORE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "store.db");
        return (storePath, [.. rest]);
    }

    private static int Help()
    {
        Console.WriteLine("""
            LeviathanText CLI (local; no web server required)

              leviathan [--store <path>] project new <name>
              leviathan [--store <path>] project list
              leviathan [--store <path>] import page <projectId> <page.xml>
              leviathan [--store <path>] import dir <projectId> <page-xml-dir>
              leviathan [--store <path>] import iiif <projectId> <manifest.json>
              leviathan [--store <path>] layout detect <projectId> <surfaceId>
              leviathan [--store <path>] export diplomatic <projectId> <surfaceId>
              leviathan [--store <path>] export page <projectId> <surfaceId>
              leviathan [--store <path>] export tei <projectId> <surfaceId>
              leviathan [--store <path>] recognize surface <projectId> <surfaceId> [--package <model-package-dir>]

            Lossy exports print a loss report to stderr; nothing is silently flattened.
            Model packages are self-verifying (manifest + label hash + weights).
            """);
        return 0;
    }
}
