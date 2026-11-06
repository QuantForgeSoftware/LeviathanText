using Xunit;

namespace LeviathanText.TestSupport;

// Kept separate so ordinary engineering projects need no SkippableFact dependency.
public sealed class CapabilitySkippableFactAttribute : SkippableFactAttribute
{
    public CapabilitySkippableFactAttribute(params Type[] skippingExceptions) : base(skippingExceptions) => Skip = "Required capabilities must be explicitly specified and nonzero.";
    private TestCapabilities _required;
    public TestCapabilities Required
    {
        get => _required;
        set { _required = value; Skip = value == TestCapabilities.None ? "Required capabilities must be nonzero." : TestCapabilityPolicy.SkipReason(value); }
    }
}

public sealed class CapabilitySkippableTheoryAttribute : SkippableTheoryAttribute
{
    public CapabilitySkippableTheoryAttribute(params Type[] skippingExceptions) : base(skippingExceptions) => Skip = "Required capabilities must be explicitly specified and nonzero.";
    private TestCapabilities _required;
    public TestCapabilities Required
    {
        get => _required;
        set { _required = value; Skip = value == TestCapabilities.None ? "Required capabilities must be nonzero." : TestCapabilityPolicy.SkipReason(value); }
    }
}
