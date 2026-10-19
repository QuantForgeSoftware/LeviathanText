using LeviathanText.Application;
using Xunit;

namespace LeviathanText.Pilot.Tests;

/// <summary>Repository ledgers must pass the audit with zero errors.</summary>
public sealed class ManifestAuditPilotTests
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    [SkippableFact]
    public void Repository_manifests_have_no_audit_errors()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var dir = Path.Combine(root!, "data", "manifests");
        Skip.IfNot(Directory.Exists(dir), "manifest directory not found");

        var files = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var issues = CorpusManifestAudit.Audit(File.ReadAllText(file));
            Assert.DoesNotContain(issues, i => i.Severity == CorpusManifestAudit.Severity.Error);
        }
    }
}
