using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;
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

            case "import" when rest.Length >= 4 && rest[1] == "pdf":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                string? Flag(string name) =>
                    Array.IndexOf(rest, name) is var index && index >= 0 && index + 1 < rest.Length ? rest[index + 1] : null;
                var kind = Flag("--kind") ?? "page";
                var dpi = Flag("--dpi") is { } d ? int.Parse(d) : PdfRenderer.DefaultDpi;
                var maxPages = Flag("--max-pages") is { } m ? int.Parse(m) : PdfRenderer.MaxPages;
                var extractText = Array.IndexOf(rest, "--text") >= 0;
                var outputDirectory = Flag("--out") ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".leviathantext", "pdf", project.Id.Suffix,
                    $"{Path.GetFileNameWithoutExtension(rest[3])}-{Guid.NewGuid().ToString("N")[..8]}");

                var (imported, pages, warnings) = interchange.ImportPdf(project, rest[3], outputDirectory, kind, dpi, maxPages, extractText);
                Console.WriteLine($"imported object {imported.Id}, surfaces {imported.Surfaces.Count}, " +
                                  $"lines {imported.Surfaces.Sum(s => s.Lines.Count)}, page images in {outputDirectory}");
                foreach (var page in pages.Take(3))
                    Console.WriteLine($"  page {page.PageNumber}: {page.WidthPx}x{page.HeightPx} {page.ImagePath}");
                if (pages.Count > 3) Console.WriteLine($"  … {pages.Count - 3} more page(s)");
                foreach (var warning in warnings) Console.WriteLine($"note: {warning}");
                return 0;
            }

            case "import" when rest.Length >= 4 && rest[1] == "image":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var kind = rest.Length >= 6 && rest[4] == "--kind" ? rest[5] : "page";
                var imported = interchange.ImportImage(project, rest[3], kind);
                Console.WriteLine($"imported object {imported.Id}, surface {imported.Surfaces[0].Id} (no layout; draw or detect lines)");
                return 0;
            }

            case "import" when rest.Length >= 5 && rest[1] == "epidoc-dir":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var limitIndex = Array.IndexOf(rest, "--limit");
                var limit = limitIndex >= 0 && limitIndex + 1 < rest.Length ? int.Parse(rest[limitIndex + 1]) : 0;
                var (imported, skipped, missing, notes) = interchange.ImportEpiDocDirectory(project, rest[3], rest[4], limit: limit);
                foreach (var note in notes) Console.Error.WriteLine($"skipped: {note}");
                if (missing.Count > 0) Console.Error.WriteLine($"missing images (first {missing.Count}): {string.Join(", ", missing)}");
                Console.WriteLine($"imported {imported} EpiDoc record(s); skipped {skipped} (missing image or parse error)");
                return imported > 0 ? 0 : 1;
            }

            case "import" when rest.Length >= 4 && rest[1] == "annotations":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var result = AnnotationCsvImport.Import(project, File.ReadAllText(rest[3]),
                    new TranscriptionService(store), store);
                foreach (var row in result.Rows.Where(r => !r.Applied).Take(10))
                    Console.Error.WriteLine($"skipped {row.LineId} ({row.Kind}): {row.Reason}");
                Console.WriteLine($"applied {result.Applied} annotation(s), skipped {result.Skipped} row(s)");
                return 0;
            }

            case "import" when rest.Length >= 4 && rest[1] == "line-text":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var layerIndex = Array.IndexOf(rest, "--layer");
                var layer = layerIndex >= 0 && layerIndex + 1 < rest.Length
                    ? Enum.Parse<TextLayer>(rest[layerIndex + 1], ignoreCase: true)
                    : TextLayer.L1Diplomatic;
                var authorIndex = Array.IndexOf(rest, "--author");
                var author = authorIndex >= 0 && authorIndex + 1 < rest.Length ? rest[authorIndex + 1] : "csv-import";

                var result = LineTextImport.Import(project, File.ReadAllText(rest[3]), layer, author,
                    new TranscriptionService(store), store);
                foreach (var row in result.Rows.Where(r => !r.Applied).Take(10))
                    Console.Error.WriteLine($"skipped {row.LineId}: {row.Reason}");
                Console.WriteLine($"applied {result.Applied} revision(s), skipped {result.Skipped} row(s), layer {layer}");
                return 0;
            }

            case "import" when rest.Length >= 5 && rest[1] == "epidoc":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var kind = rest.Length >= 7 && rest[5] == "--kind" ? rest[6] : "inscription-face";
                var (imported, notes, imageUrl) = interchange.ImportEpiDoc(project, rest[3], rest[4], kind);
                foreach (var note in notes)
                    Console.Error.WriteLine($"{note.Severity}: {note.Element}: {note.Description}");
                Console.WriteLine($"imported {imported.Surfaces[0].Lines.Count} reference line(s) as L2; " +
                                  $"surface {imported.Surfaces[0].Id}; draw geometry in the editor (image URL: {imageUrl ?? "none"})");
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

            case "audit" when rest.Length >= 2 && rest[1] == "list":
            {
                var limitIndex = Array.IndexOf(rest, "--limit");
                var limit = limitIndex >= 0 && limitIndex + 1 < rest.Length ? int.Parse(rest[limitIndex + 1]) : 50;
                foreach (var entry in store.LoadAuthAudit(limit))
                    Console.WriteLine($"{entry.At:O}\t{entry.Action}\t{entry.UserName ?? "-"}\t{entry.Role?.ToString() ?? "-"}\t{entry.Method} {entry.Path}\t{entry.Status}");
                return 0;
            }

            case "evaluate" when rest.Length >= 4 && rest[1] == "lines":
            {
                string? Flag(string name) =>
                    Array.IndexOf(rest, name) is var index && index >= 0 && index + 1 < rest.Length ? rest[index + 1] : null;
                var datasetDirectory = rest[2];
                var packageDirectory = Flag("--package")
                    ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = "--package", Message = "A verified model package is required." });
                var split = Flag("--split") ?? "dev";
                if (split is not ("dev" or "train"))
                    throw new LeviathanException(new LeviathanError { Code = ErrorCode.StageContractViolation, Scope = "--split", Message = "Routine evaluation supports dev or train; locked test labels are not used here." });
                var imageExtension = Flag("--image-ext") ?? ".jpg";
                var csvPath = Flag("--out") ?? "evaluation.csv";
                var jsonPath = Flag("--json") ?? Path.ChangeExtension(csvPath, ".json");
                var reportPath = Flag("--report") ?? Path.ChangeExtension(csvPath, ".md");

                var snapshot = P01LineDataset.Load(datasetDirectory, imageExtension);
                var lines = split == "dev" ? snapshot.Dev : snapshot.Train;
                if (lines.Count == 0)
                    throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = split, Message = $"No labeled {split} lines were found in the dataset." });

                var loaded = RecognitionModelLoader.Load(packageDirectory);
                var recognizer = loaded.CreateRecognizer();
                var results = recognizer.Recognize(lines.Select(l => l.Image).ToList());
                if (results.Count != lines.Count)
                    throw new LeviathanException(new LeviathanError { Code = ErrorCode.StageContractViolation, Scope = "evaluation", Message = "Recognizer result count does not match the evaluated line count." });

                var pairs = lines.Select((line, index) => (line.Text, (string?)results[index].Text));
                var (rows, summary) = LineEvaluationReport.Build(pairs);
                var packageName = loaded.Manifest?.ModelName ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDirectory));
                var digestInput = string.Join("\n", snapshot.WitnessSplitMap.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => $"{p.Key}={p.Value}"));
                var splitDigest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(digestInput)));

                var reportContext = new Dictionary<string, string>
                {
                    ["package"] = Path.GetFullPath(packageDirectory),
                    ["model"] = packageName,
                    ["dataset"] = Path.GetFullPath(datasetDirectory),
                    ["split"] = split,
                    ["splitDigest"] = splitDigest,
                };
                File.WriteAllText(csvPath, LineEvaluationReport.ToCsv(rows));
                File.WriteAllText(jsonPath, LineEvaluationReport.ToJson(summary, reportContext));
                File.WriteAllText(reportPath, LineEvaluationReport.ToMarkdown(rows, summary, reportContext));

                var markErrorRate = summary.ReferenceScalars == 0 ? 0 : (double)summary.MarkErrors / summary.ReferenceScalars;
                var runId = $"run-{Guid.NewGuid().ToString("N")[..12]}";
                store.SaveRun(new RunRecord
                {
                    Id = runId,
                    Kind = "evaluation",
                    Name = $"{packageName}:{split}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "cli",
                    DatasetDigest = splitDigest,
                    Config = new Dictionary<string, string>
                    {
                        ["package"] = Path.GetFullPath(packageDirectory),
                        ["dataset"] = Path.GetFullPath(datasetDirectory),
                        ["split"] = split,
                        ["imageExtension"] = imageExtension,
                    },
                    Metrics = new Dictionary<string, double>
                    {
                        ["cer"] = summary.ScalarCer,
                        ["lines"] = summary.Lines,
                        ["referenceScalars"] = summary.ReferenceScalars,
                        ["markErrors"] = summary.MarkErrors,
                        ["markErrorRate"] = markErrorRate,
                    },
                    Artifacts = [csvPath, jsonPath, reportPath],
                    Notes = $"{split} split only; locked labels were not used. Labels are not necessarily specialist-adjudicated.",
                });

                Console.WriteLine($"evaluated {summary.Lines} {split} line(s): CER {summary.ScalarCer:0.###}, mark errors {summary.MarkErrors} ({markErrorRate:0.###})");
                Console.WriteLine($"csv: {csvPath}");
                Console.WriteLine($"json: {jsonPath}");
                Console.WriteLine($"report: {reportPath}");
                Console.WriteLine($"recorded run: {runId}");
                return 0;
            }

            case "runs" when rest.Length >= 2 && rest[1] == "list":
            {
                var kindIndex = Array.IndexOf(rest, "--kind");
                var kind = kindIndex >= 0 && kindIndex + 1 < rest.Length ? rest[kindIndex + 1] : null;
                var limitIndex = Array.IndexOf(rest, "--limit");
                var limit = limitIndex >= 0 && limitIndex + 1 < rest.Length ? int.Parse(rest[limitIndex + 1]) : 50;
                foreach (var run in store.LoadRuns(kind, limit))
                {
                    var metrics = string.Join(' ', run.Metrics
                        .Where(m => m.Key is "bestDevCer" or "cer" or "devF1" or "devPrecision" or "devRecall" or "markErrorRate")
                        .Select(m => $"{m.Key}={m.Value:0.###}"));
                    Console.WriteLine($"{run.Id}\t{run.Kind}\t{run.Name}\t{run.CreatedAt:O}\t{metrics}");
                }
                return 0;
            }

            case "runs" when rest.Length >= 3 && rest[1] == "show":
            {
                var run = store.LoadRun(rest[2])
                    ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = rest[2], Message = "No such run." });
                Console.WriteLine($"id: {run.Id}\nkind: {run.Kind}\nname: {run.Name}\ncreated: {run.CreatedAt:O}");
                Console.WriteLine($"dataset digest: {run.DatasetDigest ?? "-"}");
                Console.WriteLine("config:");
                foreach (var pair in run.Config.OrderBy(p => p.Key)) Console.WriteLine($"  {pair.Key}: {pair.Value}");
                Console.WriteLine("metrics:");
                foreach (var pair in run.Metrics.OrderBy(p => p.Key)) Console.WriteLine($"  {pair.Key}: {pair.Value:0.######}");
                if (run.History.Count > 0)
                    Console.WriteLine($"history ({run.History.Count} points): {string.Join(", ", run.History.Take(10).Select(v => v.ToString("0.###")))}");
                Console.WriteLine("artifacts:");
                foreach (var artifact in run.Artifacts) Console.WriteLine($"  {artifact}");
                if (!string.IsNullOrEmpty(run.Notes)) Console.WriteLine($"notes: {run.Notes}");
                return 0;
            }

            case "runs" when rest.Length >= 5 && rest[1] == "regress":
            {
                var metric = rest[2];
                var baseline = store.LoadRun(rest[3])
                    ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = rest[3], Message = "No such baseline run." });
                var candidate = store.LoadRun(rest[4])
                    ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = rest[4], Message = "No such candidate run." });
                if (!baseline.Metrics.TryGetValue(metric, out var baselineValue))
                    throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = rest[3], Message = $"Baseline has no '{metric}' metric." });
                if (!candidate.Metrics.TryGetValue(metric, out var candidateValue))
                    throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = rest[4], Message = $"Candidate has no '{metric}' metric." });

                bool higher = Array.IndexOf(rest, "--higher") >= 0;
                var toleranceIndex = Array.IndexOf(rest, "--max-regression");
                double tolerance = toleranceIndex >= 0 && toleranceIndex + 1 < rest.Length
                    ? double.Parse(rest[toleranceIndex + 1], System.Globalization.CultureInfo.InvariantCulture)
                    : 0;
                double regression = higher ? baselineValue - candidateValue : candidateValue - baselineValue;
                bool pass = regression <= tolerance + 1e-12;
                Console.WriteLine($"metric {metric}: baseline={baselineValue:0.######} candidate={candidateValue:0.######} " +
                                  $"regression={regression:0.######} tolerance={tolerance:0.######} -> {(pass ? "PASS" : "FAIL")}");
                return pass ? 0 : 1;
            }

            case "runs" when rest.Length >= 3 && rest[1] == "compare":
            {
                var metric = rest[2];
                var higherIndex = Array.IndexOf(rest, "--higher");
                bool higher = higherIndex >= 0;
                var runs = store.LoadRuns()
                    .Where(r => r.Metrics.ContainsKey(metric))
                    .OrderBy(r => higher ? -r.Metrics[metric] : r.Metrics[metric])
                    .ToList();
                foreach (var run in runs)
                    Console.WriteLine($"{run.Metrics[metric]:0.######}\t{run.Id}\t{run.Kind}\t{run.Name}");
                return runs.Count > 0 ? 0 : 1;
            }

            case "user" when rest.Length >= 3 && rest[1] == "add":
            {
                var roleIndex = Array.IndexOf(rest, "--role");
                var roleText = roleIndex >= 0 && roleIndex + 1 < rest.Length ? rest[roleIndex + 1] : "annotator";
                if (!Enum.TryParse<UserRole>(roleText, ignoreCase: true, out var role))
                {
                    Console.Error.WriteLine("role must be reader, annotator, reviewer or admin");
                    return 1;
                }
                var key = LeviathanText.Infrastructure.UserKeys.NewKey();
                var account = store.CreateUser(rest[2], role, LeviathanText.Infrastructure.UserKeys.Hash(key));
                Console.WriteLine($"created {account.Id} ({account.Name}, {account.Role})");
                Console.WriteLine($"api key (shown once): {key}");
                return 0;
            }

            case "user" when rest.Length >= 2 && rest[1] == "list":
            {
                foreach (var user in store.LoadUsers())
                    Console.WriteLine($"{user.Id}\t{user.Name}\t{user.Role}\t{(user.Disabled ? "disabled" : "active")}\t{user.CreatedAt:O}");
                return 0;
            }

            case "user" when rest.Length >= 3 && rest[1] == "disable":
            {
                store.DisableUser(rest[2]);
                Console.WriteLine($"disabled {rest[2]}");
                return 0;
            }

            case "user" when rest.Length >= 3 && rest[1] == "rotate":
            {
                var key = LeviathanText.Infrastructure.UserKeys.NewKey();
                store.UpdateUserKey(rest[2], LeviathanText.Infrastructure.UserKeys.Hash(key));
                Console.WriteLine($"rotated key for {rest[2]} (shown once): {key}");
                return 0;
            }

            case "review" when rest.Length >= 4 && rest[1] == "line":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var lineId = EntityId.Parse(rest[3], EntityKind.Line);
                var reviewerIndex = Array.IndexOf(rest, "--reviewer");
                var outcomeIndex = Array.IndexOf(rest, "--outcome");
                var noteIndex = Array.IndexOf(rest, "--note");
                var textIndex = Array.IndexOf(rest, "--text");
                if (reviewerIndex < 0 || reviewerIndex + 1 >= rest.Length || outcomeIndex < 0 || outcomeIndex + 1 >= rest.Length)
                {
                    Console.Error.WriteLine("usage: review line <projectId> <lineId> --reviewer <id> --outcome accept|correct|dispute [--note ...] [--text ...]");
                    return 1;
                }
                if (!Enum.TryParse<ReviewOutcome>(rest[outcomeIndex + 1], ignoreCase: true, out var outcome))
                {
                    Console.Error.WriteLine("outcome must be accept, correct or dispute");
                    return 1;
                }
                var note = noteIndex >= 0 && noteIndex + 1 < rest.Length ? rest[noteIndex + 1] : null;
                var correction = textIndex >= 0 && textIndex + 1 < rest.Length ? rest[textIndex + 1] : null;
                var (_, _, decision, revision) = new ReviewService(store).Record(project, lineId,
                    rest[reviewerIndex + 1], outcome, note, correction);
                Console.WriteLine($"recorded {decision.Outcome} by {decision.Reviewer} for {lineId}" +
                                  (revision is null ? "" : $" (new revision {revision.Id})"));
                return 0;
            }

            case "review" when rest.Length >= 3 && rest[1] == "summary":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceIndex = Array.IndexOf(rest, "--surface");
                EntityId? surfaceId = surfaceIndex >= 0 && surfaceIndex + 1 < rest.Length
                    ? EntityId.Parse(rest[surfaceIndex + 1], EntityKind.Surface)
                    : null;
                var summary = new ReviewService(store).Summarize(project, surfaceId);
                Console.WriteLine($"transcribed={summary.TranscribedLines} reviewed={summary.ReviewedLines} " +
                                  $"double-reviewed={summary.DoubleReviewedLines} disputed={summary.DisputedLines} " +
                                  $"needs-adjudication={summary.NeedsAdjudication}");
                foreach (var line in summary.Lines.Where(l => l.NeedsAdjudication).Take(20))
                    Console.WriteLine($"  {line.LineId}: {string.Join(", ", line.Reviewers)} -> {string.Join(", ", line.Outcomes)}");
                return 0;
            }

            case "export" when rest.Length >= 4 && rest[1] == "adjudication":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                File.WriteAllText(rest[3], new ReviewService(store).ExportAdjudicationCsv(project));
                Console.WriteLine($"wrote {rest[3]} (lines needing adjudication)");
                return 0;
            }

            case "data" when rest.Length >= 4 && rest[1] == "inventory-inscriptions":
            {
                var records = rest[2];
                var imageIndex = Array.IndexOf(rest, "--images");
                var images = imageIndex >= 0 && imageIndex + 1 < rest.Length ? rest[imageIndex + 1] : null;
                var limitIndex = Array.IndexOf(rest, "--limit");
                var limit = limitIndex >= 0 && limitIndex + 1 < rest.Length ? int.Parse(rest[limitIndex + 1]) : 0;
                var outIndex = Array.IndexOf(rest, "--out");
                var outFile = outIndex >= 0 && outIndex + 1 < rest.Length ? rest[outIndex + 1] : null;

                var summary = InscriptionInventory.Scan(records, images, limit);
                var csv = InscriptionInventory.ToCsv(summary.Items);
                if (outFile is not null)
                {
                    File.WriteAllText(outFile, csv);
                    Console.WriteLine($"wrote {outFile}");
                }
                else Console.Write(csv);

                Console.Error.WriteLine($"records={summary.Records} edition={summary.WithEdition} greek={summary.WithGreekText} " +
                                        $"active-graphic={summary.WithActiveGraphic} greek+graphic={summary.WithGreekAndGraphic} " +
                                        $"screen={summary.WithScreenGraphic} local-image={summary.WithLocalImage} " +
                                        $"edition-lines={summary.TotalEditionLines}");
                return 0;
            }

            case "validate" when rest.Length >= 3 && rest[1] == "manifests":
            {
                var directory = rest[2];
                var strict = rest.Contains("--strict");
                var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal).ToList();
                int errors = 0, warnings = 0;
                foreach (var file in files)
                {
                    var issues = CorpusManifestAudit.Audit(File.ReadAllText(file));
                    errors += issues.Count(i => i.Severity == CorpusManifestAudit.Severity.Error);
                    warnings += issues.Count(i => i.Severity == CorpusManifestAudit.Severity.Warning);
                    foreach (var issue in issues)
                        Console.Error.WriteLine($"{Path.GetFileName(file)}\t{issue.Severity}\t{issue.SourceId}\t{issue.Field}: {issue.Message}");
                }
                Console.WriteLine($"audited {files.Count} manifest(s): {errors} error(s), {warnings} warning(s)");
                return strict && errors > 0 ? 1 : 0;
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

            case "project" when rest.Length >= 4 && rest[1] == "member" && rest[2] == "list":
            {
                var project = EntityId.Parse(rest[3], EntityKind.Collection);
                _ = projects.Load(project);
                foreach (var member in store.LoadProjectMembers(project))
                    Console.WriteLine(member);
                return 0;
            }

            case "project" when rest.Length >= 5 && rest[1] == "member" && rest[2] == "add":
            {
                var project = EntityId.Parse(rest[3], EntityKind.Collection);
                _ = projects.Load(project);
                store.AddProjectMember(project, rest[4]);
                Console.WriteLine($"added {rest[4]} to {rest[3]}");
                return 0;
            }

            case "project" when rest.Length >= 5 && rest[1] == "member" && rest[2] == "remove":
            {
                var project = EntityId.Parse(rest[3], EntityKind.Collection);
                _ = projects.Load(project);
                store.RemoveProjectMember(project, rest[4]);
                Console.WriteLine($"removed {rest[4]} from {rest[3]}");
                return 0;
            }

            case "project" when rest.Length >= 3 && rest[1] == "delete":
            {
                if (!rest.Contains("--force"))
                {
                    Console.Error.WriteLine("refusing deletion without --force (this removes the project document; back up first)");
                    return 1;
                }
                projects.Delete(EntityId.Parse(rest[2], EntityKind.Collection));
                Console.WriteLine($"deleted project {rest[2]}");
                return 0;
            }

            case "jobs" when rest.Length >= 2 && rest[1] == "list":
            {
                var limit = rest.Length >= 4 && rest[2] == "--limit" ? int.Parse(rest[3]) : 20;
                foreach (var job in store.LoadJobs(limit))
                    Console.WriteLine($"{job.Id}\t{job.Type}\t{job.Status}\tretries={job.RetriesUsed}");
                return 0;
            }

            case "layout" when rest.Length >= 3 && rest[1] == "inspect":
            {
                var model = PatchLayoutModel.Load(rest[2]);
                var metadataPath = Path.Combine(rest[2], "layout-metadata.json");
                var metadata = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metadataPath)).RootElement;
                Console.WriteLine($"package: {rest[2]}");
                Console.WriteLine($"architecture: {PatchLayoutModel.Architecture} {PatchLayoutModel.ModelVersion}");
                Console.WriteLine($"trained pages: {model.TrainedPageNames.Count}; epochs: {model.EpochCount}; threshold: {model.Threshold:0.00}");
                Console.WriteLine($"final loss: {model.FinalTrainingLoss:0.###}");
                foreach (var field in new[] { "TrainingDataDigest", "WeightsDigest", "AugmentTraining" })
                    if (metadata.TryGetProperty(field, out var value))
                        Console.WriteLine($"{field}: {value}");
                return 0;
            }

            case "layout" when rest.Length >= 4 && rest[1] == "train":
            {
                // Page-level split (same convention as all pilots): every 5th page test,
                // every 6th dev; training never sees test pages. Threshold is selected on dev.
                var dataDir = rest[2];
                string? Flag(string name) =>
                    Array.IndexOf(rest, name) is var index && index >= 0 && index + 1 < rest.Length ? rest[index + 1] : null;
                var outDir = Flag("--out") ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "models", "layout-patch");
                var imageExt = Flag("--image-ext");
                var epochs = Flag("--epochs") is { } e ? int.Parse(e) : 100;
                var seed = Flag("--seed") is { } s ? int.Parse(s) : 31;

                var xmlFiles = Directory.GetFiles(dataDir, "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
                if (xmlFiles.Count == 0) { Console.Error.WriteLine("no PAGE XML files found"); return 1; }

                string? ResolveImage(string xml) => new[]
                {
                    imageExt,
                    ".png", ".jpg", ".jpeg", ".tif", ".tiff",
                }.Where(e => e is not null)
                 .Select(e => Path.Combine(dataDir, Path.GetFileNameWithoutExtension(xml) + e))
                 .FirstOrDefault(File.Exists);

                var train = new List<LeviathanText.Layout.LayoutTrainingPage>();
                var dev = new List<LeviathanText.Layout.LayoutTrainingPage>();
                int testPages = 0;
                for (int i = 0; i < xmlFiles.Count; i++)
                {
                    var image = ResolveImage(xmlFiles[i]);
                    if (image is null) continue;
                    var imported = PageXml.Import(File.ReadAllText(xmlFiles[i]),
                        new EntityId(EntityKind.Capture, "cap"),
                        new Checksum { Algorithm = "unavailable", Digest = "pending" });
                    var bands = imported.Lines
                        .Select(l => l.Polygon.Count >= 3 ? l.Polygon : l.Baseline)
                        .Where(g => g.Count >= 2)
                        .Select(g =>
                        {
                            int top = (int)g.Min(p => p.Y);
                            int bottom = (int)g.Max(p => p.Y);
                            return (top, Math.Max(bottom, top + 1));
                        }).ToList();
                    if (bands.Count == 0) continue;

                    var page = new LeviathanText.Layout.LayoutTrainingPage(
                        Path.GetFileNameWithoutExtension(xmlFiles[i]), image, bands);
                    if (i % 5 == 0) testPages++;
                    else if (i % 6 == 1) dev.Add(page);
                    else train.Add(page);
                }

                if (train.Count == 0 || dev.Count == 0)
                {
                    Console.Error.WriteLine($"need train and dev pages (train={train.Count}, dev={dev.Count})");
                    return 1;
                }

                var options = new LeviathanText.Layout.PatchLayoutOptions
                {
                    Epochs = epochs,
                    Seed = seed,
                    AugmentTraining = !rest.Contains("--no-augment"),
                };
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var model = LeviathanText.Layout.PatchLayoutModel.Train(train, options,
                    message => Console.Error.WriteLine(message));
                watch.Stop();

                // Threshold on dev pages only.
                var devBitmaps = dev.Select(page => (page.Name, Bitmap: ImageDecoder.Decode(page.ImagePath).Bitmap, page.Bands)).ToList();
                try
                {
                    var probabilities = devBitmaps.ToDictionary(
                        page => page.Name, page => model.PredictRowProbabilities(page.Bitmap));
                    float bestThreshold = 0.5f;
                    double bestF1 = -1, bestP = 0, bestR = 0;
                    for (float threshold = 0.15f; threshold <= 0.851f; threshold += 0.05f)
                    {
                        var rows = devBitmaps.Select(page =>
                            (Witness: page.Name,
                                Detected: model.DetectFromProbabilities(page.Bitmap, probabilities[page.Name], threshold).Lines,
                                GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Bands)).ToList();
                        var summary = LayoutComparison.Compare(rows);
                        double f1 = summary.MeanPrecision + summary.MeanRecall == 0 ? 0
                            : 2 * summary.MeanPrecision * summary.MeanRecall / (summary.MeanPrecision + summary.MeanRecall);
                        if (f1 > bestF1) { bestF1 = f1; bestThreshold = threshold; bestP = summary.MeanPrecision; bestR = summary.MeanRecall; }
                    }
                    var tuned = model.WithThreshold(bestThreshold);
                    tuned.Save(outDir);
                    double bestF1Value = bestP + bestR == 0 ? 0 : 2 * bestP * bestR / (bestP + bestR);
                    var layoutRunId = $"run-{Guid.NewGuid().ToString("N")[..12]}";
                    store.SaveRun(new RunRecord
                    {
                        Id = layoutRunId,
                        Kind = "layout",
                        Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(outDir)),
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedBy = "cli",
                        DatasetDigest = PatchLayoutModel.DataDigestOf(train),
                        Config = new Dictionary<string, string>
                        {
                            ["model"] = PatchLayoutModel.Architecture,
                            ["epochs"] = epochs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["seed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["augmentTraining"] = options.AugmentTraining.ToString(),
                            ["threshold"] = bestThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["inputHeight"] = options.InputHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["maxInputWidth"] = options.MaxInputWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        },
                        Metrics = new Dictionary<string, double>
                        {
                            ["devPrecision"] = bestP,
                            ["devRecall"] = bestR,
                            ["devF1"] = bestF1Value,
                            ["finalLoss"] = tuned.FinalTrainingLoss,
                            ["threshold"] = bestThreshold,
                        },
                        History = tuned.LossHistory,
                        Artifacts = [outDir],
                        Notes = "PAGE-derived layout labels; single-column training. No test pages used in training.",
                    });

                    Console.WriteLine($"trained patch layout: train={train.Count} dev={dev.Count} test(held out)={testPages} " +
                                      $"epochs={epochs} time={watch.Elapsed.TotalSeconds:0.0}s");
                    Console.WriteLine($"threshold {bestThreshold:0.00}: dev P={bestP:0.###} R={bestR:0.###}; package: {outDir}");
                    Console.WriteLine($"recorded run: {layoutRunId}");
                    return 0;
                }
                finally
                {
                    foreach (var page in devBitmaps) page.Bitmap.Dispose();
                }
            }

            case "layout" when rest.Length >= 4 && rest[1] == "detect":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceId = EntityId.Parse(rest[3], EntityKind.Surface);
                var cache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
                var layout = new LayoutService(store, new ImageResolver(store, cache));
                var maxIndex = Array.IndexOf(rest, "--max-pixels");
                var maxPixels = maxIndex >= 0 && maxIndex + 1 < rest.Length ? long.Parse(rest[maxIndex + 1]) : 12_000_000;
                var modelIndex = Array.IndexOf(rest, "--model");
                PatchLayoutModel? patchModel = modelIndex >= 0 && modelIndex + 1 < rest.Length
                    ? PatchLayoutModel.Load(rest[modelIndex + 1])
                    : null;
                var outcome = layout.Detect(project, surfaceId, maxPixels, patchModel);
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

                var recognitionRunId = $"run-{Guid.NewGuid().ToString("N")[..12]}";
                store.SaveRun(new RunRecord
                {
                    Id = recognitionRunId,
                    Kind = "recognition",
                    Name = config.ModelName,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "cli",
                    DatasetDigest = splitHash,
                    Config = new Dictionary<string, string>
                    {
                        ["epochs"] = config.Epochs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["batchSize"] = config.BatchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["learningRate"] = config.LearningRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["weightDecay"] = config.WeightDecay.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["seed"] = config.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["hiddenSize"] = config.HiddenSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["augmentTraining"] = config.AugmentTraining.ToString(),
                        ["labels"] = labels.NumClasses.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    Metrics = new Dictionary<string, double>
                    {
                        ["bestDevCer"] = run.BestValidationCer,
                        ["epochsCompleted"] = run.EpochsCompleted,
                        ["trainLines"] = snapshot.Train.Count,
                        ["devLines"] = snapshot.Dev.Count,
                        ["testLockedLines"] = snapshot.TestLocked.Count,
                    },
                    History = [.. run.History.Select(h => h.ValidationCer)],
                    Artifacts = [package.Directory, package.ModelPath, package.ManifestPath],
                    Notes = "PAGE-derived labels; not specialist-adjudicated. Locked test split was not used.",
                });

                Console.WriteLine($"trained {config.ModelName}: best dev CER {run.BestValidationCer:0.###} over {run.EpochsCompleted} epochs");
                Console.WriteLine($"recorded run: {recognitionRunId}");
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
              leviathan [--store <path>] import pdf <projectId> <document.pdf> [--out <dir>] [--dpi 200] [--max-pages 500] [--text]
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
