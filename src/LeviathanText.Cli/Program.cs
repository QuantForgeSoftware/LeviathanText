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

            case "import" when rest.Length >= 4 && rest[1] == "page":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var (imported, unsupported) = interchange.ImportPage(project, rest[3]);
                Console.WriteLine($"imported object {imported.Id}, surface {imported.Surfaces[0].Id}, lines {imported.Surfaces[0].Lines.Count}");
                foreach (var issue in unsupported)
                    Console.WriteLine($"unsupported: {issue.Element}: {issue.Description}");
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

            case "recognize" when rest.Length >= 4 && rest[1] == "surface":
            {
                var project = projects.Load(EntityId.Parse(rest[2], EntityKind.Collection));
                var surfaceId = EntityId.Parse(rest[3], EntityKind.Surface);
                var packageDir = rest.Length >= 6 && rest[4] == "--package" ? rest[5] : null;
                packageDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "models", "p01-pilot");

                // Load the packaged model (manifest + label hash + weights verified by the loader);
                // a package that cannot prove its identity fails with ModelIncompatible.
                var loaded = RecognitionModelLoader.Load(packageDir);

                var pipeline = new RecognitionPipeline(store);
                var outcome = pipeline.Run(project, surfaceId, new RecognitionPipeline.Options
                {
                    ModelDirectory = packageDir,
                    ModelName = Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDir)),
                    ModelFactory = () => loaded.Model,
                    LabelSetFactory = () => loaded.Labels,
                });

                foreach (var line in outcome.Lines)
                    Console.WriteLine($"{line.LineId}\t{line.L0Text}");
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
