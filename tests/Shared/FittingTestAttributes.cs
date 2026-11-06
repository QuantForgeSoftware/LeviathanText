using Xunit;

namespace LeviathanText.TestSupport;

/// <summary>Discovery-time opt-in: skipped cases never construct their test class.</summary>
public sealed class FittingFactAttribute : FactAttribute
{
    public FittingFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("LEVIATHANTEXT_RUN_FITTING") != "1")
            Skip = "Synthetic fitting requires explicit authorization and LEVIATHANTEXT_RUN_FITTING=1.";
    }
}

/// <summary>Theory counterpart; the opt-in is evaluated by each fresh test host.</summary>
public sealed class FittingTheoryAttribute : TheoryAttribute
{
    public FittingTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("LEVIATHANTEXT_RUN_FITTING") != "1")
            Skip = "Synthetic fitting requires explicit authorization and LEVIATHANTEXT_RUN_FITTING=1.";
    }
}
