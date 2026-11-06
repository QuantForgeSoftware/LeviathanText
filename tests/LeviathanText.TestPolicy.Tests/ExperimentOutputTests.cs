using LeviathanText.TestSupport;
using Xunit;

namespace LeviathanText.TestPolicy.Tests;

public sealed class ExperimentOutputTests : IDisposable
{
    // Owned generated filesystem only. Resolve macOS /var alias before passing the explicit physical path.
    private readonly string _root;
    private readonly string _repository;
    private readonly string _container;
    public ExperimentOutputTests()
    {
        _root = Directory.CreateTempSubdirectory("lt-test-policy-").FullName;
        if (_root.StartsWith("/var/", StringComparison.Ordinal)) _root = "/private" + _root;
        _repository = Path.Combine(_root, "repository");
        _container = Path.Combine(_repository, ".artifacts", "experiments", "generated-container");
        Directory.CreateDirectory(_container);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative")]
    public void Missing_or_relative_container_has_no_fallback_or_output(string? container)
    {
        var before = Entries();
        Assert.Throws<InvalidOperationException>(() => Reserve(container));
        Assert.Equal(before, Entries());
    }

    [Theory]
    [InlineData("docs/evaluation")]
    [InlineData(".artifacts/datasets")]
    [InlineData(".artifacts/models")]
    [InlineData(".artifacts/releases")]
    [InlineData(".artifacts/experiments-similar")]
    public void Historical_or_prefix_lookalike_containers_are_rejected_without_mutation(string relative)
    {
        var path = Path.Combine(_repository, relative);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "existing.txt"), "untouched");
        var before = Entries();
        Assert.Throws<InvalidOperationException>(() => Reserve(path));
        Assert.Equal(before, Entries());
        Assert.Equal("untouched", File.ReadAllText(Path.Combine(path, "existing.txt")));
    }

    [Fact]
    public void Reservation_keeps_receipts_and_repeated_experiments_receive_distinct_fresh_children()
    {
        var first = ExperimentOutput.ReserveAt("generated-case", _repository, _container, () => new string('a', 32));
        var second = ExperimentOutput.ReserveAt("generated-case", _repository, _container, () => new string('b', 32));
        Assert.NotEqual(first, second);
        Assert.StartsWith(_container + Path.DirectorySeparatorChar, first);
        Assert.Single(Directory.GetFiles(first));
        Assert.True(File.Exists(first + ".reservation.json"));
        Assert.True(File.Exists(Path.Combine(first, "experiment-reservation.json")));
        File.WriteAllText(Path.Combine(first, "report.txt"), "first evidence");
        Assert.Throws<IOException>(() => Reserve(_container));
        Assert.Equal("first evidence", File.ReadAllText(Path.Combine(first, "report.txt")));
    }

    [Fact]
    public void Preexisting_child_is_never_reused_even_when_no_reservation_token_exists()
    {
        var child = Path.Combine(_container, "generated-case-" + new string('a', 32));
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(child, "report.txt"), "historical");
        Assert.Throws<IOException>(() => Reserve(_container));
        Assert.Equal(new[] { "report.txt" }, Directory.GetFiles(child).Select(Path.GetFileName));
        Assert.Equal("historical", File.ReadAllText(Path.Combine(child, "report.txt")));
        Assert.True(File.Exists(child + ".reservation.json")); // failed reservation retained, never cleaned/reused
    }

    [Fact]
    public async Task Concurrent_cooperative_writers_have_one_winner_for_the_same_reservation()
    {
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            try { return Reserve(_container); }
            catch (IOException) { return null; }
        }));
        var results = await Task.WhenAll(tasks);
        Assert.Single(results, result => result is not null);
        Assert.Single(Directory.GetDirectories(_container));
    }

    [Fact]
    public void Linked_container_or_child_is_refused_before_evidence_mutation()
    {
        var elsewhere = Path.Combine(_root, "outside");
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "report.txt"), "unchanged");
        var link = Path.Combine(_container, "alias");
        Directory.CreateSymbolicLink(link, elsewhere);
        Assert.Throws<InvalidOperationException>(() => Reserve(link));
        Assert.Equal(new[] { "report.txt" }, Directory.GetFiles(elsewhere).Select(Path.GetFileName));
        var child = Path.Combine(_container, "generated-case-" + new string('a', 32));
        Directory.CreateSymbolicLink(child, elsewhere);
        Assert.Throws<IOException>(() => Reserve(_container));
        Assert.Equal("unchanged", File.ReadAllText(Path.Combine(elsewhere, "report.txt")));
    }

    [Fact]
    public void Invalid_identifier_and_suffix_are_refused_before_reservation()
    {
        var before = Entries();
        Assert.Throws<ArgumentException>(() => ExperimentOutput.ReserveAt("../escape", _repository, _container, () => new string('a', 32)));
        Assert.Throws<InvalidOperationException>(() => ExperimentOutput.ReserveAt("valid", _repository, _container, () => "../escape"));
        Assert.Equal(before, Entries());
    }

    [Fact]
    public void Explicit_physical_temp_container_is_supported_but_missing_container_is_not_created()
    {
        var tempContainer = Path.Combine(_root, "temp-container");
        Assert.Throws<DirectoryNotFoundException>(() => Reserve(tempContainer));
        Assert.False(Directory.Exists(tempContainer));
        Directory.CreateDirectory(tempContainer);
        Assert.StartsWith(tempContainer, Reserve(tempContainer));
    }

    [Fact]
    public void Case_alias_of_repository_docs_cannot_gain_temp_container_permission()
    {
        var docs = Path.Combine(_repository, "docs", "evaluation");
        Directory.CreateDirectory(docs);
        var alias = Path.Combine(Path.GetDirectoryName(_repository)!, "REPOSITORY", "docs", "evaluation");
        // On a case-sensitive filesystem this alias does not resolve; the bypass requires a real alias.
        if (!Directory.Exists(alias)) return;
        var before = Entries();
        Assert.Throws<InvalidOperationException>(() => Reserve(alias));
        Assert.Equal(before, Entries());
    }

    private string Reserve(string? container) => ExperimentOutput.ReserveAt("generated-case", _repository, container, () => new string('a', 32));
    private string[] Entries() => Directory.GetFileSystemEntries(_repository, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
