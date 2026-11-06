using System.Text.Json;

namespace LeviathanText.TestSupport;

/// <summary>
/// Fresh evidence directories for explicitly selected experiments. This is a cooperative single-host reservation,
/// not a hostile-filesystem atomicity guarantee. Flags and this output location do not grant execution permission.
/// </summary>
public static class ExperimentOutput
{
    public const string ContainerVariable = "LEVIATHANTEXT_TEST_OUTPUT_DIR";

    public static string Reserve(string experimentName, string repositoryRoot) =>
        ReserveAt(experimentName, repositoryRoot, Environment.GetEnvironmentVariable(ContainerVariable), () => Guid.NewGuid().ToString("N"));

    internal static string ReserveAt(string experimentName, string repositoryRoot, string? container, Func<string> newSuffix)
    {
        if (string.IsNullOrWhiteSpace(container) || !Path.IsPathFullyQualified(container))
            throw new InvalidOperationException($"Set {ContainerVariable} to an existing absolute output container; no output fallback is permitted.");
        if (!Path.IsPathFullyQualified(repositoryRoot)) throw new ArgumentException("Repository root must be absolute.", nameof(repositoryRoot));
        if (experimentName.Length is < 1 or > 160 || experimentName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            throw new ArgumentException("Use a bounded experiment identifier containing only ASCII letters, digits, underscore or hyphen.", nameof(experimentName));

        var repo = PhysicalDirectory(repositoryRoot);
        var experiments = Path.Combine(repo, ".artifacts", "experiments");
        var temp = PhysicalDirectory(Path.GetTempPath());
        var fullContainer = Path.TrimEndingDirectorySeparator(Path.GetFullPath(container));
        // Require physical paths deliberately: e.g. /private/var/... rather than macOS /var aliases.
        string? approved = Within(fullContainer, experiments) ? experiments :
            Within(fullContainer, temp) && !Within(fullContainer, repo) ? temp : null;
        if (approved is null)
            throw new InvalidOperationException("Output container must be under repository .artifacts/experiments or physical OS temp, never historical docs/data/models/releases. Use physical paths (macOS /private/...).");
        RejectLinks(repo, experiments, checkMissing: true);
        RejectLinks(approved, fullContainer, checkMissing: false);
        if (!Directory.Exists(fullContainer)) throw new DirectoryNotFoundException("Output container must already exist.");
        if (!string.Equals(PhysicalDirectory(fullContainer), fullContainer, PathComparison))
            throw new InvalidOperationException("Output container aliases are not permitted; use its physical path.");

        string suffix = newSuffix();
        if (suffix.Length != 32 || suffix.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidOperationException("Invalid reservation suffix.");
        var child = Path.Combine(fullContainer, experimentName + "-" + suffix);
        var receipt = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, experimentName, directory = child, createdAt = DateTimeOffset.UtcNow,
            processId = Environment.ProcessId, reservationScope = "cooperative-single-host",
        });
        // Exclusive retained sibling token coordinates participating writers, including deterministic collision tests.
        using (var token = new FileStream(child + ".reservation.json", FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(token)) writer.Write(receipt);
        if (Directory.Exists(child) || File.Exists(child)) throw new IOException("Experiment directory already exists; never resume or overwrite evidence.");
        Directory.CreateDirectory(child);
        RejectLinks(approved, child, checkMissing: false);
        if (Directory.EnumerateFileSystemEntries(child).Any()) throw new IOException("Reserved directory is not empty.");
        using (var marker = new FileStream(Path.Combine(child, "experiment-reservation.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(marker)) writer.Write(receipt);
        return child;
    }

    private static StringComparison PathComparison => (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static bool Within(string candidate, string root) =>
        string.Equals(candidate, root, PathComparison) || candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, PathComparison);

    private static string PhysicalDirectory(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if (!directory.Exists) throw new DirectoryNotFoundException("Required directory does not exist: " + path);
        if (directory.Parent is null) return directory.FullName;
        var physicalParent = PhysicalDirectory(directory.Parent.FullName);
        var physical = new DirectoryInfo(Path.Combine(physicalParent, directory.Name));
        return Path.TrimEndingDirectorySeparator(physical.LinkTarget is null ? physical.FullName :
            physical.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? throw new IOException("Unresolved directory link."));
    }

    private static void RejectLinks(string root, string target, bool checkMissing)
    {
        if (!Within(target, root)) throw new InvalidOperationException("Output path escapes approved root.");
        var current = root;
        Check(current);
        foreach (var segment in Path.GetRelativePath(root, target).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..") throw new InvalidOperationException("Output path traversal is not permitted.");
            current = Path.Combine(current, segment);
            Check(current);
        }
        void Check(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                if (checkMissing) return;
                throw new DirectoryNotFoundException("Output path does not exist.");
            }
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Symlink/reparse output paths are not permitted.");
        }
    }
}
