using Xunit;

namespace LeviathanText.TestSupport;

[Flags]
public enum TestCapabilities
{
    None = 0,
    Fitting = 1,
    Corpus = 2,
    LockedEvaluation = 4,
}

/// <summary>Execution gates, not authorization. Reads flags only; never probes data or native libraries.</summary>
public static class TestCapabilityPolicy
{
    public static string? SkipReason(TestCapabilities required, Func<string, string?>? readEnvironment = null)
    {
        const TestCapabilities known = TestCapabilities.Fitting | TestCapabilities.Corpus | TestCapabilities.LockedEvaluation;
        if ((required & ~known) != 0)
            return "Unknown test capability; execution refused.";
        readEnvironment ??= Environment.GetEnvironmentVariable;
        var missing = new List<string>();
        Check(TestCapabilities.Fitting, "LEVIATHANTEXT_RUN_FITTING");
        Check(TestCapabilities.Corpus, "LEVIATHANTEXT_RUN_CORPUS");
        Check(TestCapabilities.LockedEvaluation, "LEVIATHANTEXT_RUN_LOCKED");
        return missing.Count == 0 ? null :
            "Requires separate authorization and explicit " + string.Join(", ", missing) + ".";

        void Check(TestCapabilities capability, string variable)
        {
            if ((required & capability) != 0 && readEnvironment(variable) != "1")
                missing.Add(variable + "=1");
        }
    }
}

/// <summary>Single xUnit discoverer: skips before class construction and theory data enumeration.</summary>
public sealed class CapabilityFactAttribute : FactAttribute
{
    public CapabilityFactAttribute(TestCapabilities required) => Skip = required == TestCapabilities.None ? "Required capabilities must be nonzero." : TestCapabilityPolicy.SkipReason(required);
}

public sealed class CapabilityTheoryAttribute : TheoryAttribute
{
    public CapabilityTheoryAttribute(TestCapabilities required) => Skip = required == TestCapabilities.None ? "Required capabilities must be nonzero." : TestCapabilityPolicy.SkipReason(required);
}
