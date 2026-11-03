using Xunit;

// Process-level CLI tests redirect Console.Out/Error to capture command output; xUnit's
// per-class parallelism would interleave those global streams and make captures flaky.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
