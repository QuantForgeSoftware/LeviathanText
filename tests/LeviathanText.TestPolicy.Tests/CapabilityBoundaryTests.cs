using System.Collections;
using LeviathanText.TestSupport;
using Xunit;

namespace LeviathanText.TestPolicy.Tests;

public sealed class CapabilityPolicyTests
{
    [Fact]
    public void Independent_exact_flags_fail_closed_without_IO()
    {
        var flags = new Dictionary<string, string?>();
        string? Read(string name) => flags.GetValueOrDefault(name);
        Assert.Null(TestCapabilityPolicy.SkipReason(TestCapabilities.None, _ => throw new Exception("Unneeded environment lookup")));
        foreach (var (capability, flag) in new[]
        {
            (TestCapabilities.Fitting, "LEVIATHANTEXT_RUN_FITTING"),
            (TestCapabilities.Corpus, "LEVIATHANTEXT_RUN_CORPUS"),
            (TestCapabilities.LockedEvaluation, "LEVIATHANTEXT_RUN_LOCKED"),
        })
        {
            foreach (string? value in new[] { null, "", "true", "01", "1 ", "0" })
            {
                flags[flag] = value;
                Assert.Contains(flag, TestCapabilityPolicy.SkipReason(capability, Read));
            }
            flags[flag] = "1";
            Assert.Null(TestCapabilityPolicy.SkipReason(capability, Read));
            flags.Clear();
        }
        flags["LEVIATHANTEXT_RUN_FITTING"] = "1";
        Assert.NotNull(TestCapabilityPolicy.SkipReason(TestCapabilities.Fitting | TestCapabilities.Corpus, Read));
        Assert.NotNull(TestCapabilityPolicy.SkipReason((TestCapabilities)8, _ => throw new Exception("Unknown capability lookup")));
    }
}

// Harmless observability only: no model, corpus, native dependency or optimizer exists in this project.
internal static class Sentinel
{
    internal static void Mark(string name)
    {
        if (Environment.GetEnvironmentVariable("LEVIATHANTEXT_POLICY_MARKER") is { Length: > 0 } path)
            File.AppendAllText(path, name + Environment.NewLine);
    }
}

public sealed class PlainCapabilitySentinels
{
    public PlainCapabilitySentinels() => Sentinel.Mark("plain.constructor");
    [CapabilityFact(TestCapabilities.Fitting)]
    public void Fitting() => Sentinel.Mark("plain.fitting.body");
    [CapabilityFact(TestCapabilities.Corpus)]
    public void Corpus() => Sentinel.Mark("plain.corpus.body");
    [CapabilityFact(TestCapabilities.LockedEvaluation)]
    public void Locked() => Sentinel.Mark("plain.locked.body");
    [CapabilityFact(TestCapabilities.Fitting | TestCapabilities.Corpus | TestCapabilities.LockedEvaluation)]
    public void All() => Sentinel.Mark("plain.all.body");
    public static IEnumerable<object[]> Rows()
    {
        Sentinel.Mark("plain.memberdata");
        yield return new object[] { 1 };
    }
    [CapabilityTheory(TestCapabilities.Corpus)]
    [MemberData(nameof(Rows))]
    public void Member(int value) { Assert.Equal(1, value); Sentinel.Mark("plain.member.body"); }
    [CapabilityTheory(TestCapabilities.Corpus)]
    [ClassData(typeof(PlainRows))]
    public void Class(int value) { Assert.Equal(1, value); Sentinel.Mark("plain.class.body"); }
}
public sealed class PlainRows : IEnumerable<object[]>
{
    public PlainRows() => Sentinel.Mark("plain.classdata.constructor");
    public IEnumerator<object[]> GetEnumerator() { Sentinel.Mark("plain.classdata.enumerate"); yield return new object[] { 1 }; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class SkippableCapabilitySentinels
{
    public SkippableCapabilitySentinels() => Sentinel.Mark("skippable.constructor");
    public static IEnumerable<object[]> Rows()
    {
        Sentinel.Mark("skippable.memberdata");
        yield return new object[] { 1 };
    }
    [CapabilitySkippableFact(Required = TestCapabilities.Fitting | TestCapabilities.Corpus | TestCapabilities.LockedEvaluation)]
    public void All() => Sentinel.Mark("skippable.all.body");
    [CapabilitySkippableFact(Required = TestCapabilities.Corpus)]
    public void Missing_opted_in_data_retains_runtime_skip()
    {
        Sentinel.Mark("skippable.runtime.skip");
        Skip.IfNot(false, "Harmless sentinel: no corpus fixture exists or is accessed.");
        throw new Exception("Runtime skip did not stop execution");
    }
    [CapabilitySkippableTheory(Required = TestCapabilities.Corpus)]
    [MemberData(nameof(Rows))]
    public void Member(int value) { Assert.Equal(1, value); Sentinel.Mark("skippable.member.body"); }
    [CapabilitySkippableTheory(Required = TestCapabilities.Corpus)]
    [ClassData(typeof(SkippableRows))]
    public void Class(int value) { Assert.Equal(1, value); Sentinel.Mark("skippable.class.body"); }
}
public sealed class SkippableRows : IEnumerable<object[]>
{
    public SkippableRows() => Sentinel.Mark("skippable.classdata.constructor");
    public IEnumerator<object[]> GetEnumerator() { Sentinel.Mark("skippable.classdata.enumerate"); yield return new object[] { 1 }; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Misconfigured attributes must never become an accidental opt-in, even with every flag set.
public sealed class InvalidCapabilitySentinels
{
    public InvalidCapabilitySentinels() => Sentinel.Mark("INVALID.constructor");
    [CapabilitySkippableFact]
    public void Omitted_fact() => throw new Exception("Omitted Required ran");
    [CapabilitySkippableTheory]
    [InlineData(1)]
    public void Omitted_theory(int value) => throw new Exception($"Omitted Required ran: {value}");
    [CapabilityFact(TestCapabilities.None)]
    public void None_fact() => throw new Exception("None ran");
    [CapabilityTheory(TestCapabilities.None)]
    [InlineData(1)]
    public void None_theory(int value) => throw new Exception($"None ran: {value}");
    [CapabilitySkippableFact(Required = TestCapabilities.None)]
    public void None_skippable_fact() => throw new Exception("None ran");
    [CapabilitySkippableTheory(Required = TestCapabilities.None)]
    [InlineData(1)]
    public void None_skippable_theory(int value) => throw new Exception($"None ran: {value}");
    [CapabilityFact((TestCapabilities)8)]
    public void Unknown() => throw new Exception("Unknown capability ran");
}
