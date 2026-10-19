using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
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

            case "backup" when rest.Length >= 2:
            {
                // Consistent backup set: VACUUM INTO database copy + cached images + manifest.
                var target = rest[1];
                Directory.CreateDirectory(target);
                var dbTarget = Path.Combine(target, "store.db");
                store.BackupTo(dbTarget);

                var imagesDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
                var manifest = new Dictionary<string, object>
                {
                    ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["storeChecksum"] = Sha256File(dbTarget),
                    ["images"] = new List<object>(),
                };
                var imageEntries = (List<object>)manifest["images"]!;
                if (Directory.Exists(imagesDir))
                {
                    var imageTarget = Path.Combine(target, "images");
                    Directory.CreateDirectory(imageTarget);
                    foreach (var file in Directory.GetFiles(imagesDir))
                    {
                        var name = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(imageTarget, name), overwrite: true);
                        imageEntries.Add(new Dictionary<string, object>
                        {
                            ["name"] = name,
                            ["sha256"] = Sha256File(file),
                            ["size"] = new FileInfo(file).Length,
                        });
                    }
                }
                File.WriteAllText(Path.Combine(target, "backup-manifest.json"),
                    System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"backup written: {target} ({imageEntries.Count} images)");
                return 0;
            }

            case "restore" when rest.Length >= 2:
            {
                var source = rest[1];
                var targetDb = rest.Length >= 4 && rest[2] == "--into" ? rest[3] : storePath;
                var force = rest.Contains("--force");

                var manifestPath = Path.Combine(source, "backup-manifest.json");
                if (!File.Exists(manifestPath)) { Console.Error.WriteLine("no backup-manifest.json in the backup"); return 1; }
                var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath)).RootElement;

                var sourceDb = Path.Combine(source, "store.db");
                var expected = manifest.GetProperty("storeChecksum").GetString();
                if (Sha256File(sourceDb) != expected)
                {
                    Console.Error.WriteLine("backup checksum mismatch; refusing to restore");
                    return 1;
                }

                // Verify every image BEFORE writing anything: a corrupt set must not leave
                // a half-restored store behind.
                var imagesSource = Path.Combine(source, "images");
                var images = manifest.GetProperty("images").EnumerateArray().ToList();
                foreach (var property in images)
                {
                    var name = property.GetProperty("name").GetString()!;
                    var file = Path.Combine(imagesSource, name);
                    if (!File.Exists(file) || Sha256File(file) != property.GetProperty("sha256").GetString())
                    {
                        Console.Error.WriteLine($"image checksum mismatch or missing ({name}); refusing to restore");
                        return 1;
                    }
                }

                if (File.Exists(targetDb) && !force)
                {
                    Console.Error.WriteLine($"target store exists ({targetDb}); pass --force to overwrite");
                    return 1;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetDb))!);
                File.Copy(sourceDb, targetDb, overwrite: true);

                if (images.Count > 0)
                {
                    var imagesTarget = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDb))!, "images");
                    Directory.CreateDirectory(imagesTarget);
                    foreach (var property in images)
                    {
                        var name = property.GetProperty("name").GetString()!;
                        File.Copy(Path.Combine(imagesSource, name), Path.Combine(imagesTarget, name), overwrite: true);
                    }
                }
                Console.WriteLine($"restored: {source} -> {targetDb}");
                return 0;
            }

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

            case "import" when rest.Length >= 4 && rest[1] == "image":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var kind = rest.Length >= 6 && rest[4] == "--kind" ? rest[5] : "page";
                var imported = interchange.ImportImage(project, rest[3], kind);
                Console.WriteLine($"imported object {imported.Id}, surface {imported.Surfaces[0].Id} (no layout; draw or detect lines)");
                return 0;
            }

            case "import" when rest.Length >= 5 && rest[1] is "alto" or "hocr":
            {
                // Legacy OCR migration: bounded ALTO/hOCR subset + the page image.
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var kind = rest.Length >= 7 && rest[5] == "--kind" ? rest[6] : "page";
                var (imported, unsupported) = interchange.ImportLegacyLayout(project, rest[3], rest[4], rest[1], kind);
                foreach (var issue in unsupported)
                    Console.Error.WriteLine($"unsupported: {issue.Element}: {issue.Description}");
                Console.WriteLine($"imported object {imported.Id}, surface {imported.Surfaces[0].Id}");
                return 0;
            }

            case "import" when rest.Length >= 4 && rest[1] == "page":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var kind = rest.Length >= 6 && rest[4] == "--kind" ? rest[5] : "page";
                var (imported, unsupported) = interchange.ImportPage(project, rest[3], kind);
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

            case "export" when rest.Length >= 4 && rest[1] == "annotations":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                File.WriteAllText(rest[3], AnnotationCsv.Export(project));
                Console.WriteLine($"wrote {rest[3]} (annotation CSV; native project remains authoritative)");
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "lines":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                File.WriteAllText(rest[3], LineCsv.Export(project));
                Console.WriteLine($"wrote {rest[3]} (CSV; native project remains authoritative)");
                return 0;
            }

            case "export" when rest.Length >= 3 && rest[1] == "bundle":
            {
                if (rest.Length < 4) { Console.Error.WriteLine("usage: export bundle <projectId> <out.zip>"); return 1; }
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var result = ProjectBundle.Export(project, rest[3]);
                Console.WriteLine($"bundle: {result.Path} ({result.FileCount} files, {result.SizeBytes} bytes)");
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "alto":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                Console.WriteLine(interchange.ExportAlto(project, EntityId.Parse(rest[3], EntityKind.Surface)));
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "hocr":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                Console.WriteLine(interchange.ExportHocr(project, EntityId.Parse(rest[3], EntityKind.Surface)));
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "tei":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceId = EntityId.Parse(rest[3], EntityKind.Surface);
                Console.WriteLine(interchange.ExportTei(project, surfaceId, $"LeviathanText export {surfaceId}"));
                return 0;
            }

            case "search" when rest.Length >= 3:
            {
                var project = projects.Load(EntityId.Parse(rest[1], EntityKind.Collection));
                var mode = rest.Contains("--base") ? "base" : "literal";
                var result = SearchService.Search(project, rest[2], mode);
                foreach (var hit in result.Hits)
                    Console.WriteLine($"{hit.SurfaceId}\t{hit.LineId}\t{hit.Layer}\t{hit.Match.Start}..{hit.Match.End}\t{hit.Snippet}");
                Console.Error.WriteLine($"{result.TotalHits} hit(s){(result.Truncated ? " (truncated)" : "")} mode={result.Mode}");
                return 0;
            }

            case "validate" when rest.Length >= 3 && rest[1] == "text":
            {
                // Greek transcription-profile inspection (no rewriting): unknown scalars,
                // Latin letters and unpaired combining marks are reported per surface.
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var strict = rest.Contains("--strict");
                var report = ProfileReport.Build(project);
                foreach (var surface in report.Surfaces)
                {
                    foreach (var issue in surface.Issues)
                        Console.Error.WriteLine($"  {issue.LineId} scalar {issue.ScalarOffset}: {issue.Category} {issue.Scalar}");
                    Console.WriteLine($"{surface.SurfaceId}\tlines={surface.Lines}\tflagged={surface.Flagged}\tprofile={report.Profile}@{report.Version}");
                }
                Console.Error.WriteLine($"flagged {report.FlaggedTotal} line(s) across {report.Surfaces.Count} surface(s)");
                return strict && report.FlaggedTotal > 0 ? 1 : 0;
            }

            case "jobs" when rest.Length >= 2 && rest[1] == "list":
            {
                var limit = rest.Length >= 4 && rest[2] == "--limit" ? int.Parse(rest[3]) : 20;
                foreach (var job in store.LoadJobs(limit))
                    Console.WriteLine($"{job.Id}\t{job.Type}\t{job.Status}\tretries={job.RetriesUsed}");
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
                var profileStrict = rest.Contains("--profile-strict");

                var snapshot = P01LineDataset.Load(dataDir);
                if (profileStrict)
                {
                    // Exclude labels with scalars outside the transcription profile (markup
                    // leakage, unknown symbols). Reported, never silently dropped.
                    static bool Outside(string text) => GreekTextProfile.Inspect(text).Issues
                        .Any(i => i.Category == GreekTextProfile.Category.Other);
                    var excludedTrain = snapshot.Train.Count(l => Outside(l.Text));
                    var excludedDev = snapshot.Dev.Count(l => Outside(l.Text));
                    snapshot = snapshot with
                    {
                        Train = [.. snapshot.Train.Where(l => !Outside(l.Text))],
                        Dev = [.. snapshot.Dev.Where(l => !Outside(l.Text))],
                    };
                    Console.Error.WriteLine($"profile-strict: excluded {excludedTrain} train / {excludedDev} dev out-of-profile lines");
                }
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

            case "recognize" when rest.Length >= 4 && rest[1] == "project":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var packageDir = rest.Length >= 5 && rest[3] == "--package" ? rest[4] : null;
                packageDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "models", "p01-pilot");
                var loaded = RecognitionModelLoader.Load(packageDir);
                var modelName = loaded.Manifest?.ModelName
                    ?? (Directory.GetFiles(packageDir, "*.best.json") is [var only]
                        ? Path.GetFileName(only).Replace(".best.json", string.Empty, StringComparison.Ordinal)
                        : Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDir)));
                var modelPath = CheckpointStore.ModelPath(packageDir, modelName, ".best");
                var metadata = loaded.Metadata;

                var imageCache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
                var pipeline = new RecognitionPipeline(store, new ImageResolver(store, imageCache));
                int surfaces = 0, lines = 0, failed = 0;
                var reviewEntries = new List<RecognitionPipeline.LineRecognition>();
                foreach (var surface in project.Objects.SelectMany(o => o.Surfaces).ToList())
                {
                    if (surface.Lines.Count == 0) continue;
                    try
                    {
                        var current = projects.Load(project.Id);
                        var outcome = pipeline.Run(current, surface.Id, new RecognitionPipeline.Options
                        {
                            ModelDirectory = packageDir,
                            ModelName = modelName,
                            ModelFactory = () =>
                            {
                                var model = new CrnnModel(loaded.Labels.NumClasses, metadata.HiddenSize,
                                    metadata.WidthDownsampleFactor, metadata.InputHeightPixels, metadata.Dropout);
                                model.load(modelPath);
                                return model;
                            },
                            LabelSetFactory = () => loaded.Labels,
                        });
                        surfaces++;
                        lines += outcome.Lines.Count;
                        reviewEntries.AddRange(outcome.Lines);
                        Console.WriteLine($"{surface.Id}\t{outcome.Lines.Count} lines");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failed++;
                        Console.Error.WriteLine($"{surface.Id}: {ex.Message}");
                    }
                }
                Console.Error.WriteLine($"surfaces={surfaces};lines={lines};failed={failed}");
                var queueIndex = Array.IndexOf(rest, "--review-queue");
                if (queueIndex >= 0 && queueIndex + 1 < rest.Length)
                {
                    var queue = ReviewQueue.Build(reviewEntries);
                    File.WriteAllText(rest[queueIndex + 1], ReviewQueue.ToCsv(queue));
                    Console.Error.WriteLine($"review queue: {queue.Count} lines -> {rest[queueIndex + 1]} " +
                                            $"(first: {string.Join(", ", queue.Take(3).Select(e => e.LineId))})");
                }
                return surfaces > 0 ? 0 : 1;
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
                var surfaceQueueIndex = Array.IndexOf(rest, "--review-queue");
                if (surfaceQueueIndex >= 0 && surfaceQueueIndex + 1 < rest.Length)
                {
                    var queue = ReviewQueue.Build(outcome.Lines);
                    File.WriteAllText(rest[surfaceQueueIndex + 1], ReviewQueue.ToCsv(queue));
                    Console.Error.WriteLine($"review queue: {queue.Count} lines -> {rest[surfaceQueueIndex + 1]}");
                }
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

    private static string Sha256File(string path) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    private static int Help()
    {
        Console.WriteLine("""
            LeviathanText CLI (local; no web server required)

              leviathan [--store <path>] project new <name>
              leviathan [--store <path>] backup <target-dir>
              leviathan [--store <path>] restore <backup-dir> [--into <store>] [--force]
              leviathan [--store <path>] project list
              leviathan [--store <path>] import page <projectId> <page.xml>
              leviathan [--store <path>] import dir <projectId> <page-xml-dir>
              leviathan [--store <path>] import iiif <projectId> <manifest.json>
              leviathan [--store <path>] layout detect <projectId> <surfaceId>
              leviathan [--store <path>] export diplomatic <projectId> <surfaceId>
              leviathan [--store <path>] export page <projectId> <surfaceId>
              leviathan [--store <path>] export tei <projectId> <surfaceId>
              leviathan [--store <path>] export alto <projectId> <surfaceId>
              leviathan [--store <path>] recognize surface <projectId> <surfaceId> [--package <model-package-dir>]

            Lossy exports print a loss report to stderr; nothing is silently flattened.
            Model packages are self-verifying (manifest + label hash + weights).
            """);
        return 0;
    }
}
